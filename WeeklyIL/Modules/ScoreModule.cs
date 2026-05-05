using System.Globalization;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public partial class ManageModule
{
    [Group("score", "Commands for managing scores")]
    public class ScoreModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
        : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();

        private async Task<bool> PermissionsFail()
        {
            if (await _dbContext.UserIsOrganizer(Context))
            {
                return false;
            }

            await RespondAsync("You can't do that here!", ephemeral: true);
            return true;
        }

        [SlashCommand("clear", "Removes all scores from leaderboard by id")]
        public async Task ClearLeaderboard(ulong? week = null)
        {
            if (await PermissionsFail())
            {
                return;
            }

            ulong weekId = week ?? (await _dbContext.CurrentWeek(Context.Guild.Id))?.Id ?? 0;
            var we = await _dbContext.Weeks
                .Where(w => w.GuildId == Context.Guild.Id)
                .FirstOrDefaultAsync(w => w.Id == weekId);

            if (we == null
                || (we.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    && !await _dbContext.UserIsOrganizer(Context)))
            {
                await RespondAsync("No week to clear!", ephemeral: true);
                return;
            }

            _dbContext.Scores.RemoveRange(_dbContext.Scores.Where(s => s.WeekId == weekId));
            await _dbContext.SaveChangesAsync();

            await RespondAsync($"Successfully cleared scores for week {weekId}!", ephemeral: true);
        }

        [SlashCommand("all", "Shows the full leaderboard for a week")]
        public async Task LeaderboardAll(ulong? weekId = null)
        {
            await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

            if (await PermissionsFail())
            {
                return;
            }

            // check if the week is assigned to the current guild
            var week = weekId == null
                ? await _dbContext.CurrentWeek(Context.Guild.Id)
                : await _dbContext.Weeks.FirstOrDefaultAsync(w => w.Id == weekId);

            if (week == null || week.GuildId != Context.Guild.Id)
            {
                await RespondAsync("That level doesn't exist!", ephemeral: true);
                return;
            }

            bool isCurrent = week.Id == (await _dbContext.CurrentWeek(Context.Guild.Id))?.Id;
            bool showVideo = !isCurrent || week.ShowVideo;

            WeekEntity? nw = null;
            if (isCurrent)
            {
                nw = _dbContext.Weeks
                    .Where(w => w.GuildId == Context.Guild.Id)
                    .Where(w => w.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    .OrderBy(w => w.StartTimestamp)
                    .FirstOrDefault();
            }

            var eb = _dbContext.LeaderboardBuilder(client, week, nw, showVideo, true);

            await RespondAsync(embed: eb.Build(), ephemeral: true);
        }

        [SlashCommand("edit", "Edit a time")]
        public async Task EditScore(ulong scoreId, string time)
        {
            bool cont = TimeSpan.TryParseExact(
                time, @"m\:ss\.fff", CultureInfo.InvariantCulture,
                out var ts);

            var score = _dbContext.Scores.FirstOrDefault(s => s.Id == scoreId);
            if (score == null)
            {
                await RespondAsync("This score doesn't exist!", ephemeral: true);
                return;
            }

            var week = _dbContext.Week(score.WeekId);
            if (week.GuildId != Context.Guild.Id)
            {
                await RespondAsync("This score doesn't exist!", ephemeral: true);
                return;
            }

            if (!score.Verified)
            {
                await RespondAsync("This run hasn't been verified!", ephemeral: true);
                return;
            }

            if (!cont)
            {
                await RespondAsync("Failed to parse the time! (format m:ss.fff)", ephemeral: true);
                return;
            }

            score.TimeMs = (uint)ts.TotalMilliseconds;
            await _dbContext.SaveChangesAsync();

            await RespondAsync($"Successfully set the time of score {scoreId} to {time}", ephemeral: true);
        }
    }
}