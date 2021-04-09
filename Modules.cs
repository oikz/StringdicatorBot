using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
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
                Console.WriteLine(message.Author + " Said " + message);
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
            request.Options.StartIndex = random.Next(0, 190); //Max limit may change and result in 400 Bad Request responses
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
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle("String!");
            builder.WithImageUrl(item.Link);
            builder.WithColor(3447003);

            //Send message
            await Context.Channel.SendMessageAsync("", false, builder.Build());
            Console.WriteLine("String! - " + item.Link + " " + (response.Query.StartIndex + index));
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

    
    /**
     * Base class that holds the audio stuffs
     */
    public class AudioAssistModule : ModuleBase<SocketCommandContext> {
        private Process CreateStream(string path) {
            return Process.Start(new ProcessStartInfo {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }

        /**
         * Joins the users channel and plays the specified url, then disconnects
         */
        public async Task VoiceAsync(string url) {
            if (Context.User.IsBot) {
                return;
            }

            var channel = (Context.User as IGuildUser).VoiceChannel;
            if (channel == null) {
                await ReplyAsync("User not in a voice channel");
                return;
            }

            var audioClient = await channel.ConnectAsync();

            var ffmpeg = CreateStream(url);
            var output = ffmpeg.StandardOutput.BaseStream;
            var discord = audioClient.CreatePCMStream(AudioApplication.Mixed); {
                try {
                    await output.CopyToAsync(discord);
                } finally {
                    await discord.FlushAsync();
                    await channel.DisconnectAsync();
                }
            }
        }
    }

    public class TestAudioModule : AudioAssistModule {
        [Command("test",RunMode = RunMode.Async)]
        [Summary("Plays a specified audio file")]
        public async Task PlayAudio() {
            await VoiceAsync("audio url");
        }
    }
}
