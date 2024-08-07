﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Stringdicator.Database;
using Stringdicator.Services;
using Stringdicator.Util;
using Victoria;
using Victoria.Rest.Search;

namespace Stringdicator.Modules;

/// <summary>
/// Module containing all Music related commands
/// </summary>
public class MusicModule : InteractionModuleBase<SocketInteractionContext> {
    private readonly LavaNode<LavaPlayer<LavaTrack>, LavaTrack> _lavaNode;
    private readonly MusicService _musicService;
    private readonly HttpClient _httpClient;
    private readonly ApplicationContext _applicationContext;

    /// <summary>
    /// Constructor for music module to retrieve the lavaNode in use
    /// Uses the lavaNode for retrieving/managing/playing audio to voice channels
    /// </summary>
    /// <param name="lavaNode">The lavaNode to be used for audio playback</param>
    /// <param name="musicService">The musicService responsible for handling music events</param>
    /// <param name="httpClient">The httpClient to be used for making http requests</param>
    /// <param name="applicationContext">The database context to be used for accessing the database</param>
    public MusicModule(LavaNode<LavaPlayer<LavaTrack>, LavaTrack> lavaNode, MusicService musicService,
        HttpClient httpClient, ApplicationContext applicationContext) {
        _lavaNode = lavaNode;
        _musicService = musicService;
        _httpClient = httpClient;
        _applicationContext = applicationContext;
    }

    /// <summary>
    /// Command for joining the voice channel that a user is currently in
    /// Separate from join action to prevent responding to a message twice
    /// If the user is not currently in a channel, will embed an "error message"
    /// </summary>
    [SlashCommand("join", "Join the voice channel that the user is currently in")]
    private async Task JoinCommandAsync() {
        await JoinAsync();
        await RespondAsync("Joining", ephemeral: true);
    }

    /// <summary>
    /// Method for joining a voice channel
    /// </summary>
    private async Task JoinAsync() {
        //Check if the user is in a voice channel
        if (!UserInVoice().Result) {
            return;
        }

        //Get the users voiceState
        var voiceState = Context.User as IVoiceState;

        //Try to join the channel
        await _lavaNode.JoinAsync(voiceState?.VoiceChannel);
        _musicService.TextChannels.TryAdd(voiceState.VoiceChannel.GuildId, Context.Channel as ITextChannel);
        _musicService.VoiceChannels.TryAdd(voiceState.VoiceChannel.GuildId, voiceState?.VoiceChannel);
    }

    /// <summary>
    /// Leave the voice channel that the user is currently in
    /// If the user is not currently in a channel, does nothing
    /// </summary>
    [SlashCommand("leave", "Leave the voice channel that the user is currently in")]
    private async Task LeaveAsync() {
        if (!UserInVoice().Result || !BotInVoice().Result) {
            return;
        }


        await _lavaNode.TryGetPlayerAsync(Context.Guild.Id);
        if (await _lavaNode.TryGetPlayerAsync(Context.Guild.Id) is not null) {
            var player = await _lavaNode.GetPlayerAsync(Context.Guild.Id);
            await _lavaNode.LeaveAsync(GetVoiceChannel());
            await RespondAsync("Leaving", ephemeral: true);
        }
    }

