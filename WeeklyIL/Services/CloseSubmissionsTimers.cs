using Discord;
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
            foreach (var timer in timers!) await timer.DisposeAsync();
            timers.Clear();
        }
        else
        {
            timers = [];
            _allTimers.Add(id, timers);
        }
        
        if (dbContext.Guild(id).ProxyFor != 0) return;

        for (int i = 0; i < _intervals.Length; i++)
        {
            uint interval = _intervals[i];
            long seconds = nextWeek.StartTimestamp - interval - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (seconds < 0) continue;
            
            var dueTime = new TimeSpan(seconds * TimeSpan.TicksPerSecond);
            timers.Add(i == _intervals.Length - 1
                ? new Timer(o => _ = OnSubmissionsClose(o), await dbContext.CurrentWeek(id), dueTime, Timeout.InfiniteTimeSpan)
                : new Timer(o => _ = OnCountdown(o), nextWeek, dueTime, Timeout.InfiniteTimeSpan));
        }
        _allTimers[id] = timers;
    }

    private async Task AnnounceToProxyGuilds(ulong of, string text, Embed embed = null!)
    {
        var dbContext = await contextFactory.CreateDbContextAsync();
        
        foreach (var ge in dbContext.Guilds.Where(g => g.Id == of || g.ProxyFor == of))
        {
            var guild = client.GetGuild(ge.Id);
            var channel = guild.GetTextChannel(dbContext.Guild(guild.Id).AnnouncementsChannel);
            await channel.SendMessageAsync(text, embed: embed);
        }
    }
    
    private async Task OnCountdown(object? o)
    {
        var week = (WeekEntity)o!;
        
        long seconds = week.StartTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remaining = new TimeSpan(seconds * TimeSpan.TicksPerSecond);

        await AnnounceToProxyGuilds(week.GuildId, $@"Submissions close in `{Math.Floor(remaining.TotalHours)}h {remaining:mm}m`!");
    }
    
    private async Task OnSubmissionsClose(object? o)
    {
        await Task.Delay(1000); // trying to avoid race conditions lmao

        if (o == null) return; // this is the first week in the guild, im too lazy rn to make it announce anything
        var week = (WeekEntity)o;
        await TryCloseSubmissions(week);
    }

    public async Task<bool> TryCloseSubmissions(WeekEntity week)
    {
        var dbContext = await contextFactory.CreateDbContextAsync();

        await UpdateGuildTimer(week.GuildId);

        if (dbContext.Scores.Where(s => s.WeekId == week.Id).Any(s => !s.Verified))
        {
            await AnnounceToProxyGuilds(week.GuildId, "Submissions closed! Results will be posted when all currently pending runs are verified.");
            return false;
        }
        
        week.Ended = true;
        dbContext.Update(week);
        await dbContext.SaveChangesAsync();
        
        var eb = dbContext.LeaderboardBuilder(week, null, true);
        await AnnounceToProxyGuilds(week.GuildId, "Submissions closed! This is the leaderboard as of now:", embed: eb.Build());

        var currentWeek = await dbContext.CurrentWeek(week.GuildId);
        await AnnounceToProxyGuilds(week.GuildId, $"Next: {currentWeek!.Level}");

        var first = dbContext.Scores
            .Where(s => s.WeekId == week.Id)
            .Where(s => s.Verified)
            .OrderBy(s => s.TimeMs)
            .FirstOrDefault();

        if (first == null) return true; // sad

        var ue = dbContext.User(first.UserId);
        ue.WeeklyWins++;

        var weeklyRoles = dbContext.Guilds
            .Include(g => g.WeeklyRoles)
            .First(g => g.Id == week.GuildId).WeeklyRoles;
        
        foreach (var role in weeklyRoles) await client.Rest.RemoveRoleAsync(week.GuildId, first.UserId, role.RoleId);
        await client.Rest.AddRoleAsync(week.GuildId, first.UserId, weeklyRoles
            .Where(r => r.Requirement <= ue.WeeklyWins)
            .MaxBy(r => r.Requirement)!.RoleId);

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task DisposeAsync()
    {
        foreach (var t in _allTimers.SelectMany(kvp => kvp.Value)) await t.DisposeAsync();
    }
}