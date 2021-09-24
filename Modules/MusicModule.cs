using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Responses.Search;

namespace Stringdicator.Modules {
    /// <summary>
    /// Module containing all Music related commands
    /// </summary>
    [Summary("Music Commands")]
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
            if (args.Reason != TrackEndReason.Finished || args.Player.PlayerState != PlayerState.Stopped ||
                args.Track == null) {
                return;
            }

            //If queue is empty, return
            var player = args.Player;
            if (!player.Queue.TryDequeue(out var queueable)) {
                await _lavaNode.LeaveAsync(player.VoiceChannel);
                return;
            }

            //General Error case for queue
            if (queueable == null) {
                await player.TextChannel.SendMessageAsync("Next item in queue is not a track.");
                return;
            }

            //Play the song and output whats being played
            await args.Player.PlayAsync(queueable);

            var builder = new EmbedBuilder {
                Title = "Now Playing: ",
                Description = $"[{player.Track.Title}]({player.Track.Url})" +
                              $"\n {TrimTime(queueable.Position.ToString(@"dd\:hh\:mm\:ss"))} / " +
                              $"{TrimTime(queueable.Duration.ToString(@"dd\:hh\:mm\:ss"))}",
                ThumbnailUrl = await queueable.FetchArtworkAsync(),
                Color = new Color(3447003)
            };
            //Output now playing message
            await player.TextChannel.SendMessageAsync("", false, builder.Build());
        }


        /// <summary>
        /// Command for joining the voice channel that a user is currently in
        /// If the user is not currently in a channel, will embed an "error message"
        /// </summary>
        [Command("StringJoin")]
        [Summary("Join the voice channel that the user is currently in")]
        [Alias("SJ")]
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
        [Alias("SL")]
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
        [Alias("SP")]
        private async Task PlayAsync([Remainder] string searchQuery) {
            if (!UserInVoice().Result) {
                return;
            }

            //Join the voice channel if not already in it
            if (!_lavaNode.HasPlayer(Context.Guild)) {
                await JoinAsync();
            }

            var index = 0;
            if (searchQuery.Contains("youtube.com/watch?v=") && searchQuery.Contains("&list=")) {
                //If the video is within a playlist, change the search to be the playlist and just remove the first tracks
                searchQuery = Regex.Replace(searchQuery, @"watch\?v=.*&list=", "playlist?list=");
                index = Convert.ToInt32(searchQuery.Split("&index=")[1]) - 1;
            }

            var searchType = searchQuery.Contains("youtube.com/") ? SearchType.Direct : SearchType.YouTube;

            //Find the search result from the search terms
            var searchResponse = await _lavaNode.SearchAsync(searchType, searchQuery);
            if (searchResponse.Status == SearchStatus.LoadFailed ||
                searchResponse.Status == SearchStatus.NoMatches) {
                await EmbedText($"I wasn't able to find anything for `{searchQuery}`.", false);
                return;
            }

            //Get the player and start playing/queueing a single song or playlist
            var player = _lavaNode.GetPlayer(Context.Guild);
            //Queue up next song
            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused) {
                await QueueNow(searchResponse, player, false, index);
                //Play this song now
            } else {
                await PlayNow(searchResponse, player, index);
            }
        }

        /// <summary>
        /// Add a track to the top of the queue
        /// </summary>
        /// <param name="searchQuery">The user's track search query</param>
        [Command("StringPlayTop", RunMode = RunMode.Async)]
        [Summary("Adds a specified track to the top of the current queue")]
        [Alias("SPT")]
        private async Task PlayTopAsync([Remainder] string searchQuery) {
            if (!UserInVoice().Result) {
                return;
            }

            //Join the voice channel if not already in it
            if (!_lavaNode.HasPlayer(Context.Guild)) {
                await JoinAsync();
            }
            
            var index = 0;
            if (searchQuery.Contains("youtube.com/watch?v=") && searchQuery.Contains("&list=")) {
                //If the video is within a playlist, change the search to be the playlist and just remove the first tracks
                searchQuery = Regex.Replace(searchQuery, @"watch\?v=.*&list=", "playlist?list=");
                index = Convert.ToInt32(searchQuery.Split("&index=")[1]) - 1;
            }

            var searchType = searchQuery.Contains("youtube.com/") ? SearchType.Direct : SearchType.YouTube;

            //Find the search result from the search terms
            var searchResponse = await _lavaNode.SearchAsync(searchType, searchQuery);
            if (searchResponse.Status == SearchStatus.LoadFailed ||
                searchResponse.Status == SearchStatus.NoMatches) {
                await EmbedText($"I wasn't able to find anything for `{searchQuery}`.", false);
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            await QueueNow(searchResponse, player, true, index);
        }

        /// <summary>
        /// Helper method for queuing one or more songs based on the search terms
        /// </summary>
        /// <param name="searchResponse">The response received from the user's search</param>
        /// <param name="player">The LavaPlayer that should queue this track</param>
        /// <param name="insertAtTop">True if the track is to be added at the top of the queue</param>
        /// <param name="index">Used for adding tracks from a playlist from a given index of the playlist</param>
        private async Task QueueNow(SearchResponse searchResponse, LavaPlayer player, bool insertAtTop, int index) {
            var test = new List<LavaTrack>();
            if (insertAtTop) {
                test.AddRange(player.Queue.RemoveRange(0, player.Queue.Count));
                player.Queue.Clear();
            }

            //Playlist queueing
            if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name)) {
                for (var i = index; i < searchResponse.Tracks.Count; i++) {
                    player.Queue.Enqueue(searchResponse.Tracks.ElementAt(i));
                }
                
                await EmbedText($"{searchResponse.Tracks.Count - index} tracks added to queue", true,
                    searchResponse.Playlist.Name, await searchResponse.Tracks.ElementAt(index).FetchArtworkAsync(),
                    true);
            } else {
                //Single song queueing
                var track = searchResponse.Tracks.ElementAt(0);
                player.Queue.Enqueue(track);
                await EmbedText($"{track.Title}", true, TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                    await track.FetchArtworkAsync(), true);
            }

            player.Queue.Enqueue(test);
        }

        /// <summary>
        /// Helper method for playing one or more songs based on the search terms
        /// </summary>
        /// <param name="searchResponse">The response received from the user's search</param>
        /// <param name="player">The LavaPlayer that should play this track</param>
        /// <param name="index"></param>
        private async Task PlayNow(SearchResponse searchResponse, LavaPlayer player, int index) {
            var track = searchResponse.Tracks.ElementAt(index);

            //Play list queueing
            if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name)) {
                for (var i = index; i < searchResponse.Tracks.Count; i++) {
                    if (i == 0 || i == index) {
                        await player.PlayAsync(track);
                        await EmbedText($"Now Playing: {track.Title}", true,
                            "Duration: " + TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                            await track.FetchArtworkAsync(), true);
                    } else {
                        player.Queue.Enqueue(searchResponse.Tracks.ElementAt(i));
                    }
                }

                await EmbedText($"{searchResponse.Tracks.Count - index} tracks added to queue", true,
                    searchResponse.Playlist.Name, await searchResponse.Tracks.ElementAt(index).FetchArtworkAsync(),
                    true);
            } else {
                //Single Song queueing
                await player.PlayAsync(track);
                await EmbedText($"Now Playing: {track.Title}", true,
                    "Duration: " + TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                    await track.FetchArtworkAsync());
            }
        }


        /// <summary>
        /// Skips the currently playing song
        /// </summary>
        [Command("StringSkip")]
        [Summary("Skips the currently playing Track")]
        [Alias("SS")]
        private async Task SkipAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) return;
            var player = _lavaNode.GetPlayer(Context.Guild);


            var builder = new EmbedBuilder();
            builder.WithTitle("Song Skipped: ");
            builder.WithDescription(player.Track.Title);
            builder.WithThumbnailUrl(await player.Queue.ElementAt(0).FetchArtworkAsync());
            builder.AddField(new EmbedFieldBuilder {
                Name = "Now Playing: ",
                Value = player.Queue.ElementAt(0).Title
            });
            builder.WithColor(3447003);
            await ReplyAsync("", false, builder.Build());


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
        [Alias("SS")]
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
                await player.Queue.ElementAt(index - 1).FetchArtworkAsync());
            player.Queue.RemoveAt(index - 1);
        }

        /// <summary>
        /// Command for clearing the current track queue
        /// </summary>
        [Command("StringClear")]
        [Summary("Clear the current track queue")]
        [Alias("SC")]
        private async Task ClearQueueAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) return;
            var player = _lavaNode.GetPlayer(Context.Guild);

            player.Queue.Clear();
            await EmbedText("Queue Cleared", false);
        }

        /// <summary>
        /// Pause the currently playing track
        /// </summary>
        [Command("StringPause")]
        [Summary("Pause the currently playing track")]
        [Alias("SPS")]
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
        [Alias("SR")]
        private async Task ResumeAsync() {
            if (!UserInVoice().Result) {
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild)) return;
            if (_lavaNode.GetPlayer(Context.Guild).PlayerState.Equals(PlayerState.Playing)) return;

            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.ResumeAsync();
            await EmbedText("Resumed", false);
        }


        /// <summary>
        /// Show the track that is currently being played in this voice channel
        /// </summary>
        [Command("StringNowPlaying")]
        [Summary("Show the currently playing track")]
        [Alias("SNP")]
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
                await player.Track.FetchArtworkAsync());
        }


        /// <summary>
        /// Display the current queue of tracks
        /// </summary>
        [Command("StringQueue")]
        [Summary("Display the current track queue with an optional page number")]
        [Alias("SQ")]
        private async Task QueueAsync(int offset = 1) {
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
            offset = (offset - 1) * 5;
            if (offset > player.Queue.Count) {
                return;
            }

            //Create an embed using that image url
            var builder = new EmbedBuilder();
            builder.WithTitle($"String Music Queue - Length: {player.Queue.Count}");
            builder.WithThumbnailUrl(await player.Track.FetchArtworkAsync());
            builder.WithColor(3447003);
            builder.WithDescription("");

            if (player.Queue.Count == 0) {
                await CurrentSongAsync();
                return;
            }


            if (offset == 0) {
                //Now playing
                builder.AddField(new EmbedFieldBuilder {
                    Name = "Now Playing: ",
                    Value = $"[{player.Track.Title}]({player.Track.Url})" +
                            $"\n {TrimTime(player.Track.Position.ToString(@"dd\:hh\:mm\:ss"))} " +
                            $"/ {TrimTime(player.Track.Duration.ToString(@"dd\:hh\:mm\:ss"))}"
                });


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
            } else {
                for (var i = offset; i < offset + 5 && i < player.Queue.Count; i++) {
                    var lavaTrack = player.Queue.ElementAt(i);
                    var fieldBuilder = new EmbedFieldBuilder {
                        Name = $"Queue position {i + 1}",
                        Value = $"[{lavaTrack.Title}]({lavaTrack.Url})" +
                                $"\n {TrimTime(lavaTrack.Duration.ToString(@"dd\:hh\:mm\:ss"))}"
                    };
                    builder.AddField(fieldBuilder);
                }
            }

            await ReplyAsync("", false, builder.Build());
        }

        /// <summary>
        /// Shuffle the current queue for the channel that the user is in
        /// </summary>
        [Command("StringShuffle")]
        [Summary("Shuffle the current queue")]
        [Alias("SSH")]
        private async Task ShuffleQueue() {
            if (!UserInVoice().Result) {
                return;
            }
            

            if (!_lavaNode.HasPlayer(Context.Guild)) return;

            var player = _lavaNode.GetPlayer(Context.Guild);
            player.Queue.Shuffle();
            await EmbedText("Queue Shuffled", false);
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