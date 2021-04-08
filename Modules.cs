using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GoogleApi;
using GoogleApi.Entities.Search;
using GoogleApi.Entities.Search.Image.Request;

namespace Stringdicator {
    
    //Take the users message, delete it and then replace it with Stringdicators own message of the same content
    public class SayModule : ModuleBase<SocketCommandContext> {
        // !say hello world -> hello world
        [Command("say")]
        [Summary("Delets and then echoes a message.")]
        public async Task SayAsync([Remainder] [Summary("The text to echo")]
            string echo) {
            
            //Delete the previous message and then send the requested message afterwards
            var messages = Context.Channel.GetMessagesAsync(1).Flatten();
            await foreach (var hoge in messages) {
                await Context.Channel.DeleteMessageAsync(hoge, RequestOptions.Default);
            }
            
            await Context.Channel.SendMessageAsync(echo);
        }
    }

    public class StringModule : ModuleBase<SocketCommandContext> {
        [Command("string")]
        [Summary("Finds a string message")]
        public async Task StringAsync() {
            //Setup and send search request
            ImageSearchRequest request = new ImageSearchRequest();
            request.Query = "Ball of String";
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
            builder.WithTitle("String!");
            builder.WithImageUrl(item.Link);

            //Send messahe
            await Context.Channel.SendMessageAsync("", false, builder.Build());
        }
    }

}