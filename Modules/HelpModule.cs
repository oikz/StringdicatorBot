using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Stringdicator.Modules; 

/// <summary>
/// Module containing the base help command that displays all available commands
/// </summary>
public class HelpModule : InteractionModuleBase<SocketInteractionContext> {
    private readonly DiscordSocketClient _discordClient;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Constructor for the Help Module for saving the serviceProvider for command displaying
    /// </summary>
    /// <param name="discordClient">The discordClient used in this module for displaying commands</param>
    /// <param name="serviceProvider">The serviceProvider used in this module for displaying commands</param>
    public HelpModule(DiscordSocketClient discordClient, IServiceProvider serviceProvider) {
        _serviceProvider = serviceProvider;
        _discordClient = discordClient;
    }

    /// <summary>
    /// Outputs an embed containing up to 5 of the command supported
    /// Listed commands are affected by the moduleNumber as a page number
    /// </summary>
    /// <param name="moduleNumber">The page number the user wishes to view</param>
    [SlashCommand("stringdicator", "Get the help command for the bot")]
    private async Task HelpAsync([Summary("Page", "The page number you wish to view")] int moduleNumber = -1) {
        moduleNumber--;

        //Kinda jank but gets all the commands again
        var commands = new InteractionService(_discordClient);
        await commands.AddModulesAsync(Assembly.GetEntryAssembly(),
            _serviceProvider);
        var builder = new EmbedBuilder();
        builder.WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl());
        builder.WithColor(3447003);
        builder.WithDescription("");

        if (moduleNumber < 0) {
            //Display the list of modules
            builder.WithTitle("Available Stringdicator Modules: ");
            var description = "";
            for (var i = 0; i < commands.Modules.Count; i++) {
                description += $"{i + 1} - {commands.Modules.ElementAt(i).Name}\n";
            }

            builder.WithDescription(description);
        } else {
            if (moduleNumber >= commands.Modules.Count) {
                await RespondAsync("That module does not exist");
                return;
            }
            //Get the module chosen by the user
            var module = commands.Modules.ElementAt(moduleNumber);


            //Embed builder
            builder.WithTitle($"{module.Name.Replace("Module", " Commands")}");


            //Show each module's commands separately
            foreach (var command in module.SlashCommands) {
                var fieldBuilder = new EmbedFieldBuilder
                    { Name = $"{command.Name}", Value = command.Description };
                builder.AddField(fieldBuilder);
            }
        }

        await RespondAsync(embed: builder.Build());
    }
}