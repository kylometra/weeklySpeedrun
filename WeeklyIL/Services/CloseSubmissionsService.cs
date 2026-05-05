using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using WeeklyIL.Database;

namespace WeeklyIL.Services;

public class CloseSubmissionsService(IDbContextFactory<WilDbContext> contextFactory, CloseSubmissionsTimers timers, DiscordSocketClient client) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        client.Ready += Ready;
    }

    private async Task Ready()
    {
        var context = await contextFactory.CreateDbContextAsync();
        foreach (var guild in context.Guilds)
        {
            // this was a failed attempt to go back and end weeks if the bot was down when they were supposed to end
            // hopefully it won't be an issue anyway (foreshadowing)
            
            /*var weeks = context.Weeks
                .Where(w => w.GuildId == guild.Id).AsEnumerable()
                .OrderBy(w => w.StartTimestamp).ToList();
            for (int i = 0; i < weeks.Count - 1; i++)
            {
                WeekEntity week = weeks[i];
                if (week.Ended) continue;

                WeekEntity nextWeek = weeks[i + 1];

                if (nextWeek.StartTimestamp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    await timers.TryEndWeek(week);
                }
            }*/
            await timers.UpdateGuildTimer(guild.Id);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await timers.DisposeAsync();
    }
}