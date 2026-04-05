// <copyright file="ExecuteCommand.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.CommandLine;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Cli.Commands;

/// <summary>
/// Implements the <c>execute</c> CLI sub-command, which scans processes, evaluates rules,
/// and — after an optional confirmation prompt — applies each allowed action.
/// Re-launches itself elevated via UAC if the process is not already running as administrator.
/// </summary>
public static class ExecuteCommand
{
    private static readonly JsonSerializerOptions JsonOutputOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Command Create(IServiceProvider services, Option<FileInfo?> configOption, Option<string> outputOption)
    {
        var yesOption = new Option<bool>(
            aliases: ["--yes", "-y"],
            description: "Skip the confirmation prompt and execute immediately (non-interactive / scripted use).");
        var cmd = new Command("execute", "Execute planned actions against matched processes/services");
        cmd.AddOption(yesOption);
        cmd.SetHandler(async context =>
        {
            var configFile = context.ParseResult.GetValueForOption(configOption);
            var outputType = context.ParseResult.GetValueForOption(outputOption) ?? "console";
            var yes = context.ParseResult.GetValueForOption(yesOption);
            var actionsService = (IActionsService)services.GetService(typeof(IActionsService))!;
            var executeService = (IExecuteService)services.GetService(typeof(IExecuteService))!;
            context.ExitCode = await HandleAsync(
                configFile?.FullName, outputType, yes, actionsService, executeService);
        });
        return cmd;
    }

    public static Task<int> HandleAsync(
        string? configPath,
        string outputType,
        bool skipPrompt,
        IActionsService actionsService,
        IExecuteService executeService,
        Func<bool>? isElevated = null,
        Func<int>? relaunchAsAdmin = null)
    {
        if (!(isElevated ?? IsElevated)())
        {
            AnsiConsole.MarkupLine("[yellow]Not running as administrator. Requesting elevation...[/]");
            return Task.FromResult((relaunchAsAdmin ?? RelaunchAsAdmin)());
        }

        var jsonMode = outputType.Equals("json", StringComparison.OrdinalIgnoreCase);

        // In JSON mode, skip prompt (non-interactive) and go straight to execution
        if (jsonMode)
        {
            var execResult = executeService.Execute(configPath);
            Console.WriteLine(JsonSerializer.Serialize(execResult, JsonOutputOptions));
            return Task.FromResult(execResult.ExitCode);
        }

        // Console mode: show plan, prompt, then execute
        var actionsResult = actionsService.GetActions(configPath);
        if (actionsResult.ErrorMessage is not null)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(actionsResult.ErrorMessage)}");
            return Task.FromResult(actionsResult.ExitCode);
        }

        if (actionsResult.Plans.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No actions to execute.[/]");
            return Task.FromResult(0);
        }

        var planTable = new Table().Border(TableBorder.Rounded);
        planTable.AddColumn("Process");
        planTable.AddColumn("Service");
        planTable.AddColumn("Rule");
        planTable.AddColumn("Action");
        planTable.AddColumn("Blocked");

        foreach (var plan in actionsResult.Plans)
        {
            var actionColor = plan.Action is ActionType.Kill or ActionType.Stop ? "red" : "yellow";
            planTable.AddRow(
                Markup.Escape(plan.ProcessName),
                Markup.Escape(plan.ServiceName ?? string.Empty),
                Markup.Escape(plan.RuleId),
                $"[{actionColor}]{plan.Action}[/]",
                plan.Blocked
                    ? $"[red]Yes — {Markup.Escape(plan.BlockReason ?? string.Empty)}[/]"
                    : "[green]No[/]");
        }

        AnsiConsole.Write(planTable);

        if (!skipPrompt && !AnsiConsole.Confirm("Proceed with these actions?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
            return Task.FromResult(0);
        }

        var executeResult = executeService.Execute(configPath);

        var resultTable = new Table().Border(TableBorder.Rounded);
        resultTable.AddColumn("Process");
        resultTable.AddColumn("Action");
        resultTable.AddColumn("Result");
        resultTable.AddColumn("Detail");

        foreach (var r in executeResult.Results)
        {
            resultTable.AddRow(
                Markup.Escape(r.ProcessName),
                r.Action.ToString(),
                r.Success ? "[green]✓ Success[/]" : "[red]✗ Failed[/]",
                Markup.Escape(r.Message));
        }

        AnsiConsole.Write(resultTable);
        return Task.FromResult(executeResult.ExitCode);
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static int RelaunchAsAdmin()
    {
        var exe = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
        var argParts = Environment.GetCommandLineArgs().Skip(1)
                           .Select(a => a.Contains(' ') ? $"\"{a}\"" : a);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.Join(" ", argParts),
                Verb = "runas",
                UseShellExecute = true,
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit();
            return proc?.ExitCode ?? 1;
        }
        catch (Win32Exception)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Elevation was cancelled or denied.");
            return 1;
        }
    }
}
