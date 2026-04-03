// <copyright file="HelpCommand.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.CommandLine;
using Spectre.Console;

namespace SystemFitnessHelper.Cli.Commands;

/// <summary>
/// Implements the <c>help</c> CLI sub-command.
/// Without an argument it lists every available command; when given a command name
/// it prints that command's description and all its options (including global options).
/// </summary>
public static class HelpCommand
{
    /// <summary>
    /// Creates the <c>help</c> command, wired to the provided sub-command registry and global options.
    /// </summary>
    /// <param name="subCommands">All other registered sub-commands to document.</param>
    /// <param name="globalOptions">Root-level global options (e.g. --config, --verbose) shown at the bottom of per-command output.</param>
    public static Command Create(
        IReadOnlyList<Command> subCommands,
        IReadOnlyList<Option> globalOptions)
    {
        var commandArg = new Argument<string?>(
            name: "command",
            getDefaultValue: () => null,
            description: "Name of the command to show detailed help for (e.g. execute, list, config, actions)");
        commandArg.Arity = ArgumentArity.ZeroOrOne;

        var cmd = new Command("help", "Show help for a specific command, or list all commands");
        cmd.AddArgument(commandArg);
        cmd.SetHandler(context =>
        {
            var commandName = context.ParseResult.GetValueForArgument(commandArg);
            Handle(commandName, subCommands, globalOptions);
        });
        return cmd;
    }

    private static void Handle(
        string? commandName,
        IReadOnlyList<Command> subCommands,
        IReadOnlyList<Option> globalOptions)
    {
        if (commandName is null)
        {
            PrintAllCommands(subCommands);
            return;
        }

        var match = subCommands.FirstOrDefault(
            c => string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Unknown command '[yellow]{Markup.Escape(commandName)}[/]'. " +
                "Run [grey]sfhcli help[/] to list available commands.");
            return;
        }

        PrintCommandHelp(match, globalOptions);
    }

    private static void PrintAllCommands(IReadOnlyList<Command> subCommands)
    {
        AnsiConsole.MarkupLine("[bold]sfhcli[/] — System Fitness Helper\n");
        AnsiConsole.MarkupLine("Usage: [grey]sfhcli <command> [[options]][/]\n");

        var table = new Table().Border(TableBorder.None).Expand();
        table.AddColumn(new TableColumn("[bold]Command[/]") { Width = 14 });
        table.AddColumn("[bold]Description[/]");

        foreach (var cmd in subCommands)
        {
            table.AddRow(
                $"[cyan]{Markup.Escape(cmd.Name)}[/]",
                Markup.Escape(cmd.Description ?? string.Empty));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\nRun [grey]sfhcli help <command>[/] for detailed options.");
    }

    private static void PrintCommandHelp(Command cmd, IReadOnlyList<Option> globalOptions)
    {
        AnsiConsole.MarkupLine($"[bold]sfhcli {Markup.Escape(cmd.Name)}[/]");
        AnsiConsole.MarkupLine(Markup.Escape(cmd.Description ?? string.Empty));
        AnsiConsole.WriteLine();

        var ownOptions = cmd.Options.Where(o => !o.IsHidden).ToList();
        if (ownOptions.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold underline]Options[/]");
            AnsiConsole.Write(BuildOptionTable(ownOptions));
            AnsiConsole.WriteLine();
        }

        if (globalOptions.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold underline]Global Options[/]");
            AnsiConsole.Write(BuildOptionTable(globalOptions));
        }
    }

    private static Table BuildOptionTable(IEnumerable<Option> options)
    {
        var table = new Table().Border(TableBorder.None).HideHeaders().Expand();
        table.AddColumn(new TableColumn(string.Empty) { Width = 30, NoWrap = true });
        table.AddColumn(new TableColumn(string.Empty));

        foreach (var opt in options)
        {
            var aliases = string.Join(", ", opt.Aliases.OrderByDescending(a => a.Length));
            table.AddRow(
                $"  [cyan]{Markup.Escape(aliases)}[/]",
                Markup.Escape(opt.Description ?? string.Empty));
        }

        return table;
    }
}
