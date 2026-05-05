using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeeklyIL.Database;
using WeeklyIL.Services;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        config.AddYamlFile("config.yml", false); // add config from yaml
    })
    .ConfigureServices(services => // add services
    {
        services.AddDbContextFactory<WilDbContext>();
        services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers
        }));
        services.AddSingleton<InteractionService>();
        services.AddSingleton<CloseSubmissionsTimers>();
        services.AddHostedService<CloseSubmissionsService>();
        services.AddHostedService<InteractionHandlingService>();
        services.AddHostedService<DiscordStartupService>();
    })
    .Build();

await host.RunAsync();