using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public class PastModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();
    private readonly DiscordSocketClient _client = client;

    [SlashCommand("past", "Shows previous levels")]
    public async Task PastLevels()
    {
        var we = await _dbContext.CurrentWeek(Context.Guild.Id);
        if (we == null)
        {
            await RespondAsync("nope", ephemeral: true);
            return;
        }
        
        var eb = new EmbedBuilder().WithTitle("Previous levels");
        foreach (var week in _dbContext.Weeks
                     .Where(w => w.GuildId == Context.Guild.Id)
                     .Where(w => w.StartTimestamp < we.StartTimestamp)
                     .OrderByDescending(w => w.StartTimestamp))
        {
            eb.AddField($"<t:{week.StartTimestamp}:D> ID: {week.Id}", week.Level);
        }
        await RespondAsync(embed: eb.Build(), ephemeral: true);
    }
}