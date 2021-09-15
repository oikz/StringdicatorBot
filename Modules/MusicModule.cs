using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Responses.Search;

namespace Stringdicator.Modules {
    /**
    * Base play module for playing/queueing up songs
    * Joins the users channel and plays the specified url, then disconnects
    */
    /// <summary>
    /// Module containing all Music related commands
    /// </summary>
    public class MusicModule : ModuleBase<SocketCommandContext> {
        private readonly LavaNode _lavaNode;

        /// <summary>
        /// Constructor for music module to retrieve the lavaNode in use
        /// Uses the lavaNode for retrieving/managing/playing audio to voice channels
        /// </summary>
        /// <param name="lavaNode">The lavaNode to be used for audio playback</param>
        public MusicModule(LavaNode lavaNode) {
            _lavaNode = lavaNode;
            _lavaNode.OnTrackEnded += OnTrackEnded;
        }

        /// <summary>
        /// The method called when a track ends
        /// Obtained mostly from the Victoria Tutorial pages
        /// </summary>
        /// <param name="args">The information about the track that has ended</param>
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


        /// <summary>
        /// Command for joining the voice channel that a user is currently in
        /// If the user is not currently in a channel, will embed an "error message"
        /// </summary>
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

        /// <summary>
        /// Leave the voice channel that the user is currently in
        /// If the user is not currently in a channel, does nothing
        /// </summary>
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

        /// <summary>
        /// Play a track or if one is already being played, add it to the queue
        /// </summary>
        /// <param name="searchQuery">The user's track search query</param>
        [Command("StringPlay", RunMode = RunMode.Async)]
        [Summary("Play a song or queue up a song")]
        private async Task PlayAsync([Remainder] string searchQuery) {
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
                            await EmbedText($"Now Playing: {track.Title}", true,
                                "Duration: " + TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
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
                    await EmbedText($"Now Playing: {track.Title}", true,
                        "Duration: " + TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                        track.FetchArtworkAsync().Result);
                }
            }
        }


        /// <summary>
        /// Skips the currently playing song
        /// </summary>
        [Command("StringSkip")]
        [Summary("Skips the currently playing song")]
        private async Task SkipAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) return;
            var player = _lavaNode.GetPlayer(Context.Guild);
            await EmbedText("Song Skipped: ", true, player.Track.Title, player.Track.FetchArtworkAsync().Result);
            if (!player.Queue.Any()) {
                await player.StopAsync();
            } else {
                await player.SkipAsync();
            }
        }


        /// <summary>
        /// Removes a specified song from the queue
        /// </summary>
        /// <param name="index">The index in the queue that the user wishes to skip</param>
        [Command("StringSkip")]
        [Summary("Skips a specified song in the queue")]
        private async Task RemoveFromQueueAsync([Remainder] int index) {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) return;
            var player = _lavaNode.GetPlayer(Context.Guild);
            if (player.Queue.Count < index - 1) {
                await EmbedText("Index is longer than the queue length", false);
                return;
            }

            await EmbedText("Song Skipped: ", true, player.Queue.ElementAt(index - 1).Title,
                player.Queue.ElementAt(index - 1).FetchArtworkAsync().Result);
            player.Queue.RemoveAt(index - 1);
        }

        /// <summary>
        /// Pause the currently playing track
        /// </summary>
        [Command("StringPause")]
        [Summary("Pause the currently playing track")]
        private async Task PauseAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) return;

            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.PauseAsync();
            await EmbedText("Paused", false);
        }


        /// <summary>
        /// Resume the currently playing track
        /// </summary>
        [Command("StringResume")]
        [Summary("Resume the currently playing track")]
        private async Task ResumeAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) return;
            if (!_lavaNode.GetPlayer(Context.Guild).PlayerState.Equals(PlayerState.Playing)) return;

            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.ResumeAsync();
            await EmbedText("Resumed", false);
        }


        /// <summary>
        /// Show the track that is currently being played in this voice channel
        /// </summary>
        [Command("StringPlaying")]
        [Summary("Show the currently playing track")]
        private async Task CurrentSongAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) return;

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (player.PlayerState == PlayerState.None) {
                await EmbedText("Not playing anything", false);
            }

            await EmbedText("Now Playing: ", true, $"[{player.Track.Title}]({player.Track.Url})" +
                                                   $"\n {TrimTime(player.Track.Position.ToString(@"dd\:hh\:mm\:ss"))} / " +
                                                   $"{TrimTime(player.Track.Duration.ToString(@"dd\:hh\:mm\:ss"))}",
                player.Track.FetchArtworkAsync().Result);
        }


        /// <summary>
        /// Display the current queue of tracks
        /// </summary>
        [Command("StringQueue")]
        [Summary("Display the current track queue")]
        private async Task QueueAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) return;

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

        /// <summary>
        /// Embed and send a message with the provided parameters
        /// </summary>
        /// <param name="title">The title of the embed to send</param>
        /// <param name="hasThumbnail">True if the embed should contain a thumbnail</param>
        /// <param name="description">The description of the embed to send</param>
        /// <param name="thumbnail">The thumbnail URL to be used by the embed, defaults to empty (use bot's avatar)</param>
        /// <param name="hasAuthor">True if the embed should contain an Author field</param>
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

        /// <summary>
        /// Check if the user sending the command is in a voice channel
        /// </summary>
        /// <returns>A Task with result true if the user is in a channel</returns>
        private async Task<bool> UserInVoice() {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel != null) return true;
            await EmbedText("You must be connected to a voice channel!", false);
            return false;
        }

        /// <summary>
        /// Trim the start of the time string to remove any leading "00:" sections
        /// Leaves at least 0:XX as a default
        /// </summary>
        /// <param name="time">The string time to be trimmed</param>
        /// <returns>A trimmed string containing nice formatting</returns>
        private static string TrimTime(string time) {
            if (time.StartsWith("00:")) {
                time = time.TrimStart('0', ':');
            }

            time = time.Length switch {
                //Always have at least minutes and seconds displayed
                2 => "0:" + time,
                1 => "0:0" + time,
                0 => "0:00" + time,
                _ => time
            };

            return time;
        }
    }
}