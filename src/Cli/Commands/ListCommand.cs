using Spectre.Console;
using System.CommandLine;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;

namespace SystemFitnessHelper.Cli.Commands;

/// <summary>
/// Implements the <c>list</c> CLI sub-command, which enumerates all running processes and
/// highlights matched ones in yellow (non-destructive action) or red (Kill/Stop/Suspend).
/// No actions are executed.
/// </summary>
public static class ListCommand
{
    public static Command Create(IServiceProvider services, Option<FileInfo?> configOption)
    {
        var cmd = new Command("list", "Enumerate processes and highlight matched ones");
        cmd.SetHandler(async context =>
        {
            var configFile = context.ParseResult.GetValueForOption(configOption);
            var scanner    = (IProcessScanner)services.GetService(typeof(IProcessScanner))!;
            var matcher    = (IRuleMatcher)services.GetService(typeof(IRuleMatcher))!;
            context.ExitCode = await HandleAsync(configFile?.FullName, scanner, matcher);
        });
        return cmd;
    }

    public static Task<int> HandleAsync(
        string? configPath,
        IProcessScanner scanner,
        IRuleMatcher matcher)
    {
        var path = ConfigurationLoader.DiscoverPath(configPath);
        if (path is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No rules.json found. Use --config to specify a path.");
            return Task.FromResult(2);
        }

        var (ruleSet, validation) = ConfigurationLoader.Load(path);
        if (!validation.IsValid || ruleSet is null)
        {
            foreach (var err in validation.Errors)
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(err)}");
            return Task.FromResult(2);
        }

        var fingerprints    = scanner.Scan();
        var matches         = matcher.Match(fingerprints, ruleSet);
        var matchedSet      = matches.Select(m => m.Fingerprint).ToHashSet();
        var destructiveSet  = matches
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

        foreach (var fp in fingerprints.OrderBy(f => f.ProcessName))
        {
            var ruleText = string.Join(", ",
                matches.Where(m => m.Fingerprint == fp).Select(m => m.Rule.Id));

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
        AnsiConsole.MarkupLine($"[grey]{fingerprints.Count} processes, {matches.Count} matches.[/]");

        return Task.FromResult(0);
    }
}
