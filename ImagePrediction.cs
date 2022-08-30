using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Stringdicator.Database;

namespace Stringdicator {
    public static class ImagePrediction {
        public static HttpClient HttpClient { get; set; }
        public static ApplicationContext ApplicationContext { get; set; }

        /// <summary>
        /// Handles setup for the image classification prediction
        /// Take a given file url/attachment and set it up for Custom Vision prediction
        /// </summary>
        /// <param name="attachmentUrl">The url of the attachment to be sent for prediction</param>
        /// <param name="channel">The channel of the message, used for replying to the user</param>
        /// <param name="author">The author of the original message</param>
        public static async Task MakePrediction(string attachmentUrl, ISocketMessageChannel channel, IUser author) {
            if (await ChannelInImageBlacklist(channel)) {
                return;
            }

            //Trim the end of urls as some can contain extra characters after the filename
            attachmentUrl = attachmentUrl switch {
                var a when a.Contains(".jpg") => attachmentUrl.Split(".jpg")[0] + ".jpg",
                var a when a.Contains(".png") => attachmentUrl.Split(".png")[0] + ".png",
                var a when a.Contains(".jpeg") => attachmentUrl.Split(".jpeg")[0] + ".jpeg",
                var a when a.Contains(".gif") => attachmentUrl.Split(".gif")[0] + ".gif",
                var a when a.Contains("?format=jpg") => attachmentUrl.Split("?format=jpg")[0] + ".jpg", // Special Case for Twitter Images
                _ => attachmentUrl
            };

            var extension = Path.GetExtension(attachmentUrl); //Get the extension for use later
            if (!extension.Equals(".gif") && !extension.Equals(".jpg") && !extension.Equals(".png") &&
                !extension.Equals("jpeg")) {
                Console.WriteLine($"{DateTime.Now}: Image Classification was not successful. Invalid file type");
                return;
            }

            byte[] image;
            try {
                image = await HttpClient.GetByteArrayAsync(new Uri(attachmentUrl));
            } catch (HttpRequestException exception) {
                Console.WriteLine($"{DateTime.Now}: Image Classification was not successful. " + exception.Message);
                return;
            }

            //Need to load first frame as a png if its a gif
            //Get the first frame and save it as a png in the same format
            if (extension.Equals(".gif")) {
                try {
                    var gifImg = SixLabors.ImageSharp.Image.Load(image);
                    await using var stream = new MemoryStream();
                    await gifImg.SaveAsPngAsync(stream);
                    image = stream.ToArray();
                } catch (Exception exception) {
                    Console.WriteLine($"{DateTime.Now}: Image Classification was not successful. " + exception.Message);
                    return;
                }
            }
            
            // Check the image size is within the limits and resize if necessary
            image = await ResizeImage(image);
            if (image.Length > 4000000) {
                Console.WriteLine($"{DateTime.Now}: Image is too large to be classified.");
                return;
            }
            
            // Check aspect ratio is lower than 25:1
            var img = SixLabors.ImageSharp.Image.Load(image);
            if (img.Width / img.Height > 25) {
                Console.WriteLine($"{DateTime.Now}: Image aspect ratio is too wide to be classified.");
                return;
            }

            await MakePredictionRequest(image, channel, author);
        }

        /// <summary>
        /// Handles the prediction of image classification when a user uploads an image
        /// Mostly taken from the Microsoft Docs for Custom Vision
        /// </summary>
        /// <param name="image">The image to be sent in bytes</param>
        /// <param name="channel">The channel of the message, used for replying to the user</param>
        /// <param name="author">The author of the original message</param>
        private static async Task MakePredictionRequest(byte[] image, ISocketMessageChannel channel, IUser author) {
            //Prediction endpoint
            const string url =
                "https://string3-prediction.cognitiveservices.azure.com/customvision/v3.0/Prediction/fc50bffa-e84d-4043-b691-58c1e27a35d7/classify/iterations/NoAnime7/image";

            // Sends the image as a byte array to the endpoint to run a prediction on it
            using var content = new ByteArrayContent(image);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.Add("Prediction-Key", "1414d8884b384beba783ebba4a225082");
            var response = await HttpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode) {
                Console.WriteLine($"{DateTime.Now}: Image Classification request was not successful. Error code {response.StatusCode}.");
                return;
            }

            //Result
            var result = JObject.Parse(await response.Content.ReadAsStringAsync());

            //Get the top prediction
            var prediction = result["predictions"]?.FirstOrDefault();
            if (prediction == null) {
                Console.WriteLine($"{DateTime.Now}: Image Classification request was not successful. Bad response.");
                return;
            }

            var predictionName = prediction["tagName"]?.ToString();
            var predictionProbability = prediction["probability"]?.ToString();

            Console.WriteLine($"{DateTime.Now}: Image from {author.Username}#{author.DiscriminatorValue} - {predictionName} - {predictionProbability}");

            //Message response
            if (predictionName is "Anime" && Convert.ToDouble(predictionProbability) > 0.8) {
                var newMessage =
                    await channel.SendMessageAsync("This looks like Anime - " + author.Mention);
                await newMessage.AddReactionAsync(new Emoji("\U0001F44D")); //Thumbs up react
                await newMessage.AddReactionAsync(new Emoji("\U0001F44E")); //Thumbs down react
            }
        }

        /// <summary>
        /// Checks whether the current channel is blacklisted from reacting to images with Image Classification
        /// </summary>
        /// <param name="channel">The channel the message was sent in</param>
        /// <returns>True if in the blacklist, false otherwise</returns>
        private static async Task<bool> ChannelInImageBlacklist(ISocketMessageChannel channel) {
            var channelObject = await ApplicationContext.Channels.FindAsync(channel.Id);
            return channelObject is not null && channelObject.ImageBlacklisted;
        }

        /// <summary>
        /// Check an Image's filesize in bytes and attempt to resize down to 1080p and then 720p if the file is larger
        /// than the maximum 4MB allowed by Custom Vision.
        /// </summary>
        /// <param name="img">The image in bytes</param>
        /// <returns>The same image if it is small enough or a resized image in bytes</returns>
        private static async Task<byte[]> ResizeImage(byte[] img) {
            if (img.Length <= 4000000) return img;
            
            //Resize to 1920x1080
            var image = SixLabors.ImageSharp.Image.Load(img);
            image.Mutate(x => x.Resize(1920, 1080));
            await using var stream = new MemoryStream();
            await image.SaveAsPngAsync(stream);
            img = stream.ToArray();
            
            if (img.Length <= 4000000) return img;
            
            // Resize to 1280x720
            image.Mutate(x => x.Resize(1280, 720));
            await image.SaveAsPngAsync(stream);
            img = stream.ToArray();
            return img;
        }
    }
}