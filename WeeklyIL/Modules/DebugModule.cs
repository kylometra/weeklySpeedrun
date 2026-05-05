using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Services;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public partial class ManageModule
{
    [Group("debug", "Commands for fixing bugs")]
    public class DebugModule(
        IDbContextFactory<WilDbContext> contextFactory,
        DiscordSocketClient client,
        CloseSubmissionsTimers levelEnder)
        : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();

        [SlashCommand("clearstats", "oops")]
        [RequireOwner]
        public async Task ClearStats(SocketGuildUser? user = null)
        {
            user ??= client.GetGuild(Context.Guild.Id).GetUser(Context.User.Id);

            await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
            await _dbContext.CreateUserIfNotExists(user.Id);

            var ue = _dbContext.User(user.Id);
            ue.WeeklyWins = 0;
            await _dbContext.SaveChangesAsync();

            await RespondAsync("Success!", ephemeral: true);
        }

        [SlashCommand("closesubs", "oops")]
        [RequireOwner]
        public async Task CloseSubmissions(ulong id)
        {
            var week = await _dbContext.Weeks.FindAsync(id);
            if (week == null)
            {
                await RespondAsync("That level doesn't exist!", ephemeral: true);
                return;
            }

            if (await levelEnder.TryCloseSubmissions(week!))
            {
                await RespondAsync("Success!", ephemeral: true);
            }
            else
            {
                await RespondAsync("Failed to close leaderboard (pending submissions)", ephemeral: true);
            }
        }

        [SlashCommand("opensubs", "oops")]
        [RequireOwner]
        public async Task OpenSubmissions(ulong id)
        {
            var week = await _dbContext.Weeks.FindAsync(id);
            if (week == null)
            {
                await RespondAsync("That level doesn't exist!", ephemeral: true);
                return;
            }

            week.Ended = false;
            await _dbContext.SaveChangesAsync();
            await RespondAsync("Success!", ephemeral: true);
        }
    }
}