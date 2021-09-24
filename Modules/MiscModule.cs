using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Discord;
using Discord.Commands;

namespace Stringdicator.Modules {
    /// <summary>
    /// Module for miscellaneous commands that users can use
    /// </summary>
    public class MiscModule : ModuleBase<SocketCommandContext> {
        /// <summary>
        /// Blacklist a channel from being accessible to commands
        /// Using the command again un-blacklists the channel
        /// </summary>
        [Command("StringBlacklist")]
        [Summary("Blacklist this channel from receiving commands")]
        [Alias("SB")]
        private async Task BlacklistChannelAsync() {
            var stream = File.Open("Blacklist.xml", FileMode.Open);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            var channels = document.Element("Channels");
            
            //Check for this channel already being in the file
            var thisChannel =
                channels?.Nodes().FirstOrDefault(node => ((XElement) node).Value.Equals(Context.Channel.Id.ToString()));
            
            var builder = new EmbedBuilder();
            builder.WithDescription("");
            builder.WithColor(3447003);
            //If this channel is already blacklisted, un-blacklist it
            if (thisChannel != null) {
                builder.WithTitle("Channel Unblacklisted");
                thisChannel.Remove();
            } else {
                //Blacklist this channel
                builder.WithTitle("Channel Blacklisted");
                channels?.Add(new XElement("Channel", Context.Channel.Id));
            }

            await ReplyAsync("", false, builder.Build());

            //Cleanup and save
            stream.Close();
            document.Save("Blacklist.xml");
        }
        
        
        /// <summary>
        /// Blacklist a channel from being accessible to commands
        /// Using the command again un-blacklists the channel
        /// </summary>
        [Command("StringBlacklistImages")]
        [Summary("Blacklist this channel from reacting to images")]
        [Alias("SBI")]
        private async Task BlacklistChannelImageAsync() {
            var stream = File.Open("BlacklistImages.xml", FileMode.Open);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            var channels = document.Element("Channels");
            
            //Check for this channel already being in the file
            var thisChannel =
                channels?.Nodes().FirstOrDefault(node => ((XElement) node).Value.Equals(Context.Channel.Id.ToString()));
            
            var builder = new EmbedBuilder();
            builder.WithDescription("");
            builder.WithColor(3447003);
            //If this channel is already blacklisted, un-blacklist it
            if (thisChannel != null) {
                builder.WithTitle("Images Unblacklisted");
                thisChannel.Remove();
            } else {
                //Blacklist this channel
                builder.WithTitle("Images Blacklisted");
                channels?.Add(new XElement("Channel", Context.Channel.Id));
            }

            await ReplyAsync("", false, builder.Build());

            //Cleanup and save
            stream.Close();
            document.Save("BlacklistImages.xml");
        }
    }
}