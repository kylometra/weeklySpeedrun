using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public class ViewComponentInteractions(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client) : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
{
    private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();

    [ComponentInteraction("view-week", true)]
    public async Task ViewLevel()
    {
        // check if the week is assigned to the current guild
        ulong id = ulong.Parse(Context.Interaction.Data.Values.First());
        var week = await _dbContext.Weeks.FindAsync(id);
        if (week == null 
            || week.GuildId != Context.Guild.Id)
        {
            await RespondAsync("That level doesn't exist!", ephemeral: true);
            return;
        }
        
        var eb = _dbContext.LeaderboardBuilder(client, week, null, false);
        await Context.Interaction.Message.ModifyAsync(m => m.Embed = eb.Build());
        await DeferAsync();
    }
}