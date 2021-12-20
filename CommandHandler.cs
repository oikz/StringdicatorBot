﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Discord;
using Discord.Commands;
using Discord.Interactions;
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
        private readonly InteractionService _interactions;
        private StreamWriter _logFile;
        private readonly IServiceProvider _services;
        private readonly LavaNode _lavaNode;
        private readonly HttpClient _httpClient;


        /// <summary>
        /// Constructor for the CommandHandler to initialise the values needed to run
        /// </summary>
        /// <param name="client">This discord client object</param>
        /// <param name="interactions">The InteractionService to be used</param>
        /// <param name="services">The ServiceProvider to be used</param>
        /// <param name="httpClient">The HTTPClient to be used</param>
        public CommandHandler(DiscordSocketClient client, InteractionService interactions, IServiceProvider services,
            HttpClient httpClient) {
            _discordClient = client;
            _services = services;
            _interactions = interactions;
            _lavaNode = _services.GetRequiredService<LavaNode>();
            _httpClient = httpClient;
        }

        /// <summary>
        /// Finish initialising the CommandHandler by adding modules to the CommandHandler and setting method calls
        /// for event handling
        /// </summary>
        public async Task InstallCommandsAsync() {
            _logFile = new StreamWriter("log.txt");
            // Hook the MessageReceived event into our command handler
            _discordClient.MessageReceived += HandleCommandAsync;
            _discordClient.InteractionCreated += HandleInteraction;
            _discordClient.MessageDeleted += HandleMessageDelete;
            _discordClient.MessageUpdated += HandleMessageUpdate;
            _discordClient.Ready += HandleReady;
            
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(),
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
            

            //Check each attachment posted by the user and if its an image (checked inside MakePrediction(), do a prediction
            var attachments = message.Attachments;
            foreach (var attachment in attachments) {
                if (attachment == null) {
                    continue;
                }

                ImagePrediction.MakePrediction(attachment.Url, context.Channel, context.User);
                GC.Collect(); //To fix file in use errors
                return;
            }

            if (message.Content.StartsWith("https://tenor.com")) {
                if (!message.Content.StartsWith("https://tenor.com/view")) {
                    var check = await _httpClient.GetAsync(new Uri(message.Content));
                    ImagePrediction.MakePrediction(check.RequestMessage?.RequestUri + ".gif", context.Channel, context.User);
                    GC.Collect();
                    return;
                }

                ImagePrediction.MakePrediction(message.Content + ".gif", context.Channel, context.User);
                GC.Collect();
            }
        }

        private async Task HandleInteraction(SocketInteraction interaction) {
            //Ignore all channels in the blacklist
            if (await ChannelInBlacklist(interaction)) {
                await interaction.RespondAsync("This channel is blacklisted", ephemeral: true);
                return;
            }
            
            var context = new SocketInteractionContext(_discordClient, interaction);
            await _interactions.ExecuteCommandAsync(context, _services);
        }


        /// <summary>
        /// Handles reacting to messages being deleted by users
        /// </summary>
        /// <param name="cachedMessage">The message that was deleted</param>
        /// <param name="channel">The channel it was located in</param>
        /// <returns>A Task</returns>
        private Task HandleMessageDelete(Cacheable<IMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> channel) {
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
                $"Message from {message.Author} was removed from the channel {channel.Value.Name}: \n"
                + message.Content);
            _logFile.WriteLine(
                $"{DateTime.Now}: Message from {message.Author} was removed from the channel {channel.Value.Name}: \n"
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
            
            await _interactions.RegisterCommandsToGuildAsync(Convert.ToUInt64(Environment.GetEnvironmentVariable("DEV_GUILD_ID")));
        }

        /// <summary>
        /// Check if a channel is in the blacklist and ignore it if so
        /// </summary>
        private async Task<bool> ChannelInBlacklist(SocketInteraction interaction) {
            if ((interaction as SocketSlashCommand)?.CommandName == "blacklist") {
                return false;
            }
            
            //Create new empty Blacklist file
            if (!File.Exists("Blacklist.xml")) {
                var settings = new XmlWriterSettings { Async = true };
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
                where element.Value == interaction.Channel.Id.ToString()
                select element;
            return address.Any();
        }
    }
}