    /// <summary>
    /// Play a track or if one is already being played, add it to the queue
    /// </summary>
    /// <param name="searchQuery">The user's track search query</param>
    [SlashCommand("play", "Play a track or queue up a track", runMode: RunMode.Async)]
    private async Task PlayAsync([Summary("query", "The search query to use in the search")] string searchQuery) {
        if (!UserInVoice().Result) {
            return;
        }

        await DeferAsync();

        var player = await _lavaNode.TryGetPlayerAsync(GetVoiceChannel().Guild.Id);
        if (player is null || player.State.IsConnected == false) {
            await JoinAsync();
            player = await _lavaNode.GetPlayerAsync(GetVoiceChannel().Guild.Id);
        }

        //Convert shortened link to full link
        if (searchQuery.Contains("youtu.be")) {
            searchQuery = searchQuery.Replace("youtu.be/", "youtube.com/watch?v=");
        }

        var index = 0;
        if (searchQuery.Contains("youtube.com/watch?v=") && searchQuery.Contains("&list=")) {
            //If the video is within a playlist, change the search to be the playlist and just remove the first tracks
            searchQuery = Regex.Replace(searchQuery, @"watch\?v=.*&list=", "playlist?list=");
            if (searchQuery.Contains("&index=")) {
                index = Convert.ToInt32(searchQuery.Split("&index=")[1]) - 1;
            } else {
                index = 0;
            }
        }

        //Get the timestamp from the search query if present
        searchQuery = searchQuery.Replace("?t=", "&t=");
        var timestampString = searchQuery.Contains("&t=") ? searchQuery.Split("&t=")[1] : null;
        var timestamp = TimeSpan.FromSeconds(Convert.ToDouble(timestampString));

        // Add YouTube Search to the search query
        if (!searchQuery.Contains("youtube.com")) {
            searchQuery = "ytsearch:" + searchQuery;
        }

        //Find the search result from the search terms
        var searchResponse = await _lavaNode.LoadTrackAsync(searchQuery);

        // Try again a couple of times for error handling
        for (var i = 0; i < 10; i++) {
            if (searchResponse.Exception.Message is "This video is unavailable") {
                //searchResponse = await _lavaNode.SearchAsync(searchType, searchQuery);
                searchResponse = await _lavaNode.LoadTrackAsync(searchQuery);
            }
        }

        if (searchResponse.Type is SearchType.Error or SearchType.Error) {
            await EmbedText($"I wasn't able to find anything for `{searchQuery}`.", false);
            return;
        }

        if (player.GetQueue().Count == 0 && player.Track is null) {
            await PlayNow(searchResponse, player, index, timestamp);
            return;
        }

        await QueueNow(searchResponse, player, false, index);
    }

    /// <summary>
    /// Add a track to the top of the queue
    /// </summary>
    /// <param name="searchQuery">The user's track search query</param>
    [SlashCommand("playtop", "Adds a specified track to the top of the current queue", runMode: RunMode.Async)]
    private async Task
        PlayTopAsync([Summary("query", "The search query to use in the search")] string searchQuery) {
        if (!UserInVoice().Result) {
            return;
        }

        await DeferAsync();

        var player = await _lavaNode.TryGetPlayerAsync(GetVoiceChannel().Guild.Id);
        if (player is null || player.State.IsConnected == false) {
            await JoinAsync();
            player = await _lavaNode.GetPlayerAsync(GetVoiceChannel().Guild.Id);
        }

        //Convert shortened link to full link
        if (searchQuery.Contains("youtu.be")) {
            searchQuery = searchQuery.Replace("youtu.be/", "youtube.com/watch?v=");
        }

        var index = 0;
        if (searchQuery.Contains("youtube.com/watch?v=") && searchQuery.Contains("&list=")) {
            //If the video is within a playlist, change the search to be the playlist and just remove the first tracks
            searchQuery = Regex.Replace(searchQuery, @"watch\?v=.*&list=", "playlist?list=");
            index = Convert.ToInt32(searchQuery.Split("&index=")[1]) - 1;
        }

        if (searchQuery.Contains("youtube.com")) {
            searchQuery = "ytsearch:" + searchQuery;
        }

        //Find the search result from the search terms
        //var searchResponse = await _lavaNode.SearchAsync(searchType, searchQuery);
        var searchResponse = await _lavaNode.LoadTrackAsync(searchQuery);

        // Try again a couple of times for error handling
        for (var i = 0; i < 10; i++) {
            if (searchResponse.Exception.Message is "This video is unavailable") {
                searchResponse = await _lavaNode.LoadTrackAsync(searchQuery);
            }
        }

        if (searchResponse.Type is SearchType.Error or SearchType.Error) {
            await EmbedText($"I wasn't able to find anything for `{searchQuery}`.", false);
            return;
        }

        await QueueNow(searchResponse, player, true, index);
    }

    /// <summary>
    /// Helper method for queuing one or more tracks based on the search terms
    /// </summary>
    /// <param name="searchResponse">The response received from the user's search</param>
    /// <param name="player">The LavaPlayer<LavaTrack> that should queue this track</param>
    /// <param name="insertAtTop">True if the track is to be added at the top of the queue</param>
    /// <param name="index">Used for adding tracks from a playlist from a given index of the playlist</param>
    private async Task QueueNow(SearchResponse searchResponse, LavaPlayer<LavaTrack> player, bool insertAtTop,
        int index) {
        var test = new List<LavaTrack>();
        if (insertAtTop) {
            test.AddRange(player.GetQueue().RemoveRange(0, player.GetQueue().Count));
            player.GetQueue().Clear();
        }

        //Playlist queueing
        if (searchResponse.Type == SearchType.Playlist) {
            for (var i = index; i < searchResponse.Tracks.Count; i++) {
                player.GetQueue().Enqueue(searchResponse.Tracks.ElementAt(i));
            }

            await EmbedText($"{searchResponse.Tracks.Count - index} tracks added to queue", true,
                searchResponse.Playlist.Name,
                await searchResponse.Tracks.ElementAt(index).FetchArtworkAsync(),
                true);
        } else {
            //Single track queueing
            var track = searchResponse.Tracks.ElementAt(0);
            player.GetQueue().Enqueue(track);
            await EmbedText($"{track.Title}", true, TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                await track.FetchArtworkAsync(), true);
        }

        foreach (var item in test) {
            player.GetQueue().Enqueue(item);
        }
    }

