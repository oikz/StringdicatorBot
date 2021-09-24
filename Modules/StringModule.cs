using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GoogleApi;
using GoogleApi.Entities.Search;
using GoogleApi.Entities.Search.Image.Request;

namespace Stringdicator.Modules {

    /// <summary>
    /// Module containing the String Image modules
    /// </summary>
    [Summary("String Commands")]
    public class StringModules : ModuleBase<SocketCommandContext> {
        /// <summary>
        /// String! - Googles "ball of string" and returns one of the top 190 images
        /// </summary>
        [Command("String")]
        [Summary("Finds a string image")]
        [Alias("S")]
        private async Task StringAsync() {
            //Setup and send search request
            var request = new ImageSearchRequest();
            var random = new Random(); //Nice
            request.Options.StartIndex =
                random.Next(0, 190); //Max limit may change and result in 400 Bad Request responses
            request.Query = "Ball of String";
            request.Key = Environment.GetEnvironmentVariable("API_KEY");
            request.SearchEngineId = Environment.GetEnvironmentVariable("SEARCH_ENGINE_ID");

            //Gets the search response - contains info about search
            BaseSearchResponse response;
            try {
                response =
                    await GoogleSearch.ImageSearch.QueryAsync(request);
            } catch (Exception e) {
                Console.WriteLine("Error: " + e.Message);
                return;
            }

            //Pick a random search result
            var items = response.Items.ToArray();
            var index = random.Next(0, items.Length);
            var item = items[index];

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("String!");
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            //Send message
            await Context.Channel.SendMessageAsync("", false, builder.Build());
            Console.WriteLine("String! - " + item.Link + " " + (response.Query.StartIndex + index));
        }


        /// <summary>
        /// Google Image Searches for a given search term and sends an embedded message containing a random search result
        /// </summary>
        /// <param name="searchTerm">The string term to be searched</param>
        [Command("Stringsearch")]
        [Summary("Finds a searched image")]
        [Alias("SSR")]
        private async Task StringSearchAsync([Remainder] string searchTerm) {
            //Setup and send search request
            var request = new ImageSearchRequest {
                Query = searchTerm,
                Key = Environment.GetEnvironmentVariable("API_KEY"),
                SearchEngineId = Environment.GetEnvironmentVariable("SEARCH_ENGINE_ID")
            };

            //Gets the search response - contains info about search
            var response =
                await GoogleSearch.ImageSearch.QueryAsync(request);

            //Pick a random search result
            var items = response.Items.ToArray();
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
    }
}