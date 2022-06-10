using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Stringdicator.Database;
using Stringdicator.Modules;
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
        private readonly ApplicationContext _applicationContext;


        /// <summary>
        /// Constructor for the CommandHandler to initialise the values needed to run
        /// </summary>
        /// <param name="services">The ServiceProvider to be used</param>
        public CommandHandler(IServiceProvider services) {
            _services = services;
            _discordClient = _services.GetRequiredService<DiscordSocketClient>();
            _interactions = _services.GetRequiredService<InteractionService>();
            _lavaNode = _services.GetRequiredService<LavaNode>();
            _httpClient = _services.GetRequiredService<HttpClient>();
            _applicationContext = _services.GetRequiredService<ApplicationContext>();
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
            _discordClient.ReactionAdded += HandleReactionAdded;
            _discordClient.ReactionRemoved += HandleReactionRemoved;
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
            if (messageParam is not SocketUserMessage message) return;
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
                return;
            }

            if (message.Content.StartsWith("https://tenor.com") || message.Content.StartsWith("https://media.discordapp.net")) {
                if (!message.Content.StartsWith("https://tenor.com/view")) {
                    var check = await _httpClient.GetAsync(new Uri(message.Content));
                    ImagePrediction.MakePrediction(check.RequestMessage?.RequestUri + ".gif", context.Channel, context.User);
                    return;
                }

                ImagePrediction.MakePrediction(message.Content + ".gif", context.Channel, context.User);
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
            
            // Ignore deleted messages from other bots
            if (cachedMessage.Value.Author.IsBot || cachedMessage.Value.Author.Id.Equals(_discordClient.CurrentUser.Id)) {
                return Task.CompletedTask;
            }


            var message = cachedMessage.Value;
            Console.WriteLine(
                $"{DateTime.Now}: Message from {message.Author.Username}#{message.Author.DiscriminatorValue} was removed from the channel {channel.Value.Name}: \n"
                + message.Content);
            _logFile.WriteLine(
                $"{DateTime.Now}: Message from {message.Author.Username}#{message.Author.DiscriminatorValue} was removed from the channel {channel.Value.Name}: \n"
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

            var message = cachedMessage.Value;
            
            // Ignore deleted messages from other bots
            if (cachedMessage.Value.Author.IsBot || cachedMessage.Value.Author.Id.Equals(_discordClient.CurrentUser.Id)) {
                return;
            }

            // Ignore messages if they are the same thing
            if (message.Content.Equals(newMessage.Content)) {
                return;
            }

            Console.WriteLine(
                $"{DateTime.Now}: Message from {message.Author.Username}#{message.Author.DiscriminatorValue} in {channel.Name} was edited from {message} -> {newMessage}");
            await _logFile.WriteLineAsync(
                $"{DateTime.Now}: Message from {message.Author.Username}#{message.Author.DiscriminatorValue} in {channel.Name} was edited from {message} -> {newMessage}");
        }


        /// <summary>
        /// Handle events that should occur when a reaction is added to a message
        /// </summary>
        /// <param name="cachedMessage">The message reacted to</param>
        /// <param name="channel">The channel it was sent in</param>
        /// <param name="reaction">The reaction that was sent</param>
        private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage,
            Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction) {
            if (cachedMessage.Value.Content.StartsWith("This looks like Anime")) {
                await CheckAnimeViolation(cachedMessage, channel, reaction);
            } else if (reaction.Emote.Equals(new Emoji("🦍"))) {
                await CheckGorilla(cachedMessage);
            }
        }
        
        /// <summary>
        /// Handle events that should occur when a reaction is removed from a message
        /// /// </summary>
        /// <param name="cachedMessage">The message that the reaction was removed from</param>
        /// <param name="channel">The channel it was sent in</param>
        /// <param name="reaction">The reaction that was removed</param>
        private async Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> cachedMessage,
            Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction) {
            
            // Don't count bots in the total.
            var users = await cachedMessage.Value.GetReactionUsersAsync(new Emoji("🦍"), 100).FlattenAsync();
            if (users.Count(e => !e.IsBot) == 1 && reaction.Emote.Equals(new Emoji("🦍"))) {
                var userId = cachedMessage.Value.Author.Id;
                await ExtraModule.UpdateGorilla(userId, false);
            }
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

            try {
                await _interactions.RegisterCommandsToGuildAsync(
                    Convert.ToUInt64(Environment.GetEnvironmentVariable("DEV_GUILD_ID")));
            } catch (Exception) {
                //Don't create guild commands
            }

            await _interactions.RegisterCommandsGloballyAsync();
        }

        /// <summary>
        /// Check if a channel is in the blacklist and ignore it if so
        /// </summary>
        private async Task<bool> ChannelInBlacklist(SocketInteraction interaction) {
            if ((interaction as SocketSlashCommand)?.CommandName == "blacklist") {
                return false;
            }

            var channelObject = await _applicationContext.Channels.FindAsync(interaction.Channel.Id);
            return channelObject is not null && channelObject.Blacklisted;
        }

        /// <summary>
        /// Check the bots response to a potential anime violation message for reactions and act based on them.
        /// If there are 3 upvotes, then record a violation, if there are 3 downvotes, delete the message
        /// Otherwise, do nothing
        /// </summary>
        /// <param name="cachedMessage">The message</param>
        /// <param name="channel">The channel it was sent in</param>
        /// <param name="reaction">The reaction that was just added</param>
        private async Task CheckAnimeViolation(Cacheable<IUserMessage, ulong> cachedMessage,
            Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction) {
            //Only want reactions to the bot message
            if (!cachedMessage.Value.Author.Username.Equals(_discordClient.CurrentUser.Username)) {
                return;
            }
            
            //Is correct message
            if (reaction.User.Value.Username.Equals(_discordClient.CurrentUser.Username)) {
                return;
            }
            
            var thumbsUp = await cachedMessage.Value.GetReactionUsersAsync(new Emoji("\U0001F44D"), 100).FlattenAsync();
            var thumbsDown = await cachedMessage.Value.GetReactionUsersAsync(new Emoji("\U0001F44D"), 100).FlattenAsync();
            if (thumbsUp.Count(e => !e.IsBot) != 3 && thumbsDown.Count(e => !e.IsBot) != 3) {
                return;
            }
            

            //Is thumbs up react and not made by stringdicator
            if (reaction.Emote.Equals(new Emoji("\U0001F44D"))) {
                await cachedMessage.Value.RemoveAllReactionsForEmoteAsync(new Emoji("\U0001F44D"));

                var context = new SocketCommandContext(_discordClient,
                    cachedMessage.Value as SocketUserMessage
                );
                var mention = cachedMessage.Value.Content.Split("- ")[1];
                foreach (var user in context.Guild.Users) {
                    if (!user.Mention.Equals(mention)) continue;
                    var builder = await ExtraModule.NoAnime(context.Guild, user);
                    await context.Channel.SendMessageAsync(embed: builder.Build());
                }
                
                
                // Thumbs down react and not made by stringdicator    
            } else if (reaction.Emote.Equals(new Emoji("\U0001F44E"))) {
                await channel.Value.DeleteMessageAsync(cachedMessage.Value);
            }
        }

        /// <summary>
        /// Check if the message has received 3 Gorilla reacts and if so, add one Gorilla Moment to the user.
        /// Doesn't count bots in the total.
        /// </summary>
        /// <param name="cachedMessage">The message</param>
        private static async Task CheckGorilla(Cacheable<IUserMessage, ulong> cachedMessage) {
            var users = await cachedMessage.Value.GetReactionUsersAsync(new Emoji("🦍"), 100).FlattenAsync();
            if (users.Count(e => !e.IsBot) >= 2) {
                var userId = cachedMessage.Value.Author.Id;
                await ExtraModule.UpdateGorilla(userId, true);
            }
        }
    }
}