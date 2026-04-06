// <copyright file="ListCommand.cs" company="Luciano Spiguel">
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
/// Implements the <c>list</c> CLI sub-command, which enumerates all running processes and
/// highlights matched ones in yellow (non-destructive action) or red (Kill/Stop/Suspend).
/// No actions are executed.
/// </summary>
public static class ListCommand
{
    private static readonly JsonSerializerOptions JsonOutputOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Command Create(IServiceProvider services, Option<FileInfo?> configOption, Option<string> outputOption)
    {
        var cmd = new Command("list", "Enumerate processes and highlight matched ones");
        var formatOption = new Option<string>(
            aliases: ["--format", "-f"],
            description: "Output format: 'table' (default) shows process list; 'template' generates a RuleSet JSON template.");
        formatOption.SetDefaultValue("table");
        cmd.AddOption(formatOption);

        cmd.SetHandler(async context =>
        {
            var configFile = context.ParseResult.GetValueForOption(configOption);
            var outputType = context.ParseResult.GetValueForOption(outputOption) ?? "console";
            var formatType = context.ParseResult.GetValueForOption(formatOption) ?? "table";
            var service = (IListService)services.GetService(typeof(IListService))!;
            context.ExitCode = await HandleAsync(configFile?.FullName, formatType, outputType, service);
        });
        return cmd;
    }

    public static Task<int> HandleAsync(
        string? configPath,
        string formatType,
        string outputType,
        IListService listService)
    {
        if (formatType.Equals("template", StringComparison.OrdinalIgnoreCase))
        {
            var template = listService.BuildTemplate();
            Console.WriteLine(JsonSerializer.Serialize(template, JsonOutputOptions));
            return Task.FromResult(0);
        }

        // formatType == "table"
        var result = listService.GetProcessList(configPath);

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

        var matchedSet = result.Matches.Select(m => m.Fingerprint).ToHashSet();
        var destructiveSet = result.Matches
            .Where(m => m.Rule.Action is ActionType.Kill or ActionType.Stop or ActionType.Suspend)
            .Select(m => m.Fingerprint)
            .ToHashSet();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("PID");
        table.AddColumn("Process");
        table.AddColumn("Service");
        table.AddColumn("Status");
        table.AddColumn("Memory (MB)");
        table.AddColumn("Matched Rule");

        foreach (var fp in result.Fingerprints.OrderBy(f => f.ProcessName))
        {
            var ruleText = string.Join(", ",
                result.Matches.Where(m => m.Fingerprint == fp).Select(m => m.Rule.Id));

            var style = destructiveSet.Contains(fp) ? "red"
                      : matchedSet.Contains(fp)     ? "yellow"
                      :                               "grey";

            table.AddRow(
                $"[{style}]{fp.ProcessId}[/]",
                $"[{style}]{Markup.Escape(fp.ProcessName)}[/]",
                $"[{style}]{Markup.Escape(fp.ServiceName ?? string.Empty)}[/]",
                $"[{style}]{Markup.Escape(fp.ServiceStatus?.ToString() ?? string.Empty)}[/]",
                $"[{style}]{fp.WorkingSetBytes / 1024 / 1024}[/]",
                $"[{style}]{Markup.Escape(ruleText)}[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{result.Fingerprints.Count} processes, {result.Matches.Count} matches.[/]");

        return Task.FromResult(0);
    }
}