    /// <summary>
    /// Helper method for playing one or more tracks based on the search terms
    /// </summary>
    /// <param name="searchResponse">The response received from the user's search</param>
    /// <param name="player">The LavaPlayer<LavaTrack> that should play this track</param>
    /// <param name="index">The index in the playlist</param>
    /// <param name="timestamp">The timestamp to start the video at</param>
    private async Task PlayNow(SearchResponse searchResponse, LavaPlayer<LavaTrack> player, int index,
        TimeSpan? timestamp = null) {
        var timestamp2 = timestamp ?? TimeSpan.Zero;
        var track = searchResponse.Tracks.ElementAt(index);

        //Play list queueing
        if (searchResponse.Type == SearchType.Playlist) {
            for (var i = index; i < searchResponse.Tracks.Count; i++) {
                if (i == 0 || i == index) {
                    await player.PlayAsync(_lavaNode, track);
                    await player.SeekAsync(_lavaNode, timestamp2);
                    await EmbedText($"Now Playing: {track.Title}", true,
                        "Duration: " + TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                        await track.FetchArtworkAsync(), true);
                } else {
                    player.GetQueue().Enqueue(searchResponse.Tracks.ElementAt(i));
                }
            }

            await EmbedText($"{searchResponse.Tracks.Count - index} tracks added to queue", true,
                searchResponse.Playlist.Name, await searchResponse.Tracks.ElementAt(index).FetchArtworkAsync(),
                true);
        } else {
            //Single Track queueing
            await player.PlayAsync(_lavaNode, track);
            await player.SeekAsync(_lavaNode, timestamp2);
            await EmbedText($"Now Playing: {track.Title}", true,
                "Duration: " + TrimTime(track.Duration.ToString(@"dd\:hh\:mm\:ss")),
                await track.FetchArtworkAsync());
        }
    }


    /// <summary>
    /// Skips the currently playing track
    /// </summary>
    [SlashCommand("skip", "Skips the currently playing Track")]
    private async Task SkipAsync([Summary("index", "The index of the track to skip")] int index = 0) {
        if (!UserInVoice().Result || !BotInVoice().Result) {
            return;
        }

        await DeferAsync();

        var player = await _lavaNode.GetPlayerAsync(Context.Guild.Id);

        if (index > 0) {
            if (player.GetQueue().Count < index - 1) {
                await EmbedText("Index is longer than the queue length", false);
                return;
            }

            await EmbedText("Track Skipped: ", false, player.GetQueue().ElementAt(index - 1).Title,
                await player.GetQueue().ElementAt(index - 1).FetchArtworkAsync());
            player.GetQueue().RemoveAt(index - 1);
            return;
        }


        var builder = new EmbedBuilder();
        builder.WithTitle("Track Skipped: ");
        builder.WithDescription(player.Track.Title);

        if (player.GetQueue().Count > 0) {
            builder.WithThumbnailUrl(await player.GetQueue().ElementAt(0).FetchArtworkAsync());
            builder.AddField(new EmbedFieldBuilder {
                Name = "Now Playing: ",
                Value = player.GetQueue().ElementAt(0).Title
            });
        } else {
            await _lavaNode.LeaveAsync(GetVoiceChannel());
            await FollowupAsync("Leaving");
            return;
        }

        builder.WithColor(3447003);
        await FollowupAsync(embed: builder.Build());

        player.GetQueue().TryDequeue(out var track);
        await player.PlayAsync(_lavaNode, track, false);
    }

    /// <summary>
    /// Command for clearing the current track queue
    /// </summary>
    [SlashCommand("clear", "Clear the current track queue")]
    private async Task ClearQueueAsync() {
        if (!UserInVoice().Result || !BotInVoice().Result) {
            return;
        }

        await DeferAsync();

        var player = await _lavaNode.GetPlayerAsync(Context.Guild.Id);

        player.GetQueue().Clear();
        await EmbedText("Queue Cleared", false);
    }

