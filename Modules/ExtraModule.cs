using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GoogleApi;
using GoogleApi.Entities.Search.Image.Request;

namespace Stringdicator.Modules {
    public class ExtraModule : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("violations", "Shows number of No Anime violations for each member of this server")]
        public async Task Violations() {
            if (!File.Exists("Violations.xml")) {
                await RespondAsync("No Violations Recorded");
                return;
            }


            var builder = new EmbedBuilder();
            builder.WithTitle("No Anime Violations: ");
            builder.WithColor(3447003);
            builder.WithDescription("");


            var stream = File.Open("Violations.xml", FileMode.OpenOrCreate);
            var document = new XmlDocument();
            document.Load(stream);
            var list = document.GetElementsByTagName("user");
            var users = new Dictionary<ulong, int>();

            for (var i = 0; i < list.Count; i++) {
                var element = list.Item(i);
                var id = Convert.ToUInt64(element.Attributes.GetNamedItem("id").Value);
                var num = Convert.ToInt32(element.Attributes.GetNamedItem("violations").Value);
                if (users.ContainsKey(id)) {
                    users[id] += num;
                } else {
                    users.Add(id, num);
                }
            }

            stream.Close();

            var userList = users.ToList();
            userList.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
            var line = "";
            foreach (var (key, value) in userList) {
                if (Context.Guild.GetUser(key) == null) continue;
                line += $"{Context.Guild.GetUser(key).Username}";
                line += $" - {value}\n";
            }

            builder.WithDescription(line);


            //Send message
            await RespondAsync(embed: builder.Build());
        }


        [SlashCommand("noanime", "Record a No Anime Violation for the given user")]
        public async Task NoAnimeAsync(
            [Summary("user", "The user to record the violation for")]
            IUser user) {
            var builder = await NoAnime(Context.Guild, user);
            await RespondAsync(embed: builder.Build());
        }

        public static async Task<EmbedBuilder> NoAnime(SocketGuild guild, IUser user) {
            if (!File.Exists("Violations.xml")) {
                //Create file
                var doc = new XmlDocument();
                var root = doc.CreateElement("guilds");
                doc.AppendChild(root);
                doc.Save("Violations.xml");
            }

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

            var num = LoadViolations(guild, user.Id);
            builder.WithTitle("A No Anime Violation has been recorded for: \n" + user.Username);
            builder.WithColor(3447003);
            builder.WithDescription("Current violations for this server: " + num);
            builder.WithThumbnailUrl(item.Link);

            return builder;
        }

        /**
         * Load the violations from the xml file
         */
        private static int LoadViolations(SocketGuild guild, ulong id) {
            var num = 0;
            var stream = File.Open("Violations.xml", FileMode.OpenOrCreate);
            var document = new XmlDocument();
            document.Load(stream);
            var root = document.DocumentElement;
            if (root != null) {
                var guilds = root.GetElementsByTagName("guild");
                for (var i = 0; i < guilds.Count; i++) {
                    var guildNode = guilds.Item(i);
                    if (!Convert.ToUInt64(guildNode?.Attributes?.GetNamedItem("id").Value).Equals(guild.Id))
                        continue;

                    //Have correct guild
                    for (var j = 0; j < guildNode?.ChildNodes.Count; j++) {
                        var user = guildNode.ChildNodes.Item(j);
                        if (id != Convert.ToUInt64(user?.Attributes?.GetNamedItem("id").Value)) continue;
                        if (user is { Attributes: { } })
                            num = Convert.ToInt32(user.Attributes.GetNamedItem("violations").Value) + 1;
                        if (user is { Attributes: { } })
                            user.Attributes.GetNamedItem("violations").Value = Convert.ToString(num);
                        goto exit;
                    }

                    //Didn't find user;
                    var newUser = NewUser(document, id);
                    guildNode.AppendChild(newUser);
                    num = 1;
                    goto exit;
                }

                //Couldn't find the guild
                var newGuild = document.CreateNode(XmlNodeType.Element, "guild", "");
                newGuild.Attributes.Append(document.CreateAttribute("id"));
                newGuild.Attributes.GetNamedItem("id").Value = guild.Id.ToString();

                var newUser2 = NewUser(document, id);
                newGuild.AppendChild(newUser2);
                root.AppendChild(newGuild);
                num = 1;
            }

            exit:
            stream.Close();

            stream = File.Open("Violations.xml", FileMode.Truncate);
            document.Save(stream);
            stream.Close();
            return num;
        }

        /**
         * Create and return a new user element 
         */
        private static XmlNode NewUser(XmlDocument document, ulong id) {
            var newUser = document.CreateNode(XmlNodeType.Element, "user", "");
            newUser.Attributes.Append(document.CreateAttribute("id"));
            newUser.Attributes.GetNamedItem("id").Value = id.ToString();

            newUser.Attributes.Append(document.CreateAttribute("violations"));
            newUser.Attributes.GetNamedItem("violations").Value = 1.ToString();
            return newUser;
        }
    }
}