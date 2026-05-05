using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeeklyIL.Database;

public class GuildEntity
{
    [Key]
    public ulong Id { get; set; }
    public ulong SubmissionsChannel { get; set; }
    public ulong AnnouncementsChannel { get; set; }
    public ulong ModeratorRole { get; set; }
    public ulong OrganizerRole { get; set; }
    public ISet<WeeklyRole> WeeklyRoles { get; set; } = new HashSet<WeeklyRole>();
    public ISet<GameRole> GameRoles { get; set; } = new HashSet<GameRole>();
}

public class UserEntity
{
    [Key]
    public ulong Id { get; set; }
    public uint WeeklyWins { get; set; }
}

public class WeekEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public ulong Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong? MonthId { get; set; }
    public string Level { get; set; }
    public string Game { get; set; }
    public uint StartTimestamp { get; set; }
    public bool ShowVideo { get; set; }
    public bool Ended { get; set; }
}

public class ScoreEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public ulong Id { get; set; }
    public ulong UserId { get; set; }
    public ulong WeekId { get; set; }
    public uint? TimeMs { get; set; }
    public string? Video { get; set; }
    public bool Verified { get; set; }
}

public class WeeklyRole
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public ulong Id { get; set; }
    public ulong RoleId { get; set; }
    public uint Requirement { get; set; }
}

public class GameRole
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public ulong Id { get; set; }
    public ulong RoleId { get; set; }
    public string Game { get; set; }
}