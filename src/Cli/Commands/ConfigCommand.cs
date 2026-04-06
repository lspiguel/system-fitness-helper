// <copyright file="ConfigCommand.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Cli.Commands;

/// <summary>
/// Implements the <c>config</c> CLI sub-command, which loads the rule file, validates it,
/// and renders the parsed rules together with any validation errors or warnings to the console.
/// </summary>
public static class ConfigCommand
{
    private static readonly JsonSerializerOptions JsonOutputOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Command Create(IServiceProvider services, Option<FileInfo?> configOption, Option<string> outputOption)
    {
        var cmd = new Command("config", "Load, validate, and display the rule file");
        cmd.SetHandler(async context =>
        {
            var configFile = context.ParseResult.GetValueForOption(configOption);
            var outputType = context.ParseResult.GetValueForOption(outputOption) ?? "console";
            var service = (IConfigService)services.GetService(typeof(IConfigService))!;
            context.ExitCode = await HandleAsync(configFile?.FullName, outputType, service);
        });
        return cmd;
    }

    public static Task<int> HandleAsync(string? configPath, string outputType, IConfigService configService)
    {
        var result = configService.GetConfig(configPath);

        if (outputType.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOutputOptions));
            return Task.FromResult(result.ExitCode);
        }

        if (result.ErrorMessage is not null)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(result.ErrorMessage)}");
            return Task.FromResult(result.ExitCode);
        }

        if (result.RuleSet is not null)
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("ID");
            table.AddColumn("Enabled");
            table.AddColumn("Action");
            table.AddColumn("Conditions");
            table.AddColumn("Description");

            foreach (var rule in result.RuleSet.Rules)
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

            if (result.RuleSet.Protected.Count > 0)
            {
                AnsiConsole.MarkupLine(
                    $"[grey]Protected services: {Markup.Escape(string.Join(", ", result.RuleSet.Protected))}[/]");
            }
        }

        foreach (var err in result.Validation.Errors)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(err)}");
        }

        foreach (var warn in result.Validation.Warnings)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(warn)}");
        }

        return Task.FromResult(result.ExitCode);
    }
}
