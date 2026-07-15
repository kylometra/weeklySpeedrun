using Microsoft.EntityFrameworkCore;

namespace WeeklyIL.Database;

public class WilDbContext : DbContext
{
    public DbSet<GuildEntity> Guilds { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<WeekEntity> Weeks { get; set; }
    public DbSet<ScoreEntity> Scores { get; set; }
    
    public string DbPath { get; }
    
    public WilDbContext()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
        DbPath = Path.Join(path, "WilBot/WilBot.db");
        
        if (!File.Exists(DbPath)) Console.WriteLine($"DB doesn't exist at \"{DbPath}\" yet!");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}

