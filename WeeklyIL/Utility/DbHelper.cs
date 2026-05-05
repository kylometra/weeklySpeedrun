using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;

namespace WeeklyIL.Utility;

public static class DbHelper
{
    public static async Task CreateGuildIfNotExists(this WilDbContext dbContext, ulong id)
    {
        if (dbContext.Guilds.Any(g => g.Id == id))
        {
            return;
        }

        await dbContext.Guilds.AddAsync(new GuildEntity
        {
            Id = id
        });
        
        await dbContext.SaveChangesAsync();
    }
    
    public static async Task CreateUserIfNotExists(this WilDbContext dbContext, ulong id)
    {
        if (dbContext.Users.Any(g => g.Id == id))
        {
            return;
        }

        await dbContext.Users.AddAsync(new UserEntity
        {
            Id = id
        });
        
        await dbContext.SaveChangesAsync();
    }
    
    public static async Task<WeekEntity?> CurrentWeek(this WilDbContext dbContext, ulong guild)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return await dbContext.Weeks
            .Where(w => w.GuildId == guild)
            .Where(w => w.StartTimestamp < now)
            .OrderByDescending(w => w.StartTimestamp)
            .FirstOrDefaultAsync();
    }

    public static async Task<WeekEntity?> NextWeek(this WilDbContext dbContext, ulong guild)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return await dbContext.Weeks
            .Where(w => w.GuildId == guild)
            .Where(w => w.StartTimestamp > now)
            .OrderBy(w => w.StartTimestamp)
            .FirstOrDefaultAsync();
    }

    public static async Task<bool> UserIsOrganizer(this WilDbContext dbContext, SocketInteractionContext context)
    {
        await dbContext.CreateGuildIfNotExists(context.Guild.Id);
        
        var g = dbContext.Guild(context.Guild.Id);
        
        return context.User is SocketGuildUser user && user.Roles.Any(r =>
            r.Id == g.OrganizerRole || r.Id == g.ModeratorRole
        );
    }

    public static GuildEntity Guild(this WilDbContext dbContext, ulong id)
    {
        return dbContext.Guilds.Find(id)!;
    }
    
    public static WeekEntity Week(this WilDbContext dbContext, ulong id)
    {
        return dbContext.Weeks.Find(id)!;
    }
    
    public static UserEntity User(this WilDbContext dbContext, ulong id)
    {
        return dbContext.Users.Find(id)!;
    }
    
    public static EmbedBuilder LeaderboardBuilder(this WilDbContext dbContext, DiscordSocketClient client, WeekEntity week, WeekEntity? nextWeek, bool forceVideo, bool showObsolete = false)
    {
        string board = string.Empty;
        int place = 1;
        var scores = dbContext.Scores
            .Where(s => s.WeekId == week.Id)
            .Where(s => s.Verified);
        if (!showObsolete)
        {
            scores = scores
                .GroupBy(s => s.UserId)
                .Select(g => g.OrderBy(s => s.TimeMs).First());
        }

        client.GetGuild(week.GuildId).DownloadUsersAsync().Wait();
        
        foreach (var score in scores.AsEnumerable().OrderBy(s => s.TimeMs))
        {
            var u = client.GetUser(score.UserId);
            string name = u == null ? "unknown" : u.Username;
            
            if (score.Video == null)
            {
                board += $":heavy_multiplication_x: `{place:D2}` - `??:??.???` - {name}\n";
                place++;
                continue;
            }
            
            board += place switch
            {
                1 => ":first_place:",
                2 => ":second_place:",
                3 => ":third_place:",
                _ => ":checkered_flag:"
            };
            var ts = new TimeSpan((long)score.TimeMs! * TimeSpan.TicksPerMillisecond);
            string timeStr = ts.Hours == 0 ? $@"{ts:mm\:ss\.fff}" : $@"{ts:hh\:mm\:ss}.";
            board += $@" `{place:D2}` - `{timeStr}` - ";
            board += forceVideo || week.ShowVideo ? $"[{name}]({score.Video})" : name;
            if (showObsolete) board += $" : {score.Id}";
            board += "\n";
            
            place++;
        }

        var eb = new EmbedBuilder()
            .WithDescription(board)
            .WithFooter($"ID: {week.Id}");

        var uri = week.Level.GetUriFromString();
        if (uri == null)
        {
            eb.WithAuthor(week.Level);
        }
        else
        {
            eb.WithAuthor(week.Level, url: uri.OriginalString);
        }

        if (nextWeek == null) return eb;
        
        var remaining = new TimeSpan((nextWeek.StartTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) * TimeSpan.TicksPerSecond);
        eb.WithTitle($"{remaining.Days}d{remaining:hh}h{remaining:mm}m remaining");

        return eb;
    }
}