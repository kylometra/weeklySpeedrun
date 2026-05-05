using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public class LeaderboardModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();

    [SlashCommand("lb", "Shows the leaderboard for a level")]
    public async Task ViewLevel(ulong? id = null)
    {
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        
        // check if the level is assigned to the current guild
        var week = id == null 
            ? await _dbContext.CurrentWeek(Context.Guild.Id) 
            : await _dbContext.Weeks.FirstOrDefaultAsync(w => w.Id == id);
        
        bool secret = week?.StartTimestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool organizer = await _dbContext.UserIsOrganizer(Context);
        
        if (week == null 
            || week.GuildId != Context.Guild.Id
            || (secret && !organizer))
        {
            await RespondAsync("That leaderboard doesn't exist!", ephemeral: true);
            return;
        }

        bool isCurrent = week.Id == (await _dbContext.CurrentWeek(Context.Guild.Id))?.Id;
        bool showVideo = !isCurrent || organizer || week.ShowVideo;
        secret |= isCurrent && showVideo && !week.ShowVideo;

        WeekEntity? nw = null;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (isCurrent)
        {
            nw = await _dbContext.Weeks
                .Where(w => w.GuildId == Context.Guild.Id)
                .Where(w => w.StartTimestamp > now)
                .OrderBy(w => w.StartTimestamp)
                .FirstOrDefaultAsync();
        }
        var eb = _dbContext.LeaderboardBuilder(client, week, nw, showVideo);
        var sb = new SelectMenuBuilder()
            .WithPlaceholder("Select a leaderboard")
            .WithCustomId("view-week");
        foreach (var w in _dbContext.Weeks.Where(w => w.GuildId == Context.Guild.Id)
                     .Where(w => w.StartTimestamp <= now)
                     .OrderByDescending(w => (long)w.Id)
                     .Take(25))
        {
            sb.AddOption(w.Level, w.Id.ToString(), $"ID: {w.Id}");
        }
        var cb = ComponentBuilder.FromComponents([sb.Build()]);
        
        await RespondAsync(embed: eb.Build(), components: cb.Build(), ephemeral: secret);
    }
}