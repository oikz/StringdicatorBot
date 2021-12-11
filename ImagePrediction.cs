using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace Stringdicator {
    public static class ImagePrediction {
        public static HttpClient HttpClient { get; set; }

        /// <summary>
        /// Handles setup for the image classification prediction
        /// Take a given file url/attachment and set it up for Custom Vision prediction
        /// </summary>
        /// <param name="attachmentUrl">The url of the attachment to be sent for prediction</param>
        /// <param name="context">The Context of the message, used for replying to the user</param>
        public static async void MakePrediction(string attachmentUrl, SocketCommandContext context) {
            if (await ChannelInImageBlacklist(context.Message)) {
                return;
            }

            //Trim the end of urls as some can contain extra characters after the filename
            attachmentUrl = attachmentUrl switch {
                var a when a.Contains(".jpg") => attachmentUrl.Split(".jpg")[0] + ".jpg",
                var a when a.Contains(".png") => attachmentUrl.Split(".png")[0] + ".png",
                var a when a.Contains(".jpeg") => attachmentUrl.Split(".jpeg")[0] + ".jpeg",
                var a when a.Contains(".gif") => attachmentUrl.Split(".gif")[0] + ".gif",
                _ => attachmentUrl
            };

            var extension = Path.GetExtension(attachmentUrl); //Get the extension for use later
            if (!extension.Equals(".gif") && !extension.Equals(".jpg") && !extension.Equals(".png") &&
                !extension.Equals("jpeg")) {
                return;
            }

            byte[] image;
            try {
                image = await HttpClient.GetByteArrayAsync(new Uri(attachmentUrl));
            } catch (HttpRequestException exception) {
                Console.WriteLine("Error: " + exception.Message);
                return;
            }

            //Need to load first frame as a png if its a gif
            //Get the first frame and save it as a png in the same format
            if (extension.Equals(".gif")) {
                try {
                    var gifImg = System.Drawing.Image.FromStream(new MemoryStream(image));
                    var bitmap = new Bitmap(gifImg);
                    await using var stream = new MemoryStream();
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    image = stream.ToArray();
                } catch (OutOfMemoryException) {
                    return;
                }
            }

            await MakePredictionRequest(image, context);
        }

        /// <summary>
        /// Handles the prediction of image classification when a user uploads an image
        /// Mostly taken from the Microsoft Docs for Custom Vision
        /// </summary>
        /// <param name="image">The image to be sent in bytes</param>
        /// <param name="context">The Context of the message, used for replying to the user</param>
        private static async Task MakePredictionRequest(byte[] image, SocketCommandContext context) {
            //Prediction endpoint
            const string url =
                "https://string3-prediction.cognitiveservices.azure.com/customvision/v3.0/Prediction/b0ad2694-b2da-4342-835e-26d7bf6018fc/classify/iterations/String/image";

            // Sends the image as a byte array to the endpoint to run a prediction on it
            using var content = new ByteArrayContent(image);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.Add("Prediction-Key", "1414d8884b384beba783ebba4a225082");
            var response = await HttpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode) {
                return;
            }

            //Result
            var result = JObject.Parse(await response.Content.ReadAsStringAsync());

            //Get the top prediction
            var prediction = result["predictions"].FirstOrDefault();
            if (prediction == null) {
                return;
            }

            var predictionName = prediction["tagName"].ToString();
            var predictionProbability = prediction["probability"].ToString();

            Console.WriteLine($"{predictionName} {predictionProbability}");

            if (predictionName.Equals("String") && Convert.ToInt32(predictionProbability) > 0.8) {
                await context.Message.ReplyAsync("This looks like String!");
            }
        }

        /// <summary>
        /// Checks whether the current channel is blacklisted from reacting to images with Image Classification
        /// </summary>
        /// <param name="message">The message sent</param>
        /// <returns>True if in the blacklist, false otherwise</returns>
        private static async Task<bool> ChannelInImageBlacklist(SocketMessage message) {
            //Create new empty Blacklist file
            if (!File.Exists("BlacklistImages.xml")) {
                var settings = new XmlWriterSettings { Async = true };
                var writer = XmlWriter.Create("BlacklistImages.xml", settings);
                await writer.WriteElementStringAsync(null, "Channels", null, null);
                writer.Close();
                return false;
            }

            //Load the xml file containing all the channels
            var root = XElement.Load("BlacklistImages.xml");

            //If the xml file contains this channel - is blacklisted, don't react to messages
            var address =
                from element in root.Elements("Channel")
                where element.Value == message.Channel.Id.ToString()
                select element;
            return address.Any();
        }
    }
}