using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GoogleApi;
using GoogleApi.Entities.Search;
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
            if (items.Length == 0) return;

            var index = random.Next(0, items.Length);
            var item = items[index];

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("String!");
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            var buttons = new ComponentBuilder().WithButton(customId: "string-reroll", label: "Search Again");
            
            //Send message
            await FollowupAsync(embed: builder.Build(), components: buttons.Build());
            Console.WriteLine("String! - " + item.Link + " " + (startIndex + index));
        }


        /// <summary>
        /// Google Image Searches for a given search term and sends an embedded message containing a random search result
        /// </summary>
        /// <param name="searchTerm">The string term to be searched</param>
        [SlashCommand("search", "Finds a searched image")]
        private async Task StringSearchAsync([Summary("search-term", "The search query to find and image for")] string searchTerm) {
            var items = await ImageSearch(searchTerm);
            if (items.Length == 0) return;

            var random = new Random();
            var item = items[random.Next(0, items.Length)];

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("Stringsearch! - " + searchTerm);
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            var buttons = new ComponentBuilder().WithButton(customId: "string-reroll", label: "Search Again");

            //Send message
            await FollowupAsync(embed: builder.Build(), components: buttons.Build());
            
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
        [SlashCommand("spoiler", "Finds a searched image and spoilers it")]
        private async Task StringSearchSpoilerAsync([Summary("search-term", "The search query to find and image for")] string searchTerm) {
            var items = await ImageSearch(searchTerm);
            if (items.Length == 0) return;
            
            var random = new Random();
            var item = items[random.Next(0, items.Length)];

            byte[] image;
            try {
                image = await HttpClient.GetByteArrayAsync(new Uri(item.Link));
            } catch (HttpRequestException exception) {
                Console.WriteLine("Error: " + exception.Message);
                return;
            }
            
            await FollowupAsync("String!");
            await Context.Channel.SendFileAsync(new MemoryStream(image), "SPOILER_image.png");

            //Do an image classification prediction for stringsearch images as well
            ImagePrediction.MakePrediction(item.Link, Context.Channel, Context.Interaction.User);
            GC.Collect();
            //Call the prediction method
        }

        /// <summary>
        /// Base Image search method that returns a list of search results
        /// </summary>
        /// <param name="searchTerm">The search query</param>
        /// <param name="startIndex">At what index to start searching from</param>
        /// <returns>A list of search results</returns>
        private async Task<Item[]> ImageSearch(string searchTerm, int startIndex = 0) {
            await DeferAsync();
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
            BaseSearchResponse response;
            try {
                response = await GoogleSearch.ImageSearch.QueryAsync(request);
            } catch (Exception exception) {
                Console.WriteLine("Error: " + exception.Message);
                await FollowupAsync($"An error occurred searching for \"{searchTerm}\" \n" +
                                    "Perhaps the Google Search quota has been reached?");
                return Array.Empty<Item>();
            }

            //No results
            if (response.Items is not null) return response.Items.ToArray();
            await FollowupAsync($"No results found for \"{searchTerm}\"");
            return Array.Empty<Item>();

            //Pick a random search result
        }
        
        /// <summary>
        /// A re-roll button for the stringsearch command to get a new image
        /// </summary>
        [ComponentInteraction("string-reroll")]
        public async Task ReRoll() {
            var searchTerm = ((SocketMessageComponent)Context.Interaction).Message.Embeds.ElementAt(0).Title
                .Replace("Stringsearch! -", "");
            var items = await ImageSearch(searchTerm);
            var random = new Random();
            var item = items[random.Next(0, items.Length)];
            
            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("Stringsearch! -" + searchTerm);
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);
            
            await ((SocketMessageComponent)Context.Interaction).ModifyOriginalResponseAsync(x => {
                x.Embed = builder.Build();
            });
        }
    }
}