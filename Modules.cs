using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

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
        }
    }

}