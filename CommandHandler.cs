using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
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

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_discordClient, message);


            //Check each attachment posted by the user and if its an image (checked inside MakePrediction(), do a prediction
            var attachments = message.Attachments;
            foreach (var attachment in attachments) {
                if (attachment == null) {
                    continue;
                }

                MakePrediction(attachment.Url, context);
                GC.Collect(); //To fix file in use errors
                return;
            }

            if (message.Content.StartsWith("https://tenor.com")) {
                MakePrediction(message.Content + ".gif", context);
                GC.Collect();
                return;
            }


            // Create a number to track where the prefix ends and the command begins
            var startPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix('!', ref startPos) ||
                  message.HasMentionPrefix(_discordClient.CurrentUser, ref startPos)) ||
                message.Author.IsBot)
                return;

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
            logFile.WriteLine(
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
            await logFile.WriteLineAsync(
                $"{DateTime.Now}: Message from {message.Author} in {channel.Name} was edited from {message} -> {newMessage}");
        }

        /**
         * Handles setup for the image classification prediction 
         * Take a given file url/attachment and set it up for Custom Vision prediction
         */
        public static void MakePrediction(string attachmentUrl, SocketCommandContext context) {
            var extension = Path.GetExtension(attachmentUrl); //Get the extension for use later
            if (!extension.Equals(".gif") && !extension.Equals(".jpg") && !extension.Equals(".png")) {
                return;
            }

            var current = Directory.GetCurrentDirectory();
            var filename = current + "\\image" + extension;

            //Download the image for easier stuff
            using (var client = new WebClient()) {
                client.DownloadFile(new Uri(attachmentUrl), filename);
            }

            //Need to load first frame as a png if its a gif
            //Get the first frame and save it as a png in the same format
            if (extension.Equals(".gif")) {
                var gifImg = System.Drawing.Image.FromFile(filename);
                var bmp = new Bitmap(gifImg);
                filename = filename.Replace(".gif", ".png");
                bmp.Save(filename);
            }
            
            MakePredictionRequest(filename, context).Wait();
        }

        /**
         * Handles the prediction of image classification when a user uploads an image
         * Mostly taken from the Microsoft Docs for Custom Vision
         */
        private static async Task MakePredictionRequest(string imageFilePath, SocketCommandContext context) {
            var client = new HttpClient();

            client.DefaultRequestHeaders.Add("Prediction-Key", "323fbb7c35b34af48005a8563b95333d");

            //Prediction endpoint
            const string url =
                "https://string.cognitiveservices.azure.com/customvision/v3.0/Prediction/f598b65b-19f1-48fa-a15b-097704cc5e76/classify/iterations/String%202/image";

            // Sends the image as a byte array to the endpoint to run a prediction on it
            var byteData = GetImageAsByteArray(imageFilePath);
            using var content = new ByteArrayContent(byteData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var response = await client.PostAsync(url, content);

            //Result
            var resultString = await response.Content.ReadAsStringAsync();


            //Take the response from Custom Vision and get the probability of each tag for the provided image
            //Lots of string splits to get it :/
            resultString = resultString.Split('[')[1];
            var tags = resultString.Split("},{");

            //Sort tag prediction probabilities into a dictionary
            var probabilities = new Dictionary<double, string>();
            foreach (var tag in tags) {
                var probability = tag.Split(":")[1].Split(",")[0]; //Extract the probability via regex
                probabilities.Add(double.Parse(probability), tag.Split("\"")[9]);
            }

            //Output highest guess
            var pair = new KeyValuePair<double, string>(0, "");
            foreach (var current in probabilities.Where(result => result.Key > pair.Key)) {
                pair = current;
            }
            Console.WriteLine(pair);

            if (pair.Value.Equals("String") && pair.Key > 0.8) {
                Console.WriteLine("String detected!");
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