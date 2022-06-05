using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Stringdicator.Database;

namespace Stringdicator.Modules {
    /// <summary>
    /// Module for miscellaneous commands that users can use
    /// </summary>
    [Discord.Commands.Summary("Miscellaneous Server Commands")]
    public class MiscModule : InteractionModuleBase<SocketInteractionContext> {
        
        private static ApplicationContext _applicationContext;

        /// <summary>
        /// Constructor for MiscModule to be able to access the database using a Database Context
        /// </summary>
        /// <param name="applicationContext">The Database Context</param>
        public MiscModule(ApplicationContext applicationContext) {
            _applicationContext = applicationContext;
        }
        
        /// <summary>
        /// Blacklist a channel from being accessible to commands
        /// Using the command again un-blacklists the channel
        /// </summary>
        [SlashCommand("blacklist", "Blacklist this channel from receiving commands")]
        private async Task BlacklistChannelAsync() {
            await DeferAsync();
            
            var builder = new EmbedBuilder();
            builder.WithDescription("");
            builder.WithColor(3447003);
            
            var channel = await _applicationContext.Channels.FindAsync(Context.Channel.Id);
            if (channel is null) {
                _applicationContext.Channels.Add(new Channel {
                    Id = Context.Channel.Id,
                    Blacklisted = true
                });
                builder.WithTitle("Channel Blacklisted");
            } else if (channel.Blacklisted) {
                channel.Blacklisted = false;
                builder.WithTitle("Channel Unblacklisted");
            } else {
                channel.Blacklisted = true;
                builder.WithTitle("Channel Blacklisted");
            }
            
            await _applicationContext.SaveChangesAsync();
            
            await FollowupAsync(embed: builder.Build());
        }
        
        
        /// <summary>
        /// Blacklist a channel from being accessible to commands
        /// Using the command again un-blacklists the channel
        /// </summary>
        [SlashCommand("blacklistimages", "Blacklist this channel from reacting to images")]
        private async Task BlacklistChannelImageAsync() {
            await DeferAsync();
            
            var builder = new EmbedBuilder();
            builder.WithDescription("");
            builder.WithColor(3447003);
            
            var channel = await _applicationContext.Channels.FindAsync(Context.Channel.Id);
            if (channel is null) {
                _applicationContext.Channels.Add(new Channel {
                    Id = Context.Channel.Id,
                    ImageBlacklisted = true
                });
                builder.WithTitle("Channel Images Blacklisted");
            } else if (channel.ImageBlacklisted) {
                channel.ImageBlacklisted = false;
                builder.WithTitle("Channel Images Unblacklisted");
            } else {
                channel.ImageBlacklisted = true;
                builder.WithTitle("Channel Images Blacklisted");
            }

            await _applicationContext.SaveChangesAsync();

            await FollowupAsync(embed: builder.Build());
        }
    }
}