using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Victoria;

namespace Stringdicator {
    class Stringdicator {
        private readonly DiscordSocketClient _discordClient;
        private readonly CommandService _commands;

        private Stringdicator() {
            _discordClient = new DiscordSocketClient(new DiscordSocketConfig {
                // How much logging do you want to see?
                LogLevel = LogSeverity.Info,

                // If you or another service needs to do anything with messages
                // (eg. checking Reactions, checking the content of edited/deleted messages),
                // you must set the MessageCacheSize. You may adjust the number as needed.
                MessageCacheSize = 250,
            });

            _commands = new CommandService(new CommandServiceConfig {
                // Again, log level:
                LogLevel = LogSeverity.Info,

                // There's a few more properties you can set,
                // for example, case-insensitive commands.
                CaseSensitiveCommands = false,
            });
        }

        public static void Main(string[] args)
            => new Stringdicator().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync() {
            //Load Token from env file
            var root = Directory.GetCurrentDirectory();
            var dotenv = Path.Combine(root, ".env");
            DotEnv.Load(dotenv);


            var services = new ServiceCollection()
                .AddSingleton(_discordClient)
                .AddSingleton(_commands)
                .AddSingleton<CommandHandler>()
                .AddLavaNode(x => { x.SelfDeaf = false; })
                .BuildServiceProvider();

            await _discordClient.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("TOKEN"));
            await _discordClient.StartAsync();


            var handler = new CommandHandler(_discordClient, _commands, services);
            await handler.InstallCommandsAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
    }
}