    /// <summary>
    /// Pause the currently playing track
    /// </summary>
    [SlashCommand("pause", "Pause the currently playing track")]
    private async Task PauseAsync() {
        if (!UserInVoice().Result || !BotInVoice().Result) {
            return;
        }

        await DeferAsync();

        var player = await _lavaNode.GetPlayerAsync(Context.Guild.Id);
        await player.PauseAsync(_lavaNode);
        await EmbedText("Paused", false);
    }


    /// <summary>
    /// Resume the currently playing track
    /// </summary>
    [SlashCommand("resume", "Resume the currently playing track")]
    private async Task ResumeAsync() {
        if (!UserInVoice().Result || !BotInVoice().Result) {
            return;
        }

        var player = await _lavaNode.GetPlayerAsync(Context.Guild.Id);
        if (!player.IsPaused) {
            await RespondAsync("Already Playing", ephemeral: true);
            return;
        }

        await DeferAsync();

        await player.ResumeAsync(_lavaNode, player.Track);
        await EmbedText("Resumed", false);
    }


    /// <summary>
    /// Show the track that is currently being played in this voice channel
    /// </summary>
    [SlashCommand("nowplaying", "Show the currently playing track")]
    private async Task CurrentTrackAsync() {
        if (!UserInVoice().Result || !BotInVoice().Result) {
            return;
        }

        await DeferAsync();


        var player = await _lavaNode.GetPlayerAsync(Context.Guild.Id);
        if (player.Track is null) {
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
    [SlashCommand("queue", "Display the current track queue with an optional page number")]
    private async Task QueueAsync(int page = 1) {
        if (!UserInVoice().Result || !BotInVoice().Result) {
            return;
        }

        var player = await _lavaNode.GetPlayerAsync(Context.Guild.Id);

        if (player.GetQueue().Count == 0) {
            await CurrentTrackAsync();
            return;
        }

        await DeferAsync();

        if (player.GetQueue().Count == 0) {
            await EmbedText("Queue is empty", false);
            return;
        }

        page = (page - 1) * 5;
        if (page > player.GetQueue().Count) {
            return;
        }

        //Create an embed using that image url
        var builder = new EmbedBuilder();
        builder.WithTitle($"String Music Queue - Length: {player.GetQueue().Count}");
        builder.WithThumbnailUrl(await player.Track.FetchArtworkAsync());
        builder.WithColor(3447003);
        builder.WithDescription("");


        if (page == 0) {
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
                Value = $"[{player.GetQueue().ElementAt(0).Title}]({player.GetQueue().ElementAt(0).Url})" +
                        $"\n {TrimTime(player.GetQueue().ElementAt(0).Duration.ToString(@"dd\:hh\:mm\:ss"))}"
            });


            //Remaining Queue
            for (var i = 1; i < 4 && i < player.GetQueue().Count; i++) {
                var lavaTrack = player.GetQueue().ElementAt(i);
                var fieldBuilder = new EmbedFieldBuilder {
                    Name = $"Queue position {i + 1}",
                    Value = $"[{lavaTrack.Title}]({lavaTrack.Url})" +
                            $"\n {TrimTime(lavaTrack.Duration.ToString(@"dd\:hh\:mm\:ss"))}"
                };
                builder.AddField(fieldBuilder);
            }
        } else {
            for (var i = page; i < page + 5 && i < player.GetQueue().Count; i++) {
                var lavaTrack = player.GetQueue().ElementAt(i);
                var fieldBuilder = new EmbedFieldBuilder {
                    Name = $"Queue position {i + 1}",
                    Value = $"[{lavaTrack.Title}]({lavaTrack.Url})" +
                            $"\n {TrimTime(lavaTrack.Duration.ToString(@"dd\:hh\:mm\:ss"))}"
                };
                builder.AddField(fieldBuilder);
            }
        }

        await FollowupAsync(embed: builder.Build());
    }

    /// <summary>
    /// Shuffle the current queue for the channel that the user is in
    /// </summary>
    [SlashCommand("shuffle", "Shuffle the current queue")]
    private async Task ShuffleQueue() {
        if (!UserInVoice().Result || !BotInVoice().Result) {
            return;
        }

        await DeferAsync();

        var player = await _lavaNode.GetPlayerAsync(Context.Guild.Id);
        player.GetQueue().Shuffle();
        await EmbedText("Queue Shuffled", false);
    }

