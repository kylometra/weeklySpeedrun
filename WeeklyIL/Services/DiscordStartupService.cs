using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WeeklyIL.Database;
using WeeklyIL.Utility;

namespace WeeklyIL.Services;

public class DiscordStartupService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly WilDbContext _dbContext;
    private readonly IConfiguration _config;
    private readonly ILogger<DiscordSocketClient> _logger;

    public DiscordStartupService(DiscordSocketClient client, IDbContextFactory<WilDbContext> contextFactory, IConfiguration config, ILogger<DiscordSocketClient> logger)
    {
        _client = client;
        _dbContext = contextFactory.CreateDbContext();
        _config = config;
        _logger = logger;

        _client.Log += msg => LogHelper.OnLogAsync(_logger, msg);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _client.LoginAsync(TokenType.Bot, _config["token"]);
        await _client.StartAsync();
        await _client.SetGameAsync("LittleBigPlanet\u2122");
        
        foreach (var user in _dbContext.Users)
        {
            var u = await _client.Rest.GetUserAsync(user.Id);
            user.Username = u?.Username ?? "unknown";
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.LogoutAsync();
        await _client.StopAsync();
    }
}