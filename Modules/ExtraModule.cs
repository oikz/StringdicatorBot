using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

            foreach (var user in users.Where(user => user.Violations != 0)) {
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
        public async Task NoAnimeAsync([Summary("user", "The user to record the violation for")] IUser user) {
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
        public static async Task<EmbedBuilder> NoAnime(IGuild guild, IUser user) {
            var builder = new EmbedBuilder();

            await guild.DownloadUsersAsync();

            var request = new ImageSearchRequest {
                Query = "No Anime",
                Key = Environment.GetEnvironmentVariable("API_KEY"),
                SearchEngineId = Environment.GetEnvironmentVariable("SEARCH_ENGINE_ID")
            };

            //Gets the search response - contains info about search
            var response = await GoogleSearch.ImageSearch.QueryAsync(request);

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


        /// <summary>
        /// Display the number of Gorilla moments that each member has had.
        /// </summary>
        [SlashCommand("gorillamoments", "Shows number of times Gorilla moments for each member of this server")]
        public async Task GorillaMoments() {
            await DeferAsync();

            var builder = new EmbedBuilder();
            builder.WithTitle("Gorilla Moments: ");
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

            users.Sort((user1, user2) => user2.GorillaMoments.CompareTo(user1.GorillaMoments));
            var line = "";

            foreach (var user in users.Where(user => user.GorillaMoments != 0)) {
                line += $"{Context.Guild.GetUser(user.Id).Username}";
                line += $" - {user.GorillaMoments}\n";
            }

            builder.WithDescription(line);
            if (line.Equals("")) {
                builder.Title = "No Gorilla Moments Recorded.";
            }

            //Send message
            await FollowupAsync(embed: builder.Build());
        }

        /// <summary>
        /// Add or remove one gorilla to a User's total count of gorilla moments
        /// Synchronised to ensure that the database values are properly updated in the event that it gets spammed.
        /// </summary>
        /// <param name="userId">The user id of the user to be gorilla momented</param>
        /// <param name="message">The message that was gorilla'd</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void AddGorilla(ulong userId, IUserMessage message) {
            var dbUser = _applicationContext.Users.Find(userId);
            if (dbUser is null) {
                dbUser = new User {
                    Id = userId,
                    GorillaMoments = 1
                };
                _applicationContext.Users.Add(dbUser);
            } else {
                dbUser.GorillaMoments += 1;
                if (dbUser.GorillaMoments < 0) dbUser.GorillaMoments = 0;
                _applicationContext.Users.Update(dbUser);
            }

            // Mark the message as already gorilla'd
            message.AddReactionAsync(new Emoji("🦍"));
            _applicationContext.SaveChanges();
        }
    }
}