    /// <summary>
    /// Enable or disable track repeating in the current server
    /// </summary>
    [SlashCommand("repeat", "Repeat the current track")]
    private async Task RepeatTrack() {
        if (!UserInVoice().Result || !BotInVoice().Result) {
            return;
        }

        await DeferAsync(ephemeral: true);
        var player = await _lavaNode.GetPlayerAsync(Context.Guild.Id);
        _musicService.RepeatTrack(player);
        await FollowupAsync("Now repeating track", ephemeral: true);
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
                { IconUrl = Context.User.GetAvatarUrl(), Name = "Added to queue" });
        builder.WithColor(3447003);
        await FollowupAsync(embed: builder.Build());
    }

    /// <summary>
    /// Check if the user sending the command is in a voice channel
    /// </summary>
    /// <returns>A Task with result true if the user is in a channel</returns>
    private async Task<bool> UserInVoice() {
        var voiceState = Context.User as IVoiceState;

        if (voiceState?.VoiceChannel != null) return true;
        await RespondAsync("You are not in a voice channel", ephemeral: true);
        return false;
    }

    /// <summary>
    /// Get the current voice channel that the user is in
    /// </summary>
    /// <returns>The IVoiceChannel the user is in</returns>
    private IVoiceChannel GetVoiceChannel() {
        var voiceState = Context.User as IVoiceState;
        return voiceState?.VoiceChannel;
    }

    /// <summary>
    /// Check if the bot is in a voice channel, responding ephemerally if it is not.
    /// </summary>
    /// <returns>True if the bot is currently in a voice channel, false otherwise.</returns>
    private async Task<bool> BotInVoice() {
        var player = await _lavaNode.TryGetPlayerAsync(GetVoiceChannel().Guild.Id);
        if (player is not null && player.State.IsConnected == false) return true;
        await RespondAsync("The bot is not in a voice channel", ephemeral: true);
        return false;
    }

    /// <summary>
    /// Trim the start of the time string to remove any leading "00:" sections
    /// Leaves at least 0:XX as a default
    /// </summary>
    /// <param name="time">The string time to be trimmed</param>
    /// <returns>A trimmed string containing nice formatting</returns>
    public static string TrimTime(string time) {
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


    /// <summary>
    /// Display the number of Gorilla moments that each member has had.
    /// </summary>
    [SlashCommand("refreshresponses", "Refresh the Dota 2 Responses Database")]
    [RequireOwner]
    public async Task RefreshResponses() {
        await DeferAsync(ephemeral: true);
        await ResponseUtils.RefreshResponses(_httpClient, _applicationContext);
        await FollowupAsync("Responses Refreshed", ephemeral: true);
    }

    /// <summary>
    /// Search for and try to play a Dota 2 response
    /// </summary>
    [SlashCommand("dota2response", "Search for and try to play a Dota 2 response")]
    public async Task Dota2Response([Summary("query", "The query to search for")] string query) {
        await DeferAsync(ephemeral: true);

        if ((Context.User as IVoiceState)?.VoiceChannel == null) {
            await FollowupAsync("You must be in a voice channel to use this command", ephemeral: true);
            return;
        }


        var response = ResponseUtils.GetResponse(query, _applicationContext);
        if (response is null) {
            await FollowupAsync("No response found for that query", ephemeral: true);
        } else {
            await PlayLink(response.Url);
            await FollowupAsync($"{response.ResponseText} - {response.Hero.Name}", ephemeral: true);
        }
    }

    /// <summary>
    /// Play a given link as an audio file, storing the currently playing track and queue to be restored after the
    /// link finishes playing.
    /// </summary>
    /// <param name="url">The audio url to play</param>
    private async Task PlayLink(string url) {
        var player = await _lavaNode.TryGetPlayerAsync(GetVoiceChannel().Guild.Id);
        if (player is null || player.State.IsConnected == false) {
            await JoinAsync();
            player = await _lavaNode.GetPlayerAsync(GetVoiceChannel().Guild.Id);
        }

        var track = await _lavaNode.LoadTrackAsync(url);

        // If there is a queue, store it temporarily and restore afterwards
        var backup = new List<LavaTrack>();
        backup.AddRange(player.GetQueue().RemoveRange(0, player.GetQueue().Count));
        player.GetQueue().Clear();

        // If there is a track currently playing, pause it and store it temporarily
        var currentTrack = player.Track;

        await player.PlayAsync(_lavaNode, track.Tracks.ElementAt(0), noReplace: false);
        _musicService.Requeue = backup;
        _musicService.RequeueCurrentTrack = currentTrack;
    }
}