using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Responses.Search;

namespace Stringdicator {
    /**
    * Base play module for playing/queueing up songs
    * Joins the users channel and plays the specified url, then disconnects
    */
    public class AudioModule : ModuleBase<SocketCommandContext> {
        private readonly LavaNode _lavaNode;

        public AudioModule(LavaNode lavaNode) {
            _lavaNode = lavaNode;
            _lavaNode.OnTrackEnded += OnTrackEnded;
        }

        /**
         * Method called when the currently playing track ends
         * Obtained mostly from the Victoria Tutorial pages
         */
        private static async Task OnTrackEnded(TrackEndedEventArgs args) {
            if (args.Reason != TrackEndReason.Finished) {
                return;
            }

            var player = args.Player;
            if (!player.Queue.TryDequeue(out var queueable)) {
                return;
            }

            if (!(queueable is { } track)) {
                await player.TextChannel.SendMessageAsync("Next item in queue is not a track.");
                return;
            }

            await args.Player.PlayAsync(track);
            await args.Player.TextChannel.SendMessageAsync(
                $"{args.Reason}: {args.Track.Title}\nNow playing: {track.Title}");
        }


        /**
         * Join the voice channel that the user is currently in
         */
        [Command("Join")]
        [Summary("Join the voice channel that the user is currently in")]
        private async Task JoinAsync() {
            //Check if the user is in a voice channel
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null) {
                await ReplyAsync("You must be connected to a voice channel!");
                return;
            }

            //Try to join the channel
            await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
        }

        /**
         * Play a song or queue up a song
         */
        [Command("StringPlay")]
        [Summary("Play a song or queue up a song")]
        public async Task PlayAsync([Remainder] string searchQuery) {
            //Ensure that the user supplies search terms
            if (string.IsNullOrWhiteSpace(searchQuery)) {
                await ReplyAsync("Please provide search terms.");
                return;
            }

            //Join the voice channel if not already in it
            if (!_lavaNode.HasPlayer(Context.Guild)) {
                await JoinAsync();
            }


            //Find the search result from the search terms
            var searchResponse = await _lavaNode.SearchAsync(SearchType.YouTube, searchQuery);
            if (searchResponse.Status == SearchStatus.LoadFailed ||
                searchResponse.Status == SearchStatus.NoMatches) {
                await ReplyAsync($"I wasn't able to find anything for `{searchQuery}`.");
                return;
            }

            //Get the player and start playing/queueing a single song or playlist
            var player = _lavaNode.GetPlayer(Context.Guild);
            //Single Song
            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused) {
                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name)) {
                    foreach (var track in searchResponse.Tracks) {
                        player.Queue.Enqueue(track);
                    }

                    await ReplyAsync($"Enqueued {searchResponse.Tracks.Count} tracks.");
                } else {
                    var track = searchResponse.Tracks.ElementAt(0);
                    player.Queue.Enqueue(track);
                    await ReplyAsync($"Enqueued: {track.Title}");
                }

                //Playlist
            } else {
                var track = searchResponse.Tracks.ElementAt(0);

                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name)) {
                    for (var i = 0; i < searchResponse.Tracks.Count; i++) {
                        if (i == 0) {
                            await player.PlayAsync(track);
                            await ReplyAsync($"Now Playing: {track.Title}");
                        } else {
                            player.Queue.Enqueue(searchResponse.Tracks.ElementAt(i));
                        }
                    }

                    await ReplyAsync($"Enqueued {searchResponse.Tracks.Count} tracks.");
                } else {
                    await player.PlayAsync(track);
                    await ReplyAsync($"Now Playing: {track.Title}");
                }
            }
        }


        /**
         * Skip the current song
         */
        [Command("StringSkip")]
        [Summary("Skips the currently playing song")]
        private async Task SkipAsync() {
            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.SkipAsync();
        }

        /**
         * Remove a song from the queue
         */
        [Command("StringSkip")]
        [Summary("Skips a specified song in the queue")]
        private async Task RemoveFromQueueAsync([Remainder] int index) {
            var player = _lavaNode.GetPlayer(Context.Guild);
            if (player.Queue.Count < index) {
                await ReplyAsync("Index is shorter than the queue length");
                return;
            }

            player.Queue.RemoveAt(index);
        }

        /**
         * Pause the song currently playing
         */
        [Command("StringPause")]
        [Summary("Pause the currently playing song")]
        private async Task PauseAsync() {
            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.PauseAsync();
        }

        /**
         * Resume the currently playing song
         */
        [Command("StringResume")]
        [Summary("Resume the currently playing song")]
        private async Task ResumeAsync() {
            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.ResumeAsync();
        }
    }
}