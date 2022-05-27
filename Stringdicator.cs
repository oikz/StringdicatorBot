using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Stringdicator.Services;
using Victoria;

namespace Stringdicator {
    /// <summary>
    /// Main class for the Stringdicator Discord bot
    /// Initialises the bot and keeps it running
    /// </summary>
    class Stringdicator {
        private readonly DiscordSocketClient _discordClient;
        private readonly InteractionService _interactions;
        private readonly HttpClient _httpClient;

        private Stringdicator() {
            _discordClient = new DiscordSocketClient(new DiscordSocketConfig {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 250,
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers
            });
            
            _interactions = new InteractionService(_discordClient, new InteractionServiceConfig() {
                LogLevel = LogSeverity.Info
            });

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AcmeInc/1.0)");
        }

        /// <summary>
        /// Main method creates new instance and starts the bot
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void Main(string[] args)
            => new Stringdicator().MainAsync().GetAwaiter().GetResult();

        /// <summary>
        /// The main method that initialises the Bot and starts operations
        /// </summary>
        private async Task MainAsync() {
            //Load Token from env file
            var root = Directory.GetCurrentDirectory();
            var dotenv = Path.Combine(root, ".env");
            DotEnv.Load(dotenv);


            //Create the ServiceProvider for dependency injection
            var services = new ServiceCollection()
                .AddSingleton(_discordClient)
                .AddSingleton(_interactions)
                .AddSingleton(_httpClient)
                .AddSingleton<CommandHandler>()
                .AddSingleton<MusicService>()
                .AddLavaNode(x => { x.SelfDeaf = false; })
                .BuildServiceProvider();

            //Login
            await _discordClient.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("TOKEN"));
            await _discordClient.StartAsync();

            ImagePrediction.HttpClient = services.GetRequiredService<HttpClient>();

            var handler = new CommandHandler(_discordClient, _interactions, services, _httpClient);
            await handler.InstallCommandsAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
    }
}