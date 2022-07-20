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
using Stringdicator.Services;

namespace Stringdicator.Modules {
    /// <summary>
    /// Module containing the String Image modules
    /// </summary>
    [Discord.Commands.Summary("String Commands")]
    public class StringModule : InteractionModuleBase<SocketInteractionContext> {
        private readonly HttpClient _httpClient;
        private static ImageSearchCacheService _imageCache;

        public StringModule(HttpClient httpClient, ImageSearchCacheService imageCache) {
            _httpClient = httpClient;
            _imageCache = imageCache;
        }

        /// <summary>
        /// String! - Googles "ball of string" and returns one of the top 190 images
        /// </summary>
        [SlashCommand("string", "Finds a string image")]
        private async Task StringAsync() {
            //Setup and send search request
            var random = new Random(); //Nice
            var startIndex = random.Next(0, 190);

            //Pick a random search result
            var items = await _imageCache.GetOrCreate(this, "Ball of String", startIndex);
            if (items.Length == 0) return;

            var index = random.Next(0, items.Length);
            var item = items[index];

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("String!");
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            //Send message
            await FollowupAsync(embed: builder.Build());
            Console.WriteLine($"{DateTime.Now}: String! - " + item.Link + " " + (startIndex + index));
        }


        /// <summary>
        /// Google Image Searches for a given search term and sends an embedded message containing a random search result
        /// </summary>
        /// <param name="searchTerm">The string term to be searched</param>
        [SlashCommand("search", "Finds a searched image")]
        private async Task StringSearchAsync([Summary("search-term", "The search query to find and image for")] string searchTerm) {
            var items = await _imageCache.GetOrCreate(this, searchTerm, 1);

            if (items.Length == 0) return;
            var item = items[0];

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("Stringsearch! - " + searchTerm);
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);
            builder.WithFooter("Image number 1 of page 1");

            var buttons = new ComponentBuilder()
                .WithButton(customId: "search-previous", label: "Previous")
                .WithButton(customId: "search-next", label: "Next")
                .WithButton(customId: "search-delete", label: "Delete");

            //Send message
            await FollowupAsync(embed: builder.Build(), components: buttons.Build());
            
            Console.WriteLine($"{DateTime.Now}: Stringsearch! - " + searchTerm + " " + item.Link);

            //Do an image classification prediction for stringsearch images as well
            await ImagePrediction.MakePrediction(item.Link, Context.Channel, Context.Interaction.User);
        }

        /// <summary>
        /// Google Image Searches for a given search term and sends an spoilered message containing a random search result
        /// </summary>
        /// <param name="searchTerm">The string term to be searched</param>
        [SlashCommand("spoiler", "Finds a searched image and spoilers it")]
        private async Task StringSearchSpoilerAsync([Summary("search-term", "The search query to find and image for")] string searchTerm) {
            var items = await _imageCache.GetOrCreate(this, searchTerm, 1);

            if (items.Length == 0) return;
            var item = items[0];

            byte[] image;
            try {
                image = await _httpClient.GetByteArrayAsync(new Uri(item.Link));
            } catch (HttpRequestException exception) {
                Console.WriteLine("Error: " + exception.Message);
                return;
            }
            
            await FollowupAsync("String!");
            await Context.Channel.SendFileAsync(new MemoryStream(image), "SPOILER_image.png");

            //Do an image classification prediction for stringsearch images as well
            await ImagePrediction.MakePrediction(item.Link, Context.Channel, Context.Interaction.User);
        }

        /// <summary>
        /// Base Image search method that returns a list of search results
        /// </summary>
        /// <param name="searchTerm">The search query</param>
        /// <param name="startIndex">At what index to start searching from</param>
        /// <returns>A list of search results</returns>
        public async Task<Item[]> ImageSearch(string searchTerm, int startIndex = 0) {
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
                if (exception.Message.Contains("Rate Limit Exceeded")) {
                    await FollowupAsync("Error: Rate Limit Exceeded");
                }
                await FollowupAsync($"An error occurred searching for \"{searchTerm}\"");
                return Array.Empty<Item>();
            }

