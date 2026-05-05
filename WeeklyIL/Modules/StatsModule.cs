using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public class StatsModule(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();

    [SlashCommand("stats", "Get stats for a user")]
    public async Task GetStats(SocketGuildUser? user = null)
    {
        user ??= client.GetGuild(Context.Guild.Id).GetUser(Context.User.Id);
        
        await _dbContext.CreateGuildIfNotExists(Context.Guild.Id);
        await _dbContext.CreateUserIfNotExists(user.Id);

        var ue = _dbContext.User(user.Id);

        var guildWeeks = _dbContext.Weeks
            .Where(w => w.GuildId == Context.Guild.Id)
            .Select(w => w.Id);

        var submissions = _dbContext.Scores
            .Where(s => guildWeeks.Contains(s.WeekId))
            .Where(s => s.UserId == user.Id);
        
        uint totalTime = (uint)(submissions
            .Where(s => s.Video != null)
            .Where(s => s.Verified)
            .Sum(s => s.TimeMs) ?? 0);

        var ts = new TimeSpan(totalTime * TimeSpan.TicksPerMillisecond);
        
        string desc = $"Total submissions: `{submissions.Count()}`\n" + 
                      $"Total run time: `{ts:d\\:hh\\:mm\\:ss\\.fff}`\n" + 
                      $"Wins: `{ue.WeeklyWins}`";

        var eb = new EmbedBuilder()
            .WithTitle($"{user.Username}'s stats")
            .WithDescription(desc)
            .WithColor(user.Roles.Where(r => r.Color != default).MaxBy(r => r.Position)!.Color);

        await RespondAsync(embed: eb.Build());
    }
    
    
}