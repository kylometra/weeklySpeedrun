using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

[Group("submit", "Commands for submitting your times")]
public class SubmitModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();

    [SlashCommand("video", "Submits a time with video proof")]
    public async Task WithVideo(string video, ulong? weekId = null)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        ulong subChannel = _dbContext.Guilds.First(g => g.Id == Context.Guild.Id).SubmissionsChannel;
        if (subChannel == 0)
        {
            await RespondAsync("No submission channel to submit to!", ephemeral: true);
            return;
        }

        weekId ??= (await _dbContext.CurrentWeek(Context.Guild.Id))?.Id ?? 0;
        var we = _dbContext.Weeks
            .Where(w => w.GuildId == Context.Guild.Id)
            .FirstOrDefault(w => w.Id == weekId);
        
        if (we == null || we.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            await RespondAsync("No leaderboard to submit to!", ephemeral: true);
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!we.Ended && we.StartTimestamp < _dbContext.Weeks
                .Where(w => w.GuildId == Context.Guild.Id)
                .Where(w => w.StartTimestamp < now)
                .OrderBy(w => w.StartTimestamp).Last().StartTimestamp)
        {
            await RespondAsync("This leaderboard is currently not accepting submissions! Try again after the results are posted.", ephemeral: true);
            return;
        }

        if (!(Uri.TryCreate(video, UriKind.Absolute, out var result)
              && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps)))
        {
            await RespondAsync("Video is not a valid link!", ephemeral: true);
            return;
        }
        
        var score = await _dbContext.Scores.AddAsync(new ScoreEntity
        {
            UserId = Context.User.Id,
            WeekId = (ulong)weekId,
            Video = video
        });
        await _dbContext.SaveChangesAsync();

        var cb = new ComponentBuilder()
            .WithButton("Verify", "verify_button", ButtonStyle.Success)
            .WithButton("Reject", "reject_button", ButtonStyle.Danger);

        var channel = (SocketTextChannel)await client.GetChannelAsync(subChannel);

        string level = we.Level;
        var uri = level.GetUriFromString();
        if (uri != null)
        {
            level = level.Replace(uri.OriginalString, $"<{uri.OriginalString}>");
        }
        
        await channel.SendMessageAsync($"ID: {score.Entity.Id} | User: {Context.User.Username} | Level: {level}\n\nVideo: {video}", components: cb.Build());
        
        await RespondAsync("Video submitted! It will be timed and verified soon.", ephemeral: true);
    }
    
    [SlashCommand("blank", "Submits a blank time to the leaderboard - you won't have a time without proof")]
    public async Task NoVideo(ulong? weekId = null)
    {
        weekId ??= (await _dbContext.CurrentWeek(Context.Guild.Id))?.Id ?? 0;
        var we = await _dbContext.Weeks
            .Where(w => w.GuildId == Context.Guild.Id)
            .FirstOrDefaultAsync(w => w.Id == weekId);
        
        if (we == null || (we.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds() && !await _dbContext.UserIsOrganizer(Context)))
        {
            await RespondAsync("No leaderboard to submit to!", ephemeral: true);
            return;
        }
        
        await _dbContext.Scores.AddAsync(new ScoreEntity
        {
            UserId = Context.User.Id,
            WeekId = (ulong)weekId,
            TimeMs = uint.MaxValue,
            Verified = true
        });
        await _dbContext.SaveChangesAsync();
        
        await RespondAsync("Submitted without proof! You'll show up on the leaderboard without a time.", ephemeral: true);
        
        var user = Context.Guild.GetUser(Context.User.Id);
        await user.AddRolesAsync(_dbContext.Guilds
            .Include(g => g.GameRoles)
            .First(g => g.Id == we.GuildId).GameRoles
            .Where(r => r.Game == we.Game)
            .Select(r => r.RoleId));
    }
}

