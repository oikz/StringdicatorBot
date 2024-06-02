using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Stringdicator.Modules;
using Stringdicator.Util;
using Victoria;
using Victoria.Enums;
using Victoria.WebSocket.EventArgs;

namespace Stringdicator.Services;

/// <summary>
/// Music service for handling all the events coming from audio playback in one place
/// </summary>
public class MusicService {
    private readonly LavaNode<LavaPlayer<LavaTrack>, LavaTrack> _lavaNode;
    public LavaTrack RequeueCurrentTrack { get; set; }
    public List<LavaTrack> Requeue { get; set; }
    private List<LavaPlayer<LavaTrack>> RepeatPlayer { get; } = [];
    public Dictionary<ulong, IVoiceChannel> VoiceChannels { get; } = new();
    public Dictionary<ulong, ITextChannel> TextChannels { get; } = new();

    /// <summary>
    /// Constructor for music module to retrieve the lavaNode in use
    /// Uses the lavaNode for retrieving/managing/playing audio to voice channels
    /// </summary>
    /// <param name="lavaNode">The lavaNode to be used for audio playback</param>
    public MusicService(LavaNode<LavaPlayer<LavaTrack>, LavaTrack> lavaNode) {
        _lavaNode = lavaNode;
        _lavaNode.OnTrackEnd += OnTrackEnd;
        _lavaNode.OnTrackException += OnTrackException;
        _lavaNode.OnTrackStuck += OnTrackStuck;
    }

    /// <summary>
    /// The method called when a track ends
    /// Obtained mostly from the Victoria Tutorial pages
    /// </summary>
    /// <param name="args">The information about the track that has ended</param>
    private async Task OnTrackEnd(TrackEndEventArg args) {
        await Task.Delay(1000);
        var player = await _lavaNode.GetPlayerAsync(args.GuildId);
        if (args.Reason == TrackEndReason.Load_Failed) {
            // If there is no next track, stop the player

            if (player.GetQueue().Count == 0) {
                await _lavaNode.LeaveAsync(VoiceChannels[player.GuildId]);
                VoiceChannels.Remove(player.GuildId);
                TextChannels.Remove(player.GuildId);
                return;
            }

            player.GetQueue().TryDequeue(out var track);
            await player.PlayAsync(_lavaNode, track, false);
        }

        if (args.Reason != TrackEndReason.Finished) {
            return;
        }

        if (RepeatPlayer.Contains(player)) {
            // Put the current track on the top of the queue
            var tempQueue = player.GetQueue();
            player.GetQueue().Clear();
            await player.PlayAsync(_lavaNode, args.Track);
            foreach (var item in tempQueue) {
                player.GetQueue().Enqueue(item);
            }

            return;
        }


        if (player.Track is null) {
            // If there is no next track, stop the player
            if (player.GetQueue().Count == 0 && RequeueCurrentTrack is null && Requeue?.Count == 0) {
                await _lavaNode.LeaveAsync(VoiceChannels[player.GuildId]);
                VoiceChannels.Remove(player.GuildId);
                TextChannels.Remove(player.GuildId);
                return;
            }
        }

        if (!player.GetQueue().TryDequeue(out var queueable)) {
            // Restore the previously playing tracks if they were interrupted by a voice line
            if (RequeueCurrentTrack != null) {
                await player.PlayAsync(_lavaNode, RequeueCurrentTrack);
                await player.SeekAsync(_lavaNode, RequeueCurrentTrack.Position);
                RequeueCurrentTrack = null;

                if (!(Requeue?.Count > 0)) return;
                foreach (var item in Requeue) {
                    player.GetQueue().Enqueue(item);
                }

                Requeue = [];

                return;
            }

            await _lavaNode.LeaveAsync(VoiceChannels[player.GuildId]);
            VoiceChannels.Remove(player.GuildId);
            TextChannels.Remove(player.GuildId);
            return;
        }

        //General Error case for queue
        if (queueable == null) {
            await TextChannels[player.GuildId].SendMessageAsync("Next item in queue is not a track."); // TODO PAIN
            return;
        }

        //If there are no other users in the voice channel, leave
        var voiceChannelUsers = (VoiceChannels[player.GuildId] as SocketVoiceChannel)?.ConnectedUsers
            .Where(x => !x.IsBot)
            .ToArray();
        if ((voiceChannelUsers ?? []).Length == 0) {
            await _lavaNode.LeaveAsync(VoiceChannels[player.GuildId]);
            VoiceChannels.Remove(player.GuildId);
            TextChannels.Remove(player.GuildId);
            return;
        }

        //Play the track and output what's being played
        try {
            await player.PlayAsync(_lavaNode, queueable);
        }
        catch (Exception e) {
            Console.WriteLine(e);
        }

        var builder = new EmbedBuilder {
            Title = "Now Playing: ",
            Description = $"[{queueable.Title}]({queueable.Url})" +
                          $"\n {MusicModule.TrimTime(queueable.Position.ToString(@"dd\:hh\:mm\:ss"))} / " +
                          $"{MusicModule.TrimTime(queueable.Duration.ToString(@"dd\:hh\:mm\:ss"))}",
            ThumbnailUrl = await queueable.FetchArtworkAsync(),
            Color = new Color(3447003)
        };

        //Output now playing message
        await TextChannels[player.GuildId].SendMessageAsync("", false, builder.Build());
    }

