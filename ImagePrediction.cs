using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Stringdicator {
    public static class ImagePrediction {
        /**
         * Handles setup for the image classification prediction 
         * Take a given file url/attachment and set it up for Custom Vision prediction
         */
        public static void MakePrediction(string attachmentUrl, SocketCommandContext context) {
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

            client.DefaultRequestHeaders.Add("Prediction-Key", "1414d8884b384beba783ebba4a225082");

            //Prediction endpoint
            const string url =
                "https://string3-prediction.cognitiveservices.azure.com/customvision/v3.0/Prediction/b0ad2694-b2da-4342-835e-26d7bf6018fc/classify/iterations/String/image";

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
                await context.Message.ReplyAsync("This looks like String!");
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