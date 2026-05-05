using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public partial class ManageModule
{
    [Group("role", "Commands for managing roles")]
    public class RoleModule(IDbContextFactory<WilDbContext> contextFactory)
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

        [SlashCommand("moderator", "Sets the moderator permissions role")]
        [RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task SetModRole(SocketRole role)
        {
            await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

            _dbContext.Guild(Context.Guild.Id).ModeratorRole = role.Id;
            await _dbContext.SaveChangesAsync();

            await RespondAsync($"Successfully set moderator role to {role.Mention}!", ephemeral: true);
        }

        [SlashCommand("organizer", "Sets the organizer permissions role")]
        [RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task SetOrgRole(SocketRole role)
        {
            await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);

            _dbContext.Guild(Context.Guild.Id).OrganizerRole = role.Id;
            await _dbContext.SaveChangesAsync();

            await RespondAsync($"Successfully set organizer role to {role.Mention}!", ephemeral: true);
        }

        [SlashCommand("weekly", "Sets a role for a certain number of weekly WRs")]
        public async Task SetWeeklyRole(int requirement, SocketRole role)
        {
            if (await PermissionsFail())
            {
                return;
            }

            if (requirement < 1)
            {
                await RespondAsync($"Requirement cannot be less than 1.", ephemeral: true);
                return;
            }

            var guild = _dbContext.Guilds
                .Include(g => g.WeeklyRoles)
                .First(g => g.Id == Context.Guild.Id);
            var roles = guild.WeeklyRoles.Where(r => r.Requirement == requirement || r.RoleId == role.Id);
            foreach (var wr in roles)
            {
                guild.WeeklyRoles.Remove(wr);
            }

            guild.WeeklyRoles.Add(new WeeklyRole { Requirement = (uint)requirement, RoleId = role.Id });

            await _dbContext.SaveChangesAsync();

            string word = requirement > 1 ? "weeklies" : "weekly";
            await RespondAsync($"Successfully set \"{requirement} {word}\" role to {role.Mention}!", ephemeral: true);
        }

        [SlashCommand("game", "Sets a role for a game")]
        public async Task GameRole(string? game = null, SocketRole? role = null)
        {
            if (await PermissionsFail())
            {
                return;
            }

            if (game == null && role == null)
            {
                string desc = _dbContext.Guilds
                    .Include(g => g.GameRoles)
                    .First(g => g.Id == Context.Guild.Id).GameRoles
                    .Aggregate("",
                        (current, gr) => current + $"{gr.Game} : {Context.Guild.GetRole(gr.RoleId).Mention}\n");
                var eb = new EmbedBuilder()
                    .WithTitle("Game roles")
                    .WithDescription(desc);
                await RespondAsync(embed: eb.Build(), ephemeral: true);
                return;
            }

            if (game == null)
            {
                await RespondAsync($"Game is missing!", ephemeral: true);
                return;
            }

            if (role == null)
            {
                await RespondAsync($"Role is missing!", ephemeral: true);
                return;
            }

            game = game.ToLowerInvariant();

            var guild = _dbContext.Guilds
                .Include(g => g.GameRoles)
                .First(g => g.Id == Context.Guild.Id);
            var roles = guild.GameRoles.Where(r => r.Game == game || r.RoleId == role.Id);
            foreach (var gr in roles)
            {
                guild.GameRoles.Remove(gr);
            }

            guild.GameRoles.Add(new GameRole { Game = game, RoleId = role.Id });

            await _dbContext.SaveChangesAsync();

            await RespondAsync($"Successfully set \"{game}\" role to {role.Mention}!", ephemeral: true);
        }
    }
}