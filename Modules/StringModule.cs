using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GoogleApi;
using GoogleApi.Entities.Search.Common;
using GoogleApi.Entities.Search.Image.Request;

namespace Stringdicator.Modules {

    /// <summary>
    /// Module containing the String Image modules
    /// </summary>
    [Summary("String Commands")]
    public class StringModule : ModuleBase<SocketCommandContext> {
        /// <summary>
        /// String! - Googles "ball of string" and returns one of the top 190 images
        /// </summary>
        [Command("String")]
        [Summary("Finds a string image")]
        [Alias("S")]
        private async Task StringAsync() {
            //Setup and send search request
            var random = new Random(); //Nice
            var startIndex = random.Next(0, 190);

            //Pick a random search result
            var items = await ImageSearch("Ball of String", startIndex);
            var index = random.Next(0, items.Length);
            var item = items[index];

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("String!");
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            //Send message
            await Context.Channel.SendMessageAsync("", false, builder.Build());
            Console.WriteLine("String! - " + item.Link + " " + (startIndex + index));
        }


        /// <summary>
        /// Google Image Searches for a given search term and sends an embedded message containing a random search result
        /// </summary>
        /// <param name="searchTerm">The string term to be searched</param>
        [Command("Stringsearch")]
        [Summary("Finds a searched image")]
        [Alias("SSR")]
        private async Task StringSearchAsync([Remainder] string searchTerm) {
            var items = await ImageSearch(searchTerm);
            var random = new Random();
            var item = items[random.Next(0, items.Length)];

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("Stringsearch!");
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            //Send message
            await Context.Channel.SendMessageAsync("", false, builder.Build());
            Console.WriteLine("Stringsearch! - " + searchTerm + " " + item.Link);

            //Do an image classification prediction for stringsearch images as well
            ImagePrediction.MakePrediction(item.Link, Context);
            GC.Collect();
            //Call the prediction method
        }

        /// <summary>
        /// Google Image Searches for a given search term and sends an spoilered message containing a random search result
        /// </summary>
        /// <param name="searchTerm">The string term to be searched</param>
        [Command("StringsearchSpoiler")]
        [Summary("Finds a searched image")]
        [Alias("SSRS")]
        private async Task StringSearchSpoilerAsync([Remainder] string searchTerm) {
            var items = await ImageSearch(searchTerm);
            var random = new Random();
            var item = items[random.Next(0, items.Length)];

            //Best solution to sending a spoilered image seems to just be to download it and send it
            const string fileName = "image.png";
            using (var client = new WebClient()) {
                try {
                    await client.DownloadFileTaskAsync(new Uri(item.Link), fileName);
                } catch (WebException exception) {
                    Console.WriteLine("Error: " + exception.Message);
                    return;
                }
            }

            await Context.Channel.SendFileAsync(fileName, isSpoiler: true);

            //Do an image classification prediction for stringsearch images as well
            ImagePrediction.MakePrediction(item.Link, Context);
            GC.Collect();
            //Call the prediction method
        }

        private static async Task<Item[]> ImageSearch(string searchTerm, int startIndex = 0) {
            //Setup and send search request
            var request = new ImageSearchRequest {
                Query = searchTerm,
                Key = Environment.GetEnvironmentVariable("API_KEY"),
                SearchEngineId = Environment.GetEnvironmentVariable("SEARCH_ENGINE_ID"),
                Options = new SearchOptions {
                    StartIndex = startIndex
                }
            };

            //Gets the search response - contains info about search
            var response =
                await GoogleSearch.ImageSearch.QueryAsync(request);

            //Pick a random search result
            return response.Items.ToArray();
        }
    }
}