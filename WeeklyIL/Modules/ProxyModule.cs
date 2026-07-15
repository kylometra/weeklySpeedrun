using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public partial class ManageModule
{
    [Group("proxy", "Commands for proxying guilds")]
    public class ProxyModule(IDbContextFactory<WilDbContext> contextFactory)
        : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();

        [SlashCommand("guild", "Sets the guild to proxy")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetGuild(string id)
        {
            await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

            var guild = Context.Client.GetGuild(ulong.Parse(id));

            _dbContext.Guild(Context.Guild.Id).ProxyFor = guild.Id;
            await _dbContext.SaveChangesAsync();

            await RespondAsync($"Successfully enabled leaderboard proxying for [{guild.Name}](https://discord.com/channels/{guild.Id})!", ephemeral: true);
        }
        
        [SlashCommand("disable", "Disables leaderboard proxying")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task Disable()
        {
            await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

            _dbContext.Guild(Context.Guild.Id).ProxyFor = 0;
            await _dbContext.SaveChangesAsync();

            await RespondAsync("Successfully disabled leaderboard proxying!", ephemeral: true);
        }
    }
}