using System.CommandLine;
using Spectre.Console;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Safety;

namespace SystemFitnessHelper.Cli.Commands;

/// <summary>
/// Implements the <c>actions</c> CLI sub-command, which performs a dry-run: it scans processes,
/// evaluates rules, and displays what actions <em>would</em> be taken (including safety-guard blocks)
/// without executing anything.
/// </summary>
public static class ActionsCommand
{
    public static Command Create(IServiceProvider services, Option<FileInfo?> configOption)
    {
        var cmd = new Command("actions", "Show what actions would be taken (dry-run)");
        cmd.SetHandler(async context =>
        {
            var configFile = context.ParseResult.GetValueForOption(configOption);
            var scanner = (IProcessScanner)services.GetService(typeof(IProcessScanner))!;
            var matcher = (IRuleMatcher)services.GetService(typeof(IRuleMatcher))!;
            context.ExitCode = await HandleAsync(configFile?.FullName, scanner, matcher);
        });
        return cmd;
    }

    public static Task<int> HandleAsync(
        string? configPath,
        IProcessScanner scanner,
        IRuleMatcher matcher,
        SafetyGuard? guard = null)
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
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(err)}");
            }

            return Task.FromResult(2);
        }

        var actualGuard = guard ?? new SafetyGuard(
            new HashSet<string>(ruleSet.Protected, StringComparer.OrdinalIgnoreCase));

        var plans = matcher
            .Match(scanner.Scan(), ruleSet)
            .Select(m => new ActionPlan(m.Fingerprint, m.Rule.Action, m.Rule.Id))
            .ToList();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Process");
        table.AddColumn("Service");
        table.AddColumn("Matched Rule");
        table.AddColumn("Action");
        table.AddColumn("Blocked");
        table.AddColumn("Reason");

        foreach (var plan in plans)
        {
            var (allowed, reason) = actualGuard.IsAllowed(plan);
            var actionColor = plan.Action is ActionType.Kill or ActionType.Stop ? "red" : "yellow";
            table.AddRow(
                Markup.Escape(plan.Fingerprint.ProcessName),
                Markup.Escape(plan.Fingerprint.ServiceName ?? string.Empty),
                Markup.Escape(plan.RuleId),
                $"[{actionColor}]{plan.Action}[/]",
                allowed ? "[green]No[/]" : "[red]Yes[/]",
                Markup.Escape(reason ?? string.Empty));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{plans.Count} planned action(s).[/]");

        return Task.FromResult(0);
    }
}