            //No results
            if (response.Items is not null) return response.Items.ToArray();
            await FollowupAsync($"No results found for \"{searchTerm}\"");
            return Array.Empty<Item>();
        }
        
        
        /// <summary>
        /// A re-roll button for the stringsearch command to get a new image
        /// </summary>
        [ComponentInteraction("search-previous")]
        public async Task PreviousImage() {
            var searchTerm = ((SocketMessageComponent)Context.Interaction).Message.Embeds.ElementAt(0).Title
                .Replace("Stringsearch! - ", "");
            
            var index = ((SocketMessageComponent)Context.Interaction).Message.Embeds.ElementAt(0).Footer?.Text
                .Split("Image number ")[1].Split(" of page ")[0];
            
            var startIndex = ((SocketMessageComponent)Context.Interaction).Message.Embeds.ElementAt(0).Footer?.Text
                .Split("Image number ")[1].Split(" of page ")[1];
            
            if (!int.TryParse(startIndex, out var startIndexInt)) startIndexInt = 0;
            if (!int.TryParse(index, out var indexInt)) indexInt = 0;

            if (indexInt == 1 && startIndexInt == 1) {
                await RespondAsync("You are on the first page!", ephemeral: true);
                return;
            }


            // Move to previous Image
            if (indexInt == 1) {
                indexInt = 11;
                startIndexInt--;
            }
            indexInt--;
            // Move to next page if reached the end of the page

            var items = await _imageCache.GetOrCreate(this, searchTerm, (startIndexInt - 1) * 10 + 1);
            
            var item = items[indexInt - 1];
            
            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle(((SocketMessageComponent)Context.Interaction).Message.Embeds.ElementAt(0).Title);
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);
            builder.WithFooter($"Image number {indexInt} of page {startIndexInt}");
            
            await ((SocketMessageComponent)Context.Interaction).ModifyOriginalResponseAsync(x => {
                x.Embed = builder.Build();
            });
        }
        
        /// <summary>
        /// A re-roll button for the stringsearch command to get a new image
        /// </summary>
        [ComponentInteraction("search-next")]
        public async Task NextImage() {
            var searchTerm = ((SocketMessageComponent)Context.Interaction).Message.Embeds.ElementAt(0).Title
                .Replace("Stringsearch! - ", "");
            
            var index = ((SocketMessageComponent)Context.Interaction).Message.Embeds.ElementAt(0).Footer?.Text
                .Split("Image number ")[1].Split(" of page ")[0];
            
            var startIndex = ((SocketMessageComponent)Context.Interaction).Message.Embeds.ElementAt(0).Footer?.Text
                .Split("Image number ")[1].Split(" of page ")[1];
            
            if (!int.TryParse(startIndex, out var startIndexInt)) startIndexInt = 0;
            if (!int.TryParse(index, out var indexInt)) indexInt = 0;

            // Move to next Image
            
            if (indexInt == 10) indexInt = 0;
            var nextIndex = indexInt;
            // Move to next page if reached the end of the page
            if (nextIndex == 0) startIndexInt++;

            var items = await _imageCache.GetOrCreate(this, searchTerm, (startIndexInt - 1) * 10 + 1);
            
            var item = items[nextIndex];
            
            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle(((SocketMessageComponent)Context.Interaction).Message.Embeds.ElementAt(0).Title);
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);
            builder.WithFooter($"Image number {indexInt + 1} of page {startIndexInt}");
            
            await ((SocketMessageComponent)Context.Interaction).ModifyOriginalResponseAsync(x => {
                x.Embed = builder.Build();
            });
        }

        
        /// <summary>
        /// Delete the Image Search if the searcher clicks the delete button
        /// </summary>
        [ComponentInteraction("search-delete")]
        public async Task Delete() {
            var clicker = Context.Interaction.User;
            var searcher = ((SocketMessageComponent)Context.Interaction).Message.Interaction.User;
            if (clicker.Id != searcher.Id) {
                await RespondAsync("Only the searcher can delete this image search", ephemeral: true);
                return;
            }

            await ((SocketMessageComponent)Context.Interaction).Message.DeleteAsync();
        }

        /// <summary>
        /// Simple wrapper around the DeferAsync method to allow the cache to Defer responses.
        /// Fixes a strange issue around Unknown Webhooks on the search-next ModifyOriginalResponseAsync when using
        /// DeferAsync within the commands themselves. 
        /// </summary>
        public Task DeferAsync() {
            return base.DeferAsync();
        }
    }
}