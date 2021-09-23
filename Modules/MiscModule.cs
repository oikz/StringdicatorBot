using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        [Alias("SBL")]
        private async Task BlacklistChannelAsync() {
            var stream = File.Open("Blacklist.xml", FileMode.Open);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            var channels = document.Element("Channels");
            
            //Check for this channel already being in the file
            var thisChannel =
                channels?.Nodes().FirstOrDefault(node => ((XElement) node).Value.Equals(Context.Channel.Id.ToString()));
            
            //If this channel is already blacklisted, un-blacklist it
            if (thisChannel != null) {
                thisChannel.Remove();
            } else {
                //Blacklist this channel
                channels?.Add(new XElement("Channel", Context.Channel.Id));
            }

            //Cleanup and save
            stream.Close();
            document.Save("Blacklist.xml");
        }
    }
}