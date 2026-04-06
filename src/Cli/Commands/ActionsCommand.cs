// <copyright file="ActionsCommand.cs" company="Luciano Spiguel">
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
/// Implements the <c>actions</c> CLI sub-command, which performs a dry-run: it scans processes,
/// evaluates rules, and displays what actions <em>would</em> be taken (including safety-guard blocks)
/// without executing anything.
/// </summary>
public static class ActionsCommand
{
    private static readonly JsonSerializerOptions JsonOutputOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Command Create(
        IServiceProvider services,
        Option<FileInfo?> configOption,
        Option<string> outputOption,
        Option<string?> ruleSetOption)
    {
        var cmd = new Command("actions", "Show what actions would be taken (dry-run)");
        cmd.SetHandler(async context =>
        {
            var configFile = context.ParseResult.GetValueForOption(configOption);
            var outputType = context.ParseResult.GetValueForOption(outputOption) ?? "console";
            var ruleSetName = context.ParseResult.GetValueForOption(ruleSetOption);
            var service = (IActionsService)services.GetService(typeof(IActionsService))!;
            context.ExitCode = await HandleAsync(configFile?.FullName, outputType, ruleSetName, service);
        });
        return cmd;
    }

    public static Task<int> HandleAsync(
        string? configPath,
        string outputType,
        string? ruleSetName,
        IActionsService actionsService)
    {
        var result = actionsService.GetActions(configPath, ruleSetName);

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

        if (result.ResolvedRuleSetName is not null)
        {
            AnsiConsole.MarkupLine($"[grey]Using ruleset: {Markup.Escape(result.ResolvedRuleSetName)}[/]");
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Process");
        table.AddColumn("Service");
        table.AddColumn("Matched Rule");
        table.AddColumn("Action");
        table.AddColumn("Blocked");
        table.AddColumn("Reason");

        foreach (var plan in result.Plans)
        {
            var actionColor = plan.Action is ActionType.Kill or ActionType.Stop ? "red" : "yellow";
            table.AddRow(
                Markup.Escape(plan.ProcessName),
                Markup.Escape(plan.ServiceName ?? string.Empty),
                Markup.Escape(plan.RuleId),
                $"[{actionColor}]{plan.Action}[/]",
                plan.Blocked ? "[red]Yes[/]" : "[green]No[/]",
                Markup.Escape(plan.BlockReason ?? string.Empty));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{result.Plans.Count} planned action(s).[/]");

        return Task.FromResult(result.ExitCode);
    }
}
