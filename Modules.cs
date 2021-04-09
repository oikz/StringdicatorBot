using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GoogleApi;
using GoogleApi.Entities.Search;
using GoogleApi.Entities.Search.Common;
using GoogleApi.Entities.Search.Image.Request;

namespace Stringdicator {
    public class HelpModule : ModuleBase<SocketCommandContext> {
        [Command("Stringdicator")]
        [Summary("Outputs all commands")]
        public async Task HelpAsync() {
            
            //Kinda jank but gets all the commands again
            CommandService _commands = new CommandService();
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                services: null);
            
            //Embed builder
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle("All Stringdicator Commands");
            builder.WithThumbnailUrl("attachment://string.jpg");
            builder.WithColor(3447003);
            builder.WithDescription("");

            foreach (CommandInfo command in _commands.Commands) {
                //builder.Description += command.Name + ": " + command.Summary + "\n";
                var fieldBuilder = new EmbedFieldBuilder();
                fieldBuilder.Name = command.Name;
                fieldBuilder.Value = command.Summary;
                builder.AddField(fieldBuilder);
            }
            await ReplyAsync("", false, builder.Build());
        }
    }


    //Take the users message, delete it and then replace it with Stringdicators own message of the same content
    public class SayModule : ModuleBase<SocketCommandContext> {
        // !say hello world -> hello world
        [Command("Say")]
        [Summary("Deletes and then echoes a message.")]
        public async Task SayAsync([Remainder] [Summary("The text to echo")]
            string echo) {
            //Delete the previous message and then send the requested message afterwards
            var messages = Context.Channel.GetMessagesAsync(1).Flatten();
            await foreach (var message in messages) {
                await Context.Channel.DeleteMessageAsync(message, RequestOptions.Default);
                Console.WriteLine(message.Author);
            }

            await Context.Channel.SendMessageAsync(echo);
        }
    }

    public class StringModule : ModuleBase<SocketCommandContext> {
        [Command("String")]
        [Summary("Finds a string image")]
        public async Task StringAsync() {
            //Setup and send search request
            ImageSearchRequest request = new ImageSearchRequest();
            Random random = new Random(); //Nice
            request.Query = "Ball of String";
            request.Options.LowRange = random.Next(1, 100000);
            request.Key = Environment.GetEnvironmentVariable("API_KEY");
            request.SearchEngineId = Environment.GetEnvironmentVariable("SEARCH_ENGINE_ID");

            //Gets the search response - contains info about search
            BaseSearchResponse response =
                await GoogleSearch.ImageSearch.QueryAsync(request);


            //Pick a random search result
            var items = response.Items.ToArray();
            var item = items[random.Next(0, items.Length)];

            //Create an embed using that image url
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle("String!");
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            //Send message
            await Context.Channel.SendMessageAsync("", false, builder.Build());
            Console.WriteLine("String! - " + item.Link);
        }
    }

    public class StringSearchModule : ModuleBase<SocketCommandContext> {
        [Command("Stringsearch")]
        [Summary("Finds a searched image")]
        public async Task StringAsync([Remainder] string searchterm) {
            //Setup and send search request
            ImageSearchRequest request = new ImageSearchRequest();
            request.Query = searchterm;
            request.Key = Environment.GetEnvironmentVariable("API_KEY");
            request.SearchEngineId = Environment.GetEnvironmentVariable("SEARCH_ENGINE_ID");

            //Gets the search response - contains info about search
            BaseSearchResponse response =
                await GoogleSearch.ImageSearch.QueryAsync(request);

            //Pick a random search result
            var items = response.Items.ToArray();
            Random random = new Random();
            var item = items[random.Next(0, items.Length)];

            //Create an embed using that image url
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle("Stringsearch!");
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            //Send message
            await Context.Channel.SendMessageAsync("", false, builder.Build());
            Console.WriteLine("Stringsearch! - " + searchterm + " " + item.Link);
        }
    }
}