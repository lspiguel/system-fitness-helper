// <copyright file="Program.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
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
var configOption = new Option<FileInfo?>(
    aliases: ["--config", "-c"],
    description: "Path to rules.json. If omitted, searches %APPDATA%\\SystemFitnessHelper\\rules.json then the executable directory.");
var verboseOption = new Option<bool>(
    aliases: ["--verbose", "-v"],
    description: "Enable verbose (Debug) logging to console and log file.");

// Sub-commands
var configCmd = ConfigCommand.Create(sp, configOption);
var listCmd = ListCommand.Create(sp, configOption);
var actionsCmd = ActionsCommand.Create(sp, configOption);
var executeCmd = ExecuteCommand.Create(sp, configOption);

var subCommands = new Command[] { configCmd, listCmd, actionsCmd, executeCmd };
var globalOptions = new Option[] { configOption, verboseOption };

var root = new RootCommand("sfhcli — System Fitness Helper: inspect and manage processes and services");
root.AddGlobalOption(configOption);
root.AddGlobalOption(verboseOption);

root.AddCommand(configCmd);
root.AddCommand(listCmd);
root.AddCommand(actionsCmd);
root.AddCommand(executeCmd);
root.AddCommand(HelpCommand.Create(subCommands, globalOptions));

return await new CommandLineBuilder(root)
    .UseDefaults()
    .Build()
    .InvokeAsync(args);
