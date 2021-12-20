using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GoogleApi;
using GoogleApi.Entities.Search.Common;
using GoogleApi.Entities.Search.Image.Request;

namespace Stringdicator.Modules {
    /// <summary>
    /// Module containing the String Image modules
    /// </summary>
    [Discord.Commands.Summary("String Commands")]
    public class StringModule : InteractionModuleBase<SocketInteractionContext> {
        public HttpClient HttpClient { get; set; }
        public DiscordSocketClient DiscordClient { get; set; }

        /// <summary>
        /// String! - Googles "ball of string" and returns one of the top 190 images
        /// </summary>
        [SlashCommand("string", "Finds a string image")]
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
            await RespondAsync(embed: builder.Build());
            Console.WriteLine("String! - " + item.Link + " " + (startIndex + index));
        }


        /// <summary>
        /// Google Image Searches for a given search term and sends an embedded message containing a random search result
        /// </summary>
        /// <param name="searchTerm">The string term to be searched</param>
        [SlashCommand("stringsearch", "Finds a searched image")]
        private async Task StringSearchAsync([Discord.Interactions.Summary("search-term", "The search query to find and image for")] string searchTerm) {
            var items = await ImageSearch(searchTerm);
            var random = new Random();
            var item = items[random.Next(0, items.Length)];

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("Stringsearch!");
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            //Send message
            await RespondAsync(embed: builder.Build());
            Console.WriteLine("Stringsearch! - " + searchTerm + " " + item.Link);

            //Do an image classification prediction for stringsearch images as well
            ImagePrediction.MakePrediction(item.Link, Context.Channel, Context.Interaction.User);
            GC.Collect();
            //Call the prediction method
        }

        /// <summary>
        /// Google Image Searches for a given search term and sends an spoilered message containing a random search result
        /// </summary>
        /// <param name="searchTerm">The string term to be searched</param>
        [SlashCommand("stringsearchspoiler", "Finds a searched image and spoilers it")]
        private async Task StringSearchSpoilerAsync([Discord.Interactions.Summary("search-term", "The search query to find and image for")] string searchTerm) {
            var items = await ImageSearch(searchTerm);
            var random = new Random();
            var item = items[random.Next(0, items.Length)];

            byte[] image;
            try {
                image = await HttpClient.GetByteArrayAsync(new Uri(item.Link));
            } catch (HttpRequestException exception) {
                Console.WriteLine("Error: " + exception.Message);
                return;
            }
            
            await RespondAsync("String!");
            await Context.Channel.SendFileAsync(new MemoryStream(image), "SPOILER_image.png");

            //Do an image classification prediction for stringsearch images as well
            ImagePrediction.MakePrediction(item.Link, Context.Channel, Context.Interaction.User);
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