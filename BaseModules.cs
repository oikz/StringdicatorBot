using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GoogleApi;
using GoogleApi.Entities.Search;
using GoogleApi.Entities.Search.Image.Request;

namespace Stringdicator {
    /**
     * Basic help module that outputs available commands to users
     */
    public class HelpModule : ModuleBase<SocketCommandContext> {
        [Command("Stringdicator")]
        [Summary("Outputs all commands")]
        public async Task HelpAsync() {
            //Kinda jank but gets all the commands again
            var commands = new CommandService();
            await commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                services: null);

            //Embed builder
            var builder = new EmbedBuilder();
            builder.WithTitle("All Stringdicator Commands");
            builder.WithThumbnailUrl("attachment://string.jpg");
            builder.WithColor(3447003);
            builder.WithDescription("");

            foreach (var command in commands.Commands) {
                //builder.Description += command.Name + ": " + command.Summary + "\n";
                var fieldBuilder = new EmbedFieldBuilder {Name = command.Name, Value = command.Summary};
                builder.AddField(fieldBuilder);
            }

            await ReplyAsync("", false, builder.Build());
        }
    }


    /**
     * String! - Googles "ball of string" and returns one of the top 190 images
     */
    public class StringModule : ModuleBase<SocketCommandContext> {
        [Command("String")]
        [Summary("Finds a string image")]
        public async Task StringAsync() {
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
    }

    /**
     * StringSearch - Googles the search term and returns a random result from the top 10 images
     */
    public class StringSearchModule : ModuleBase<SocketCommandContext> {
        [Command("Stringsearch")]
        [Summary("Finds a searched image")]
        public async Task StringAsync([Remainder] string searchTerm) {
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
            CommandHandler.MakePrediction(item.Link, Context);
            GC.Collect();
            //Call the prediction method
        }
    }
}