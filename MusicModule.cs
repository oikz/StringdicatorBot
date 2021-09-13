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
                "Now playing: " + track.Title);
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
                await EmbedText("You must be connected to a voice channel!", false);
                return;
            }

            //Try to join the channel
            await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
        }

        /**
         * Leave the voice channel that the user is currently in
         */
        private async Task LeaveAsync() {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null) {
                return;
            }

            if (_lavaNode.HasPlayer(Context.Guild)) {
                var player = _lavaNode.GetPlayer(Context.Guild);
                await _lavaNode.LeaveAsync(player.VoiceChannel);
                await EmbedText("Disconnected", false);
            }
        }

        /**
         * Play a song or queue up a song
         */
        [Command("StringPlay", RunMode = RunMode.Async)]
        [Summary("Play a song or queue up a song")]
        public async Task PlayAsync([Remainder] string searchQuery) {
            //Ensure that the user supplies search terms
            if (string.IsNullOrWhiteSpace(searchQuery)) {
                await EmbedText("Please provide search terms.", false);
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
                await EmbedText($"I wasn't able to find anything for `{searchQuery}`.", false);
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

                    await EmbedText($"Enqueued {searchResponse.Tracks.Count} tracks.");
                } else {
                    var track = searchResponse.Tracks.ElementAt(0);
                    player.Queue.Enqueue(track);
                    await EmbedText($"Enqueued: {track.Title}", true, track.Duration.ToString(),
                        track.FetchArtworkAsync().Result);
                }

                //Playlist
            } else {
                var track = searchResponse.Tracks.ElementAt(0);

                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name)) {
                    for (var i = 0; i < searchResponse.Tracks.Count; i++) {
                        if (i == 0) {
                            await player.PlayAsync(track);
                            await EmbedText($"Now Playing: {track.Title}", true, track.Duration.ToString(),
                                track.FetchArtworkAsync().Result);
                        } else {
                            player.Queue.Enqueue(searchResponse.Tracks.ElementAt(i));
                        }
                    }

                    await EmbedText($"Enqueued {searchResponse.Tracks.Count} tracks.", true, track.Duration.ToString(),
                        track.FetchArtworkAsync().Result);
                } else {
                    await player.PlayAsync(track);
                    await EmbedText($"Now Playing: {track.Title}", true, track.Duration.ToString(),
                        track.FetchArtworkAsync().Result);
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
            await EmbedText("Song Skipped: " + player.Track.Title, true, "", player.Track.FetchArtworkAsync().Result);
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
                await EmbedText("Index is shorter than the queue length", false);
                return;
            }

            await EmbedText("Song Skipped: " + player.Track.Title, true, "",
                player.Queue.ElementAt(index).FetchArtworkAsync().Result);
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
            await EmbedText("Paused");
        }

        /**
         * Resume the currently playing song
         */
        [Command("StringResume")]
        [Summary("Resume the currently playing song")]
        private async Task ResumeAsync() {
            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.ResumeAsync();
            await EmbedText("Resumed");
        }

        /**
         * Show the song currently playing
         */
        [Command("StringSong")]
        [Summary("Show the currently playing song")]
        private async Task CurrentSongAsync() {
            var player = _lavaNode.GetPlayer(Context.Guild);
            await EmbedText(player.Track.Title, true, player.Track.Position.ToString(@"hh\:mm\:ss") + " / " + player.Track.Duration,
                player.Track.FetchArtworkAsync().Result);
        }

        /**
         * Display the current queue
         */
        [Command("StringQueue")]
        [Summary("Display the current song queue")]
        private async Task QueueAsync() {
            var player = _lavaNode.GetPlayer(Context.Guild);
            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("String Music Queue");
            builder.WithThumbnailUrl(player.Track.FetchArtworkAsync().Result);
            builder.WithColor(3447003);
            builder.WithDescription("");

            for (var i = 0; i < player.Queue.Count; i++) {
                var lavaTrack = player.Queue.ElementAt(i);
                var fieldBuilder = new EmbedFieldBuilder {Name = lavaTrack.Title, Value = lavaTrack.Duration};
                builder.AddField(fieldBuilder);
            }

            await ReplyAsync("", false, builder.Build());
        }

        private async Task EmbedText(string title, bool hasThumbnail = true, string description = "",
            string thumbnail = "") {
            var builder = new EmbedBuilder();
            builder.WithTitle(title);
            builder.WithDescription(description);
            if (hasThumbnail)
                builder.WithThumbnailUrl(thumbnail == "" ? Context.Client.CurrentUser.GetAvatarUrl() : thumbnail);

            builder.WithColor(3447003);
            await ReplyAsync("", false, builder.Build());
        }
    }
}