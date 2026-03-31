using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Cli.Commands;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Logging;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Safety;

// Configure Serilog before command execution (pre-parse verbose flag)
var hasVerbose = args.Any(a => a is "--verbose" or "-v");
LoggingConfiguration.Configure(verbose: hasVerbose);

// Build DI container
var services = new ServiceCollection();
services.AddSingleton<IProcessScanner, WindowsProcessScanner>();
services.AddSingleton<IRuleMatcher, RuleMatcher>();
services.AddSingleton<IActionExecutor, WindowsActionExecutor>();
services.AddSingleton<SafetyGuard>();
var sp = services.BuildServiceProvider();

// Global options
var configOption  = new Option<FileInfo?>(["--config", "-c"],  "Path to rules.json");
var verboseOption = new Option<bool>     (["--verbose", "-v"], "Enable verbose (Debug) logging");

var root = new RootCommand("sfh — System Fitness Helper: inspect and manage processes and services");
root.AddGlobalOption(configOption);
root.AddGlobalOption(verboseOption);

root.AddCommand(ConfigCommand.Create(sp, configOption));
root.AddCommand(ListCommand.Create(sp, configOption));
root.AddCommand(ActionsCommand.Create(sp, configOption));
root.AddCommand(ExecuteCommand.Create(sp, configOption));

return await new CommandLineBuilder(root)
    .UseDefaults()
    .Build()
    .InvokeAsync(args);
