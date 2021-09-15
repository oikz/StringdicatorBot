using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Victoria;

namespace Stringdicator {
    public class CommandHandler {
        private readonly DiscordSocketClient _discordClient;
        private readonly CommandService _commands;
        private StreamWriter _logFile;
        private readonly ServiceProvider _services;
        private readonly LavaNode _lavaNode;


        // Retrieve client and CommandService instance via ctor
        public CommandHandler(DiscordSocketClient client, CommandService commands, ServiceProvider services) {
            _commands = commands;
            _discordClient = client;
            _services = services;
            _lavaNode = (LavaNode) _services.GetService(typeof(LavaNode));
        }

        public async Task InstallCommandsAsync() {
            _logFile = new StreamWriter("log.txt");
            // Hook the MessageReceived event into our command handler
            _discordClient.MessageReceived += HandleCommandAsync;
            _discordClient.MessageDeleted += HandleMessageDelete;
            _discordClient.MessageUpdated += HandleMessageUpdate;
            _discordClient.Ready += HandleReady;

            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(),
                _services);
            await _discordClient.SetGameAsync("with String!");
        }

        /**
         * Do stuff with user commands
         */
        private async Task HandleCommandAsync(SocketMessage messageParam) {
            // Don't process the command if it was a system message
            if (!(messageParam is SocketUserMessage message)) return;
            // Ignore messages from other bots
            if (message.Author.IsBot && message.Author.Id.Equals(_discordClient.CurrentUser.Id)) return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_discordClient, message);


            //Check each attachment posted by the user and if its an image (checked inside MakePrediction(), do a prediction
            var attachments = message.Attachments;
            foreach (var attachment in attachments) {
                if (attachment == null) {
                    continue;
                }

                ImagePrediction.MakePrediction(attachment.Url, context);
                GC.Collect(); //To fix file in use errors
                return;
            }

            if (message.Content.StartsWith("https://tenor.com")) {
                ImagePrediction.MakePrediction(message.Content + ".gif", context);
                GC.Collect();
                return;
            }


            // Create a number to track where the prefix ends and the command begins
            var startPos = 0;

            // Determine if the message is a command based on the prefix
            if (!(message.HasCharPrefix('!', ref startPos) ||
                  message.HasMentionPrefix(_discordClient.CurrentUser, ref startPos)))
                return;

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(
                context,
                startPos,
                _services);
        }


        /**
         * Do stuff when a message is deleted
         */
        private Task HandleMessageDelete(Cacheable<IMessage, ulong> cachedMessage, ISocketMessageChannel channel) {
            // check if the message exists in cache; if not, we cannot report what was removed
            if (!cachedMessage.HasValue) {
                return Task.CompletedTask;
            }

            // Ignore !say deleted messages
            if (cachedMessage.Value.Content.Contains("!say")) {
                return Task.CompletedTask;
            }


            var message = cachedMessage.Value;
            Console.WriteLine(
                $"Message from {message.Author} was removed from the channel {channel.Name}: \n"
                + message.Content);
            _logFile.WriteLine(
                $"{DateTime.Now}: Message from {message.Author} was removed from the channel {channel.Name}: \n"
                + message.Content);

            return Task.CompletedTask;
        }

        /**
         * Do stuff when a message is updated
         */
        private async Task HandleMessageUpdate(Cacheable<IMessage, ulong> cachedMessage, SocketMessage newMessage,
            ISocketMessageChannel channel) {
            // check if the message exists in cache; if not, we cannot report what was removed
            if (!cachedMessage.HasValue) {
                return;
            }

            var message = await cachedMessage.GetOrDownloadAsync();


            //Don't show stuff edited by bot - Embeds etc
            if (message.Author.Username.Equals(_discordClient.CurrentUser.Username)) {
                return;
            }

            Console.WriteLine(
                $"Message from {message.Author} in {channel.Name} was edited from {message} -> {newMessage}");
            await _logFile.WriteLineAsync(
                $"{DateTime.Now}: Message from {message.Author} in {channel.Name} was edited from {message} -> {newMessage}");
        }

        private async Task HandleReady() {
            Console.WriteLine("Stringdicator is connected!");
            if (!_lavaNode.IsConnected) {
                await _lavaNode.ConnectAsync();
            }
        }
    }
}