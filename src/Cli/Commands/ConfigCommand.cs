// <copyright file="ConfigCommand.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Spectre.Console;
using System.CommandLine;
using SystemFitnessHelper.Configuration;

namespace SystemFitnessHelper.Cli.Commands;

/// <summary>
/// Implements the <c>config</c> CLI sub-command, which loads the rule file, validates it,
/// and renders the parsed rules together with any validation errors or warnings to the console.
/// </summary>
public static class ConfigCommand
{
    public static Command Create(IServiceProvider _, Option<FileInfo?> configOption)
    {
        var cmd = new Command("config", "Load, validate, and display the rule file");
        cmd.SetHandler(async context =>
        {
            var configFile = context.ParseResult.GetValueForOption(configOption);
            context.ExitCode = await HandleAsync(configFile?.FullName);
        });
        return cmd;
    }

    public static Task<int> HandleAsync(string? configPath)
    {
        var path = ConfigurationLoader.DiscoverPath(configPath);
        if (path is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No rules.json found. Use --config to specify a path.");
            return Task.FromResult(2);
        }

        var (ruleSet, validation) = ConfigurationLoader.Load(path);

        if (ruleSet is not null)
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("ID");
            table.AddColumn("Enabled");
            table.AddColumn("Action");
            table.AddColumn("Conditions");
            table.AddColumn("Description");

            foreach (var rule in ruleSet.Rules)
            {
                var conditions = string.Join(", ", rule.Conditions.Select(c => $"{c.Field} {c.Op} '{c.Value}'"));
                var enabledMark = rule.Enabled ? "[green]✓[/]" : "[grey]✗[/]";
                var actionColor = rule.Action is ActionType.Kill or ActionType.Stop ? "red" : "yellow";
                table.AddRow(
                    Markup.Escape(rule.Id),
                    enabledMark,
                    $"[{actionColor}]{rule.Action}[/]",
                    Markup.Escape(conditions),
                    Markup.Escape(rule.Description ?? string.Empty));
            }

            AnsiConsole.Write(table);

            if (ruleSet.Protected.Count > 0)
            {
                AnsiConsole.MarkupLine(
                    $"[grey]Protected services: {Markup.Escape(string.Join(", ", ruleSet.Protected))}[/]");
            }
        }

        foreach (var err in validation.Errors)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(err)}");
        }

        foreach (var warn in validation.Warnings)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(warn)}");
        }

        return Task.FromResult(validation.IsValid ? 0 : 2);
    }
}
