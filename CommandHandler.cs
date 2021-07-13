using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Stringdicator {
    public class CommandHandler {
        private readonly DiscordSocketClient _discordClient;
        private readonly CommandService _commands;
        private StreamWriter logFile;


        // Retrieve client and CommandService instance via ctor
        public CommandHandler(DiscordSocketClient client, CommandService commands) {
            _commands = commands;
            _discordClient = client;
        }

        public async Task InstallCommandsAsync() {
            logFile = new StreamWriter("log.txt");
            // Hook the MessageReceived event into our command handler
            _discordClient.MessageReceived += HandleCommandAsync;
            _discordClient.MessageDeleted += HandleMessageDelete;
            _discordClient.MessageUpdated += HandleMessageUpdate;


            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                services: null);
            await _discordClient.SetGameAsync("with String!");
        }

        /**
         * Do stuff with user commands
         */
        private async Task HandleCommandAsync(SocketMessage messageParam) {
            // Don't process the command if it was a system message
            if (!(messageParam is SocketUserMessage message)) return;

            //For automatically detecting the image type and responding
            var attachments = message.Attachments;
            foreach (var attachment in attachments) {
                if (attachment == null) {
                    continue;
                }

                var extension = Path.GetExtension(attachment.Url);
                switch (extension) {
                    case null:
                        continue;
                    case ".jpg":
                    case ".png":
                        //Valid image

                        var current = Directory.GetCurrentDirectory();
                        var filename = current + "\\image" + extension;

                        using (var client = new WebClient()) {
                            client.DownloadFile(new Uri(attachment.Url), filename);
                        }

                        MakePredictionRequest(filename, messageParam).Wait();
                        GC.Collect(); //To prevent "file already in use" type errors
                        break;
                }
            }


            // Create a number to track where the prefix ends and the command begins
            int startPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix('!', ref startPos) ||
                  message.HasMentionPrefix(_discordClient.CurrentUser, ref startPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message
            SocketCommandContext context = new SocketCommandContext(_discordClient, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(
                context: context,
                argPos: startPos,
                services: null);
        }


        /**
         * Do stuff when a message is deleted
         */
        public Task HandleMessageDelete(Cacheable<IMessage, ulong> cachedMessage, ISocketMessageChannel channel) {
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
            logFile.WriteLine(
                $"{DateTime.Now}: Message from {message.Author} was removed from the channel {channel.Name}: \n"
                + message.Content);

            return Task.CompletedTask;
        }

        /**
         * Do stuff when a message is updated
         */
        public async Task HandleMessageUpdate(Cacheable<IMessage, ulong> cachedMessage, SocketMessage newMessage,
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
            await logFile.WriteLineAsync(
                $"{DateTime.Now}: Message from {message.Author} in {channel.Name} was edited from {message} -> {newMessage}");
        }
        
        /**
         * Handles the prediction of image classification when a user uploads an image
         * Mostly taken from the Microsoft Docs for Custom Vision
         */
        private async Task MakePredictionRequest(string imageFilePath, SocketMessage messageParam) {
            var client = new HttpClient();
            
            client.DefaultRequestHeaders.Add("Prediction-Key", "323fbb7c35b34af48005a8563b95333d");

            // Prediction URL - replace this example URL with your valid Prediction URL.
            const string url =
                "https://string.cognitiveservices.azure.com/customvision/v3.0/Prediction/f598b65b-19f1-48fa-a15b-097704cc5e76/classify/iterations/String%202/image";

            // Request body. Try this sample with a locally stored image.
            var byteData = GetImageAsByteArray(imageFilePath);

            using var content = new ByteArrayContent(byteData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var response = await client.PostAsync(url, content);

            var resultString = await response.Content.ReadAsStringAsync();
            
            
            //Take the response from Custom Vision and get the probability of each tag for the provided image
            //Lots of string splits to get it :/
            resultString = resultString.Split('[')[1];
            var tags = resultString.Split("},{");

            var probabilities = new Dictionary<double, string>();
            foreach (var tag in tags) {
                var probability = tag.Split( ":")[1].Split(",")[0];//Extract the probability via regex
                probabilities.Add(double.Parse(probability), tag.Split("\"")[9]);
            }
            
            //Output heighest guess
            var pair = new KeyValuePair<double, string>(0, "");
            foreach (var current in probabilities.Where(result => result.Key > pair.Key)) {
                pair = current;
                Console.WriteLine(current);
            }
            
            if (pair.Value.Equals("String") && pair.Key > 0.8) {
                var context = new SocketCommandContext(_discordClient, messageParam as SocketUserMessage);
                await context.Channel.SendMessageAsync("String!");
            }
        }

        
        /**
         * Also taken from Microsoft Docs
         * Takes the image into a byte array for sending to Custom Vision for prediction
         */
        private static byte[] GetImageAsByteArray(string imageFilePath) {
            var fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
            var binaryReader = new BinaryReader(fileStream);
            return binaryReader.ReadBytes((int) fileStream.Length);
        }


    }
}