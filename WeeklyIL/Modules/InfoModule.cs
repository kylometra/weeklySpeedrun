using Discord.Interactions;

namespace WeeklyIL.Modules;

public class InfoModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("info", "Shows info about the bot")]
    public async Task Info() =>
        await RespondAsync(
            "# :information_source: Information\n\n" +
            
            "## Every month we choose a new level/category to run\n\n" +
            
            "- Submit your recorded times with `/submit video`\n" +
            "- Want to submit but can't record a video? Submit with `/submit blank`\n" +
            "- View the current level's leaderboard with `/lb`\n" +
            "- See previous levels with `/past`\n" +
            "- Use `/stats` to see info about your submissions\n\n" +
            
            "- Get a runner role for the relevant game when your run is verified!\n" +
            "- Get a counting \"IL Wins\" role for being #1 when the leaderboard closes!", ephemeral: true);
}