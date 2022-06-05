﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GoogleApi;
using GoogleApi.Entities.Search.Image.Request;
using Stringdicator.Database;

namespace Stringdicator.Modules {
    public class ExtraModule : InteractionModuleBase<SocketInteractionContext> {
        private static ApplicationContext _applicationContext;

        /// <summary>
        /// Constructor for ExtraModule to be able to access the database using a Database Context
        /// </summary>
        /// <param name="applicationContext">The User Database Context</param>
        public ExtraModule(ApplicationContext applicationContext) {
            _applicationContext = applicationContext;
        }

        /// <summary>
        /// Display all violations of users in the current guild/server
        /// </summary>
        [SlashCommand("violations", "Shows number of No Anime violations for each member of this server")]
        public async Task Violations() {
            await DeferAsync();

            var builder = new EmbedBuilder();
            builder.WithTitle("No Anime Violations: ");
            builder.WithColor(3447003);
            builder.WithDescription("");

            // Get all of the users in a given channel and get their violations from the file if applicable
            // Special case for threads as these are handled differently and generally would return the members of the
            // parent channel instead of the thread.
            IEnumerable<IUser> usersInChannel;
            if (Context.Channel is SocketThreadChannel threadChannel) {
                usersInChannel = await threadChannel.GetUsersAsync();
            } else {
                usersInChannel = await Context.Channel.GetUsersAsync().FlattenAsync();
            }
            
            var userIds = usersInChannel.Select(x => x.Id);
            var users = _applicationContext.Users.Where(x => userIds.Contains(x.Id)).ToList();
            
            users.Sort((user1, user2) => user2.Violations.CompareTo(user1.Violations));
            var line = "";

            foreach (var user in users) {
                line += $"{Context.Guild.GetUser(user.Id).Username}";
                line += $" - {user.Violations}\n";
            }

            builder.WithDescription(line);
            if (line.Equals("")) {
                builder.Title = "No Violations Recorded.";
            }
            
            //Send message
            await FollowupAsync(embed: builder.Build());
        }


        /// <summary>
        /// Record a No Anime violation for the given user
        /// </summary>
        /// <param name="user">The user to receive the violation</param>
        [SlashCommand("noanime", "Record a No Anime Violation for the given user")]
        public async Task NoAnimeAsync(
            [Summary("user", "The user to record the violation for")]
            IUser user) {
            await DeferAsync();
            var builder = await NoAnime(Context.Guild, user);
            await FollowupAsync(embed: builder.Build());
        }

        
        /// <summary>
        /// Create an Embed for the No Anime Violation based on the given user
        /// Will search Google for a relevant Image and construct the message to send back to the server
        /// </summary>
        /// <param name="guild">The guild that the user is in</param>
        /// <param name="user">The user receiving the violation</param>
        /// <returns></returns>
        public static async Task<EmbedBuilder> NoAnime(SocketGuild guild, IUser user) {
            var builder = new EmbedBuilder();

            var unused = guild.DownloadUsersAsync();

            var request = new ImageSearchRequest {
                Query = "No Anime",
                Key = Environment.GetEnvironmentVariable("API_KEY"),
                SearchEngineId = Environment.GetEnvironmentVariable("SEARCH_ENGINE_ID")
            };

            //Gets the search response - contains info about search
            var response =
                await GoogleSearch.ImageSearch.QueryAsync(request);

            //Pick a random search result
            var items = response.Items.ToArray();
            var random = new Random();
            var item = items[random.Next(0, items.Length)];


            var dbUser = await _applicationContext.Users.FindAsync(user.Id);
            if (dbUser is null) {
                dbUser = new User {
                    Id = user.Id,
                    Violations = 1
                };
                _applicationContext.Users.Add(dbUser);
            } else {
                dbUser.Violations++;
                _applicationContext.Users.Update(dbUser);
            }

            await _applicationContext.SaveChangesAsync();
            builder.WithTitle("A No Anime Violation has been recorded for: \n" + user.Username);
            builder.WithColor(3447003);
            builder.WithDescription("Current violations for this user: " + dbUser.Violations); // TODO FOR THIS SERVER?
            builder.WithThumbnailUrl(item.Link);

            return builder;
        }
    }
}