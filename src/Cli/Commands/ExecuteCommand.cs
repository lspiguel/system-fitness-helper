using Serilog;
using Spectre.Console;
using System.CommandLine;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Safety;

namespace SystemFitnessHelper.Cli.Commands;

public static class ExecuteCommand
{
    public static Command Create(IServiceProvider services, Option<FileInfo?> configOption)
    {
        var yesOption = new Option<bool>(["--yes", "-y"], "Skip confirmation prompt (non-interactive)");
        var cmd = new Command("execute", "Execute planned actions against matched processes/services");
        cmd.AddOption(yesOption);
        cmd.SetHandler(async context =>
        {
            var configFile = context.ParseResult.GetValueForOption(configOption);
            var yes        = context.ParseResult.GetValueForOption(yesOption);
            var scanner    = (IProcessScanner)services.GetService(typeof(IProcessScanner))!;
            var matcher    = (IRuleMatcher)services.GetService(typeof(IRuleMatcher))!;
            var executor   = (IActionExecutor)services.GetService(typeof(IActionExecutor))!;
            context.ExitCode = await HandleAsync(configFile?.FullName, yes, scanner, matcher, executor);
        });
        return cmd;
    }

    public static Task<int> HandleAsync(
        string? configPath,
        bool skipPrompt,
        IProcessScanner scanner,
        IRuleMatcher matcher,
        IActionExecutor executor,
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
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(err)}");
            return Task.FromResult(2);
        }

        var actualGuard = guard ?? new SafetyGuard(
            new HashSet<string>(ruleSet.Protected, StringComparer.OrdinalIgnoreCase));

        var plans = matcher
            .Match(scanner.Scan(), ruleSet)
            .Select(m => new ActionPlan(m.Fingerprint, m.Rule.Action, m.Rule.Id))
            .ToList();

        if (plans.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No actions to execute.[/]");
            return Task.FromResult(0);
        }

        // Show plan table
        var planTable = new Table().Border(TableBorder.Rounded);
        planTable.AddColumn("Process");
        planTable.AddColumn("Service");
        planTable.AddColumn("Rule");
        planTable.AddColumn("Action");
        planTable.AddColumn("Blocked");

        foreach (var plan in plans)
        {
            var (allowed, reason) = actualGuard.IsAllowed(plan);
            var actionColor       = plan.Action is ActionType.Kill or ActionType.Stop ? "red" : "yellow";
            planTable.AddRow(
                Markup.Escape(plan.Fingerprint.ProcessName),
                Markup.Escape(plan.Fingerprint.ServiceName ?? string.Empty),
                Markup.Escape(plan.RuleId),
                $"[{actionColor}]{plan.Action}[/]",
                allowed ? "[green]No[/]" : $"[red]Yes — {Markup.Escape(reason ?? string.Empty)}[/]");
        }

        AnsiConsole.Write(planTable);

        if (!skipPrompt && !AnsiConsole.Confirm("Proceed with these actions?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
            return Task.FromResult(0);
        }

        var anyFailed   = false;
        var resultTable = new Table().Border(TableBorder.Rounded);
        resultTable.AddColumn("Process");
        resultTable.AddColumn("Action");
        resultTable.AddColumn("Result");
        resultTable.AddColumn("Detail");

        foreach (var plan in plans)
        {
            var (allowed, reason) = actualGuard.IsAllowed(plan);
            ActionResult result;

            if (!allowed)
            {
                result = ActionResult.Fail($"Blocked: {reason}");
            }
            else
            {
                result = executor.Execute(plan);
                Log.Information(
                    "Action {Action} on {Process} (rule: {Rule}): {Outcome}",
                    plan.Action, plan.Fingerprint.ProcessName, plan.RuleId,
                    result.Success ? "Success" : "Failed");
            }

            if (!result.Success) anyFailed = true;

            resultTable.AddRow(
                Markup.Escape(plan.Fingerprint.ProcessName),
                plan.Action.ToString(),
                result.Success ? "[green]✓ Success[/]" : "[red]✗ Failed[/]",
                Markup.Escape(result.Message));
        }

        AnsiConsole.Write(resultTable);
        return Task.FromResult(anyFailed ? 1 : 0);
    }
}
