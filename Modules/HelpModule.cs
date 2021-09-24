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
    [Summary("Help Commands")]
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
        /// Listed commands are affected by the moduleNumber as a page number
        /// </summary>
        /// <param name="moduleNumber">The page number the user wishes to view</param>
        [Command("Stringdicator")]
        [Summary("List Modules or View Specific Module's Commands")]
        [Alias("StringHelp")]
        private async Task HelpAsync([Remainder] int moduleNumber = -1) {
            moduleNumber--;
            
            //Kinda jank but gets all the commands again
            var commands = new CommandService();
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(),
                _serviceProvider);
            var builder = new EmbedBuilder();
            builder.WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl());
            builder.WithColor(3447003);
            builder.WithDescription("");
            
            if (moduleNumber < 0) {
                //Display the list of modules
                builder.WithTitle("Available Stringdicator Modules: ");
                for (var i = 0; i < commands.Modules.Count(); i++) {
                    builder.AddField(new EmbedFieldBuilder
                        {Name = $"{i + 1} - {commands.Modules.ElementAt(i).Name}", Value = commands.Modules.ElementAt(i).Summary});
                }
            } else {

                //Get the module chosen by the user
                var module = commands.Modules.ElementAt(moduleNumber);


                //Embed builder
                builder.WithTitle($"All Stringdicator Commands. {module.Name.Replace("Module", " Commands")}");


                //Show each module's commands separately
                foreach (var command in module.Commands) {
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

                    var fieldBuilder = new EmbedFieldBuilder
                        {Name = $"{command.Name} {aliases}", Value = command.Summary};
                    //line += $"**{command.Name} {aliases}** - {command.Summary}\n";
                    builder.AddField(fieldBuilder);
                }
            }

            await ReplyAsync("", false, builder.Build());
        }
    }
}