using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stringdicator.Database;
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
            var configFile = Path.Combine(root, ".env");
            
            if (!File.Exists(configFile)) {
                Console.WriteLine("No .env file found");
                return;
            }
            
            LoadEnvironmentVariables(configFile);


            //Create the ServiceProvider for dependency injection
            var services = new ServiceCollection()
                .AddSingleton(_discordClient)
                .AddSingleton(_interactions)
                .AddSingleton(_httpClient)
                .AddSingleton<CommandHandler>()
                .AddSingleton<MusicService>()
                .AddLavaNode(x => { x.SelfDeaf = false; })
                .AddSingleton<ApplicationContext>()
                .BuildServiceProvider();

            //Login
            await _discordClient.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("TOKEN"));
            await _discordClient.StartAsync();

            ImagePrediction.HttpClient = services.GetRequiredService<HttpClient>();
            ImagePrediction.ApplicationContext = services.GetRequiredService<ApplicationContext>();

            await services.GetRequiredService<ApplicationContext>().Database.MigrateAsync();

            var handler = new CommandHandler(_discordClient, _interactions, services, _httpClient);
            await handler.InstallCommandsAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        /// <summary>
        /// Load values from the file and set them as Environment Variables
        /// </summary>
        /// <param name="filePath">The path of the file to be read</param>
        private static void LoadEnvironmentVariables(string filePath) {
            foreach (var line in File.ReadAllLines(filePath)) {
                var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2) {
                    continue;
                }

                Environment.SetEnvironmentVariable(parts[0], parts[1]);
            }
        }
    }
}