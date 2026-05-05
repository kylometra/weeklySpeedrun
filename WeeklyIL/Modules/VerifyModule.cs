using System.Globalization;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using WeeklyIL.Database;
using WeeklyIL.Services;
using WeeklyIL.Utility;

namespace WeeklyIL.Modules;

public class VerifyModule(
    IDbContextFactory<WilDbContext> contextFactory,
    DiscordSocketClient client,
    CloseSubmissionsTimers levelEnder)
    : InteractionModuleBase<SocketInteractionContext>
{
    private readonly WilDbContext _dbContext = contextFactory.CreateDbContext();

    public class VerifyModal : IModal
    {
        public string Title => "Verify run";
        
        [ModalTextInput("time")]
        public string Time { get; set; }
    }
    
    public class RejectModal : IModal
    {
        public string Title => "Reject run";
        
        [ModalTextInput("reason")]
        public string Reason { get; set; }
    }
    
    [ModalInteraction("verify_run", true)]
    public async Task VerifyRun(VerifyModal modal)
    {
        if (!TimeSpan.TryParseExact(
                modal.Time, @"m\:ss\.fff", CultureInfo.InvariantCulture,
                out var time))
        {
            if (!TimeSpan.TryParseExact(
                    modal.Time, @"h\:mm\:ss\.fff", CultureInfo.InvariantCulture,
                    out time))
            {
                return;
            }
        }

        var context = VerifyComponentInteractions.Interactions[Context.Interaction.User.Id];

        var score = _dbContext.Scores.FirstOrDefault(s => s.Id == context.ScoreId);
        if (score == null) return;

        if (score.Verified)
        {
            await RespondAsync("The run has already been verified.", ephemeral: true);
            return;
        }
        
        var week = _dbContext.Week(score.WeekId);
        if (week.GuildId != Context.Guild.Id) return;
        
        // update the score
        score.TimeMs = (uint)time.TotalMilliseconds;
        score.Verified = true;
        await _dbContext.SaveChangesAsync();

        // let discord know the interaction went through
        await DeferAsync();
        
        // try to end the week if it's waiting for verifications
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!week.Ended && week.StartTimestamp < _dbContext.Weeks
                .Where(w => w.GuildId == Context.Guild.Id)
                .Where(w => w.StartTimestamp < now)
                .OrderBy(w => w.StartTimestamp).Last().StartTimestamp) await levelEnder.TryCloseSubmissions(week);

        try
        {
            var channel =
                (SocketTextChannel)await client.GetChannelAsync(
                    _dbContext.Guild(week.GuildId).AnnouncementsChannel);

            var ts = new TimeSpan((long)score.TimeMs * TimeSpan.TicksPerMillisecond);

            string level = week.Level;
            var uri = level.GetUriFromString();
            if (uri != null)
            {
                level = level.Replace(uri.OriginalString, $"<{uri.OriginalString}>");
            }

            var bestTimes = _dbContext.Scores
                .Where(s => s.WeekId == score.WeekId && s.Verified)
                .GroupBy(s => s.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    BestTime = g.Min(s => s.TimeMs)
                });
            
            uint place = (uint)(bestTimes.Count(x => x.BestTime < score.TimeMs) + 1);

            await Context.Guild.DownloadUsersAsync();
            var user = Context.Guild.GetUser(score.UserId);

            if (place != 0) // is pb
            {
                string placeStr = place.ToString();
                if (placeStr.Length > 1 && placeStr[^2] == '1')
                {
                    placeStr += "th";
                }
                else
                {
                    placeStr += placeStr.Last() switch
                    {
                        '1' => "st",
                        '2' => "nd",
                        '3' => "rd",
                        _ => "th"
                    };
                }
                bool isCurrent = week.Id == (await _dbContext.CurrentWeek(week.GuildId))?.Id;
                placeStr = !isCurrent || week.ShowVideo ? $"[{placeStr} place PB]({score.Video})" : $"{placeStr} place PB";
                string timeStr = ts.Hours == 0 ? $@"{ts:mm\:ss\.fff}" : $@"{ts:hh\:mm\:ss}";
                await channel.SendMessageAsync($@"{user.Mention} got a {placeStr} with a time of `{timeStr}` on {level}!");
            }
                

            await user.AddRolesAsync(_dbContext.Guilds
                .Include(g => g.GameRoles)
                .First(g => g.Id == week.GuildId).GameRoles
                .Where(r => r.Game == week.Game)
                .Select(r => r.RoleId));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        await (await Context.Channel.GetMessageAsync(context.MessageId)).DeleteAsync();
    }
    
    [ModalInteraction("reject_run", true)]
    public async Task RejectRun(RejectModal modal)
    {
        var context = VerifyComponentInteractions.Interactions[Context.Interaction.User.Id];
        
        var score = _dbContext.Scores.FirstOrDefault(s => s.Id == context.ScoreId);
        if (score == null) return;
        
        var week = _dbContext.Week(score.WeekId);
        if (week.GuildId != Context.Guild.Id) return;

        // delete the score
        _dbContext.Remove(score);
        await _dbContext.SaveChangesAsync();
        
        // let discord know the interaction went through
        await DeferAsync();
        
        try
        {
            await (await client.GetUserAsync(score.UserId)).SendMessageAsync(
                $"Your run ({score.Video}) has been rejected. \nReason: {modal.Reason}");
        } catch (Exception) { /* ignored */ }
        
        // try to end the week if it's waiting for verifications
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        uint nextWeekStart = _dbContext.Weeks
            .Where(w => w.GuildId == Context.Guild.Id)
            .Where(w => w.StartTimestamp < now)
            .OrderByDescending(w => w.StartTimestamp).First().StartTimestamp;
        if (!week.Ended && week.StartTimestamp < nextWeekStart) await levelEnder.TryCloseSubmissions(week);
        
        // delete the submission message
        await (await Context.Channel.GetMessageAsync(context.MessageId)).DeleteAsync();

        // clear the interaction context
        VerifyComponentInteractions.Interactions.Remove(Context.Interaction.User.Id);
    }
}