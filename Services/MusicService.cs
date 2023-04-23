using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Stringdicator.Modules;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace Stringdicator.Services {
    /// <summary>
    /// Music service for handling all of the events coming from audio playback in one place
    /// </summary>
    public class MusicService {
        private readonly LavaNode _lavaNode;
        public LavaTrack RequeueCurrentTrack { get; set; }
        public List<LavaTrack> Requeue { get; set; }

        /// <summary>
        /// Constructor for music module to retrieve the lavaNode in use
        /// Uses the lavaNode for retrieving/managing/playing audio to voice channels
        /// </summary>
        /// <param name="lavaNode">The lavaNode to be used for audio playback</param>
        public MusicService(LavaNode lavaNode) {
            _lavaNode = lavaNode;
            _lavaNode.OnTrackEnded += OnTrackEnded;
            _lavaNode.OnTrackException += OnTrackException;
            _lavaNode.OnTrackStuck += OnTrackStuck;
        }

        /// <summary>
        /// The method called when a track ends
        /// Obtained mostly from the Victoria Tutorial pages
        /// </summary>
        /// <param name="args">The information about the track that has ended</param>
        private async Task OnTrackEnded(TrackEndedEventArgs args) {
            await Task.Delay(1000);
            if (args.Reason == TrackEndReason.LoadFailed) {
                await args.Player.TextChannel.SendMessageAsync("The track failed to load, skipping");
                // If there is no next track, stop the player
                if (!args.Player.Queue.TryDequeue(out var nextTrack)) {
                    await _lavaNode.LeaveAsync(args.Player.VoiceChannel);
                    return;
                }
                await args.Player.SkipAsync();
            }
            
            if (args.Reason != TrackEndReason.Finished) {
                return;
            }

            if (args.Player.Track is null && args.Player.PlayerState != PlayerState.Stopped) {
                // If there is no next track, stop the player
                if (!args.Player.Queue.TryDequeue(out var nextTrack)) {
                    await _lavaNode.LeaveAsync(args.Player.VoiceChannel);
                    return;
                }
                await args.Player.SkipAsync();
            }

            //If queue is empty, return
            var player = args.Player;
            if (!player.Queue.TryDequeue(out var queueable)) {
                
                // Restore the previously playing tracks if they were interrupted by a voice line
                if (RequeueCurrentTrack != null) {
                    await player.PlayAsync(RequeueCurrentTrack);
                    await player.SeekAsync(RequeueCurrentTrack.Position);
                    RequeueCurrentTrack = null;
                }
                if (Requeue?.Count > 0) {   
                    player.Queue.Enqueue(Requeue);
                    Requeue = new List<LavaTrack>();
                    return;
                }
                
                await _lavaNode.LeaveAsync(player.VoiceChannel);
                return;
            }

            //General Error case for queue
            if (queueable == null) {
                await player.TextChannel.SendMessageAsync("Next item in queue is not a track.");
                return;
            }

            //If there are no other users in the voice channel, leave
            var voiceChannelUsers = (player.VoiceChannel as SocketVoiceChannel)?.Users
                .Where(x => !x.IsBot)
                .ToArray();
            if (!(voiceChannelUsers ?? Array.Empty<SocketGuildUser>()).Any()) { // Doesn't leave when it finishes
                await _lavaNode.LeaveAsync(player.VoiceChannel);
                return;
            }

            //Play the track and output whats being played
            try {
                await args.Player.PlayAsync(queueable);
            } catch (Exception e) {
                Console.WriteLine(e);
            }

            var builder = new EmbedBuilder {
                Title = "Now Playing: ",
                Description = $"[{player.Track.Title}]({player.Track.Url})" +
                              $"\n {MusicModule.TrimTime(queueable.Position.ToString(@"dd\:hh\:mm\:ss"))} / " +
                              $"{MusicModule.TrimTime(queueable.Duration.ToString(@"dd\:hh\:mm\:ss"))}",
                ThumbnailUrl = await queueable.FetchArtworkAsync(),
                Color = new Color(3447003)
            };
            //Output now playing message
            await player.TextChannel.SendMessageAsync("", false, builder.Build());
        }

        /// <summary>
        /// The method called when a track has an exception
        /// </summary>
        /// <param name="args">The information about the track that has ended</param>
        private static async Task OnTrackException(TrackExceptionEventArgs args) {
            var test = new List<LavaTrack>();
            if (args.Exception.Message.Contains("This video is not available") || args.Player.Track is null) {
                await args.Player.SkipAsync();
                return;
            }
            test.AddRange(args.Player.Queue.RemoveRange(0, args.Player.Queue.Count));
            args.Player.Queue.Clear();
            try {
                await args.Player.PlayAsync(args.Track);
            } catch (Exception e) {
                Console.WriteLine(e);
            }

            args.Player.Queue.Enqueue(test);
        }

        /// <summary>
        /// The method called when a track gets stuck
        /// </summary>
        /// <param name="args">The information about the track that has ended</param>
        private static async Task OnTrackStuck(TrackStuckEventArgs args) {
            var test = new List<LavaTrack>();
            test.AddRange(args.Player.Queue.RemoveRange(0, args.Player.Queue.Count));
            args.Player.Queue.Clear();
            try {
                await args.Player.PlayAsync(args.Track);
            } catch (Exception e) {
                Console.WriteLine(e);
            }

            args.Player.Queue.Enqueue(test);
        }
    }
}