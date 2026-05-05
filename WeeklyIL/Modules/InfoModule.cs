using Discord.Interactions;

namespace WeeklyIL.Modules;

public class InfoModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("info", "Shows info about the bot")]
    public async Task Info() =>
        await RespondAsync(
            "# :information_source: Information\n\n" +
            
            "## Every month we choose a new level/category to run\n\n" +
            
            "- View the current level's leaderboard with `/leaderboard`\n" +
            "- Submit your times with `/submit video`\n" +
            "- Want to submit but can't record a video? Submit with `/submit blank`\n" +
            "- Get a runner role for the relevant game when your run is verified!\n" +
            "- Get a counting \"IL Wins\" role for being #1 when the leaderboard closes!\n\n" +
            
            "- See previous levels with `/past`\n" +
            "- Add a level ID parameter to any relevant command to make it apply to the specified level\n" +
            
            "- Use `/stats` to see your stats\n" +
            "  - Add a user parameter to see the mentioned user's stats", ephemeral: true);
}