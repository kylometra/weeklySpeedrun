using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Services;

public class CloseSubmissionsTimers(IDbContextFactory<WilDbContext> contextFactory, DiscordSocketClient client)
{
    private readonly Dictionary<ulong, HashSet<Timer>> _allTimers = new();

    private readonly uint[] _intervals =
    [
        86400U, // 1 day
        3600U, // 1 hour
        600U, // 10 minutes
        0U // real one
    ];

    public async Task UpdateGuildTimer(ulong id)
    {
        // i have to create a new dbcontext here to avoid cache issues
        var dbContext = await contextFactory.CreateDbContextAsync();

        var nextWeek = await dbContext.NextWeek(id);
        if (nextWeek == null) return;

        bool notnull = _allTimers.TryGetValue(id, out var timers);
        if (notnull)
        {
            foreach (var timer in timers!)
            {
                await timer.DisposeAsync();
            }
            timers.Clear();
        }
        else
        {
            timers = [];
            _allTimers.Add(id, timers);
        }

        for (int i = 0; i < _intervals.Length; i++)
        {
            uint interval = _intervals[i];
            long seconds = nextWeek.StartTimestamp - interval - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (seconds < 0) continue;
            
            var dueTime = new TimeSpan(seconds * TimeSpan.TicksPerSecond);
            timers.Add(i == _intervals.Length - 1
                ? new Timer(o => OnSubmissionsClose(o), dbContext.CurrentWeek(id), dueTime, Timeout.InfiniteTimeSpan)
                : new Timer(o => OnCountdown(o), nextWeek, dueTime, Timeout.InfiniteTimeSpan));
        }
        _allTimers[id] = timers;
    }
    
    private async Task OnCountdown(object? o)
    {
        var week = (WeekEntity)o!;
        
        long seconds = week.StartTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remaining = new TimeSpan(seconds * TimeSpan.TicksPerSecond);
        
        var dbContext = await contextFactory.CreateDbContextAsync();
        var guild = client.GetGuild(week.GuildId);
        var channel = guild.GetTextChannel(dbContext.Guild(week.GuildId).AnnouncementsChannel);

        await channel.SendMessageAsync($@"Submissions close in `{Math.Floor(remaining.TotalHours)}h {remaining:mm}m`!");
    }
    
    private async Task OnSubmissionsClose(object? o)
    {
        await Task.Delay(1000); // trying to avoid race conditions lmao

        if (o == null) return; // this is the first week in the guild, im too lazy rn to make it announce anything
        var week = (WeekEntity)o;
        
        var dbContext = await contextFactory.CreateDbContextAsync();
        var guild = client.GetGuild(week.GuildId);
        var channel = guild.GetTextChannel(dbContext.Guild(week.GuildId).AnnouncementsChannel);

        if (!await TryCloseSubmissions(week))
        {
            await channel.SendMessageAsync("Submissions closed! Results will be posted when all currently pending runs are verified.");
        }
    }

    public async Task<bool> TryCloseSubmissions(WeekEntity week)
    {
        var dbContext = await contextFactory.CreateDbContextAsync();

        await UpdateGuildTimer(week.GuildId);

        var guild = client.GetGuild(week.GuildId);
        var channel = guild.GetTextChannel(dbContext.Guild(week.GuildId).AnnouncementsChannel);

        if (dbContext.Scores.Where(s => s.WeekId == week.Id).Any(s => !s.Verified))
        {
            return false;
        }
        
        week.Ended = true;
        dbContext.Update(week);
        await dbContext.SaveChangesAsync();
        
        var eb = dbContext.LeaderboardBuilder(client, week, null, true);
        await channel.SendMessageAsync("Submissions closed! This is the leaderboard as of now:", embed: eb.Build());

        var currentWeek = await dbContext.CurrentWeek(week.GuildId);
        await channel.SendMessageAsync($"Next: {currentWeek!.Level}");

        var first = dbContext.Scores
            .Where(s => s.WeekId == week.Id)
            .Where(s => s.Verified)
            .OrderBy(s => s.TimeMs)
            .FirstOrDefault();

        if (first == null) return true; // sad

        await dbContext.CreateUserIfNotExists(first.UserId);

        var ue = dbContext.User(first.UserId);
        ue.WeeklyWins++;

        var user = guild.GetUser(first.UserId);
        var weeklyRoles = dbContext.Guilds
            .Include(g => g.WeeklyRoles)
            .First(g => g.Id == week.GuildId).WeeklyRoles;
        await user.RemoveRolesAsync(weeklyRoles.Select(r => r.RoleId));
        await user.AddRoleAsync(weeklyRoles
            .Where(r => r.Requirement <= ue.WeeklyWins)
            .MaxBy(r => r.Requirement)!.RoleId);

        await dbContext.SaveChangesAsync();
        
        return true;
    }

    public async Task DisposeAsync()
    {
        foreach (var t in _allTimers.SelectMany(kvp => kvp.Value))
        {
            await t.DisposeAsync();
        }
    }
}