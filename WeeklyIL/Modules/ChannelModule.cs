using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public partial class ManageModule
{
    [Group("channel", "Commands for setting up channels")]
    public class ChannelModule(IDbContextFactory<WilDbContext> contextFactory)
        : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();

        [SlashCommand("submissions", "Sets the channel that submissions go to")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetSubmissionChannel(SocketTextChannel channel)
        {
            await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

            _dbContext.Guild(Context.Guild.Id).SubmissionsChannel = channel.Id;
            await _dbContext.SaveChangesAsync();

            await RespondAsync($"Successfully set submissions channel to <#{channel.Id}>!", ephemeral: true);
        }

        [SlashCommand("announcements", "Sets the channel that announcements go to")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SetAnnounceChannel(SocketTextChannel channel)
        {
            await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

            _dbContext.Guild(Context.Guild.Id).AnnouncementsChannel = channel.Id;
            await _dbContext.SaveChangesAsync();

            await RespondAsync($"Successfully set announcements channel to <#{channel.Id}>!", ephemeral: true);
        }
    }
}