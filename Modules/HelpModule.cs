using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Stringdicator.Modules {

    /// <summary>
    /// Module containing the base help command that displays all available commands
    /// </summary>
    public class HelpModule : ModuleBase<SocketCommandContext> {
        private readonly ServiceProvider _serviceProvider;

        /// <summary>
        /// Constructor for the Help Module for saving the serviceProvider for command displaying
        /// </summary>
        /// <param name="serviceProvider">The serviceProvider used in the rest of the project</param>
        public HelpModule(ServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }
        
        /// <summary>
        /// Outputs an embed containing up to 5 of the command supported
        /// Listed commands are affected by the offset as a page number
        /// </summary>
        /// <param name="offset">The page number the user wishes to view</param>
        [Command("Stringdicator")]
        [Summary("Outputs all commands with an optional page number argument")]
        [Alias("StringHelp")]
        private async Task HelpAsync([Remainder] int offset = 1) {
            //Kinda jank but gets all the commands again
            var commands = new CommandService();
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(),
                _serviceProvider);

            //Embed builder
            var builder = new EmbedBuilder();
            builder.WithTitle($"All Stringdicator Commands. Page {offset}");
            builder.WithThumbnailUrl("attachment://string.jpg");
            builder.WithColor(3447003);
            builder.WithDescription("");

            offset = (offset - 1) * 5;
            if (offset > commands.Commands.Count()) {
                return;
            }

            //Add all commands, up to 5
            for (var i = offset; i < offset + 5; i++) {
                if (i >= commands.Commands.Count()) {
                    break;
                }

                var command = commands.Commands.ElementAt(i);
                
                //Get all the aliases for each command and output them next to the title of the command
                var aliasBuilder = new StringBuilder();
                foreach (var alias in command.Aliases) {
                    if (alias.Equals(command.Name.ToLower())) continue;
                    if (aliasBuilder.ToString() == "") {
                        aliasBuilder.Append(alias);
                        continue;
                    }

                    aliasBuilder.Append($", {alias}");
                }
                //Add square brackets around any aliases
                var aliases = aliasBuilder.ToString().Any() ? $"[{aliasBuilder}]" : "";
                
                
                var fieldBuilder = new EmbedFieldBuilder {Name = $"{command.Name} {aliases}", Value = command.Summary};
                builder.AddField(fieldBuilder);
            }

            await ReplyAsync("", false, builder.Build());
        }
    }
}