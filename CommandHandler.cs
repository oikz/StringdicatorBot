using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Victoria;

namespace Stringdicator {
    /// <summary>
    /// CommandHandler for Stringdicator
    /// Manages all incoming events from channels - Message events etc
    /// </summary>
    public class CommandHandler {
        private readonly DiscordSocketClient _discordClient;
        private readonly CommandService _commands;
        private StreamWriter _logFile;
        private readonly ServiceProvider _services;
        private readonly LavaNode _lavaNode;


        /// <summary>
        /// Constructor for the CommandHandler to initialise the values needed to run
        /// </summary>
        /// <param name="client">This discord client object</param>
        /// <param name="commands">The CommandService to be used</param>
        /// <param name="services">The ServiceProvider to be used</param>
        public CommandHandler(DiscordSocketClient client, CommandService commands, ServiceProvider services) {
            _commands = commands;
            _discordClient = client;
            _services = services;
            _lavaNode = (LavaNode) _services.GetService(typeof(LavaNode));
        }

        /// <summary>
        /// Finish initialising the CommandHandler by adding modules to the CommandHandler and setting method calls
        /// for event handling
        /// </summary>
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

        /// <summary>
        /// React to normal Messages, usually commands
        /// Also reacts to images etc. for Image Prediction
        /// </summary>
        /// <param name="messageParam">The message that was received</param>
        private async Task HandleCommandAsync(SocketMessage messageParam) {
            // Don't process the command if it was a system message
            if (!(messageParam is SocketUserMessage message)) return;
            // Ignore messages from other bots
            if (message.Author.IsBot || message.Author.Id.Equals(_discordClient.CurrentUser.Id)) return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_discordClient, message);

            //Ignore all channels in the blacklist
            if (await ChannelInBlacklist(message)) return;

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
                if (!message.Content.StartsWith("https://tenor.com/view")) {
                    var check = await new HttpClient().GetAsync(new Uri(message.Content));
                    ImagePrediction.MakePrediction(check.RequestMessage.RequestUri + ".gif", context);
                    GC.Collect();
                    return;
                }

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


        /// <summary>
        /// Handles reacting to messages being deleted by users
        /// </summary>
        /// <param name="cachedMessage">The message that was deleted</param>
        /// <param name="channel">The channel it was located in</param>
        /// <returns>A Task</returns>
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

        /// <summary>
        /// Handle messages being updated/edited by users 
        /// </summary>
        /// <param name="cachedMessage">The original message before being edited</param>
        /// <param name="newMessage">The new message after being edited</param>
        /// <param name="channel">The channel that the message was located in</param>
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

        /// <summary>
        /// Event called when the bot is ready to receive messages
        /// Completes final LavaLink setup
        /// </summary>
        private async Task HandleReady() {
            Console.WriteLine("Stringdicator is connected!");
            if (!_lavaNode.IsConnected) {
                await _lavaNode.ConnectAsync();
            }
        }

        /// <summary>
        /// Check if a channel is in the blacklist and ignore it if so
        /// </summary>
        private async Task<bool> ChannelInBlacklist(SocketMessage message) {
            var commands = _commands.Commands.FirstOrDefault(command => command.Name.Equals("StringBlacklist"));
            //Always allow blacklist commands to work to un-blacklist a channel
            if (commands.Aliases.Any(a => a.Equals(message.Content.Replace("!", "")))) {
                return false;
            }


            //Create new empty Blacklist file
            if (!File.Exists("Blacklist.xml")) {
                var settings = new XmlWriterSettings {Async = true};
                var writer = XmlWriter.Create("Blacklist.xml", settings);
                await writer.WriteElementStringAsync(null, "Channels", null, null);
                writer.Close();
                
                //Create new empty BlacklistImages file
                if (File.Exists("BlacklistImages.xml")) return false;
                writer = XmlWriter.Create("BlacklistImages.xml", settings);
                await writer.WriteElementStringAsync(null, "Channels", null, null);
                writer.Close();
                return false;
            }
            //Load the xml file containing all the channels
            var root = XElement.Load("Blacklist.xml");
            
            //If the xml file contains this channel - is blacklisted, don't react to messages
            var address =
                from element in root.Elements("Channel")
                where element.Value == message.Channel.Id.ToString()
                select element;
            return address.Any();
        }
    }
}