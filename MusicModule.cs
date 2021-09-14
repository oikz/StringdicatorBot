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
        private async Task OnTrackEnded(TrackEndedEventArgs args) {
            if (args.Reason != TrackEndReason.Finished) {
                return;
            }

            //If queue is empty, return
            var player = args.Player;
            if (!player.Queue.TryDequeue(out var queueable)) {
                return;
            }

            //General Error case for queue
            if (!(queueable is { } track)) {
                await player.TextChannel.SendMessageAsync("Next item in queue is not a track.");
                return;
            }

            //Play the song and output whats being played
            await args.Player.PlayAsync(track);
            await CurrentSongAsync();
        }


        /**
         * Join the voice channel that the user is currently in
         */
        [Command("StringJoin")]
        [Summary("Join the voice channel that the user is currently in")]
        private async Task JoinAsync() {
            //Check if the user is in a voice channel
            if (!UserInVoice().Result) {
                return;
            }

            //Get the users voiceState
            var voiceState = Context.User as IVoiceState;
            //Try to join the channel
            await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
        }

        /**
         * Leave the voice channel that the user is currently in
         */
        [Command("StringLeave")]
        [Summary("Leave the voice channel that the user is currently in")]
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
            if (!UserInVoice().Result) {
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
            //Queue up next song
            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused) {
                //Playlist queueing
                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name)) {
                    foreach (var track in searchResponse.Tracks) {
                        player.Queue.Enqueue(track);
                    }

                    await EmbedText($"{searchResponse.Tracks.Count} tracks added to queue");
                } else {
                    //Single song queueing
                    var track = searchResponse.Tracks.ElementAt(0);
                    player.Queue.Enqueue(track);
                    await EmbedText($"{track.Title}", true, TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                        track.FetchArtworkAsync().Result, true);
                }

                //Play this song now
            } else {
                var track = searchResponse.Tracks.ElementAt(0);

                //Play list queueing
                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name)) {
                    for (var i = 0; i < searchResponse.Tracks.Count; i++) {
                        if (i == 0) {
                            await player.PlayAsync(track);
                            await EmbedText($"Now Playing: {track.Title}", true, TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                                track.FetchArtworkAsync().Result, true);
                        } else {
                            player.Queue.Enqueue(searchResponse.Tracks.ElementAt(i));
                        }
                    }

                    await EmbedText($"{searchResponse.Tracks.Count} tracks added to queue", true,
                        TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                        track.FetchArtworkAsync().Result);
                } else {
                    //Single Song queueing
                    await player.PlayAsync(track);
                    await EmbedText($"Now Playing: {track.Title}", true, TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
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
            if (!UserInVoice().Result) {
                return;
            }

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
            if (!UserInVoice().Result) {
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (player.Queue.Count < index - 1) {
                await EmbedText("Index is longer than the queue length", false);
                return;
            }

            await EmbedText("Song Skipped: " + player.Queue.ElementAt(index - 1).Title, true, "",
                player.Queue.ElementAt(index - 1).FetchArtworkAsync().Result);
            player.Queue.RemoveAt(index - 1);
        }

        /**
         * Pause the song currently playing
         */
        [Command("StringPause")]
        [Summary("Pause the currently playing song")]
        private async Task PauseAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.PauseAsync();
            await EmbedText("Paused", false);
        }

        /**
         * Resume the currently playing song
         */
        [Command("StringResume")]
        [Summary("Resume the currently playing song")]
        private async Task ResumeAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.ResumeAsync();
            await EmbedText("Resumed", false);
        }

        /**
         * Show the song currently playing
         */
        [Command("StringSong")]
        [Summary("Show the currently playing song")]
        private async Task CurrentSongAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (player.PlayerState == PlayerState.None) {
                await EmbedText("Not playing anything", false);
            }

            await EmbedText("Now Playing: ", true, $"[{player.Track.Title}]({player.Track.Url})" +
                                                   $"\n {TrimTime(player.Track.Position.ToString(@"dd\:hh\:mm\:ss"))} / " +
                                                   $"{TrimTime(player.Track.Duration.ToString(@"dd\:hh\:mm\:ss"))}",
                player.Track.FetchArtworkAsync().Result);
        }

        /**
         * Display the current queue
         */
        [Command("StringQueue")]
        [Summary("Display the current song queue")]
        private async Task QueueAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) {
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);

            if ((player.PlayerState == PlayerState.Stopped || player.PlayerState == PlayerState.None) &&
                player.Queue.Count == 0) {
                await EmbedText("Queue is empty", false);
                return;
            }

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle("String Music Queue");
            builder.WithThumbnailUrl(player.Track.FetchArtworkAsync().Result);
            builder.WithColor(3447003);
            builder.WithDescription("");

            if (player.PlayerState == PlayerState.Playing && player.Queue.Count == 0) {
                await CurrentSongAsync();
            } else if (player.PlayerState == PlayerState.Playing) {
                builder.AddField(new EmbedFieldBuilder {
                    Name = "Now Playing: ",
                    Value = $"[{player.Track.Title}]({player.Track.Url})" +
                            $"\n {TrimTime(player.Track.Position.ToString(@"dd\:hh\:mm\:ss"))} " +
                            $"/ {TrimTime(player.Track.Duration.ToString(@"dd\:hh\:mm\:ss"))}"
                });
            }


            //Up next
            builder.AddField(new EmbedFieldBuilder {
                Name = "Next: ",
                Value = $"[{player.Queue.ElementAt(0).Title}]({player.Queue.ElementAt(0).Url})" +
                        $"\n {TrimTime(player.Queue.ElementAt(0).Duration.ToString(@"dd\:hh\:mm\:ss"))}"
            });

            //Remaining Queue
            for (var i = 1; i < 4 && i < player.Queue.Count; i++) {
                var lavaTrack = player.Queue.ElementAt(i);
                var fieldBuilder = new EmbedFieldBuilder {
                    Name = $"Queue position {i + 1}",
                    Value = $"[{lavaTrack.Title}]({lavaTrack.Url})" +
                            $"\n {TrimTime(lavaTrack.Duration.ToString(@"dd\:hh\:mm\:ss"))}"
                };
                builder.AddField(fieldBuilder);
            }

            await ReplyAsync("", false, builder.Build());
        }

        private async Task EmbedText(string title, bool hasThumbnail = true, string description = "",
            string thumbnail = "", bool hasAuthor = false) {
            var builder = new EmbedBuilder();
            builder.WithTitle(title);
            builder.WithDescription(description);
            if (hasThumbnail)
                builder.WithThumbnailUrl(thumbnail == "" ? Context.Client.CurrentUser.GetAvatarUrl() : thumbnail);
            if (hasAuthor)
                builder.WithAuthor(new EmbedAuthorBuilder
                    {IconUrl = Context.User.GetAvatarUrl(), Name = "Added to queue"});
            builder.WithColor(3447003);
            await ReplyAsync("", false, builder.Build());
        }

        /**
         * Check if the bot is in a voice channel
         */
        private async Task<bool> UserInVoice() {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel != null) return true;
            await EmbedText("You must be connected to a voice channel!", false);
            return false;
        }

        /**
         * Trim the start of the time string to remove any leading "00:" sections
         */
        private static string TrimTime(string time) {
            if (time.StartsWith("00:")) {
                time = time.TrimStart('0', ':');
            }
            
            return time;
        }
    }
}