    /// <summary>
    /// The method called when a track has an exception
    /// </summary>
    /// <param name="args">The information about the track that has ended</param>
    private async Task OnTrackException(TrackExceptionEventArg args) {
        var backup = new List<LavaTrack>();
        var player = await _lavaNode.GetPlayerAsync(args.GuildId);
        if (args.Exception.Message?.Contains("This video is not available") ?? args.Exception.Message?.Contains("This video is unavailable") ?? false) {
            backup.AddRange(player.GetQueue().RemoveRange(0, player.GetQueue().Count));
            player.GetQueue().Clear();
            player.GetQueue().Enqueue(args.Track);
            foreach (var item in backup) {
                player.GetQueue().Enqueue(item);
            }

            return;
        }

        if (player.Track is null) {
            if (player.GetQueue().Count == 0) {
                await _lavaNode.LeaveAsync(VoiceChannels[player.GuildId]);
                VoiceChannels.Remove(player.GuildId);
                TextChannels.Remove(player.GuildId);
                return;
            }

            player.GetQueue().TryDequeue(out var track);
            await player.PlayAsync(_lavaNode, track, false);
            return;
        }

        backup.AddRange(player.GetQueue().RemoveRange(0, player.GetQueue().Count));
        player.GetQueue().Clear();
        try {
            await player.PlayAsync(_lavaNode, args.Track);
        }
        catch (Exception e) {
            Console.WriteLine(e);
        }

        foreach (var item in backup) {
            player.GetQueue().Enqueue(item);
        }
    }

    /// <summary>
    /// The method called when a track gets stuck
    /// </summary>
    /// <param name="args">The information about the track that has ended</param>
    private async Task OnTrackStuck(TrackStuckEventArg args) {
        var test = new List<LavaTrack>();
        var player = await _lavaNode.GetPlayerAsync(args.GuildId);
        test.AddRange(player.GetQueue().RemoveRange(0, player.GetQueue().Count));
        player.GetQueue().Clear();
        try {
            await player.PlayAsync(_lavaNode, args.Track);
        }
        catch (Exception e) {
            Console.WriteLine(e);
        }

        foreach (var item in test) {
            player.GetQueue().Enqueue(item);
        }
    }

    /// <summary>
    /// Repeat the current track on the current player
    /// </summary>
    /// <param name="player">The LavaPlayer for the server to repeat for</param>
    public void RepeatTrack(LavaPlayer<LavaTrack> player) {
        if (RepeatPlayer.Contains(player)) {
            RepeatPlayer.Remove(player);
        } else {
            RepeatPlayer.Add(player);
        }
    }
}