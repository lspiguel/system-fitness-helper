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
using SystemFitnessHelper.Services;

// Configure Serilog before command execution (pre-parse verbose flag)
var hasVerbose = args.Any(a => a is "--verbose" or "-v");
LoggingConfiguration.Configure(verbose: hasVerbose);

// Build DI container
var services = new ServiceCollection();
services.AddSingleton<IProcessScanner, WindowsProcessScanner>();
services.AddSingleton<IRuleMatcher, RuleMatcher>();
services.AddSingleton<IActionExecutor, WindowsActionExecutor>();
services.AddSingleton<SafetyGuard>();
services.AddSingleton<IConfigService, ConfigService>();
services.AddSingleton<IListService, ListService>();
services.AddSingleton<IActionsService, ActionsService>();
services.AddSingleton<IExecuteService, ExecuteService>();
var sp = services.BuildServiceProvider();

// Global options
var configOption = new Option<FileInfo?>(
    aliases: ["--config", "-c"],
    description: "Path to rules.json. If omitted, searches %APPDATA%\\SystemFitnessHelper\\rules.json then the executable directory.");
var verboseOption = new Option<bool>(
    aliases: ["--verbose", "-v"],
    description: "Enable verbose (Debug) logging to console and log file.");
var outputOption = new Option<string>(
    aliases: ["--output", "-o"],
    description: "Output format: 'console' (default) or 'json'.");
outputOption.SetDefaultValue("console");
var ruleSetOption = new Option<string?>(
    aliases: ["--ruleset", "-r"],
    description: "Name of the ruleset to use. If omitted, the default ruleset is used.");

// Sub-commands
var configCmd = ConfigCommand.Create(sp, configOption, outputOption);
var listCmd = ListCommand.Create(sp, configOption, outputOption, ruleSetOption);
var actionsCmd = ActionsCommand.Create(sp, configOption, outputOption, ruleSetOption);
var executeCmd = ExecuteCommand.Create(sp, configOption, outputOption, ruleSetOption);

var subCommands = new Command[] { configCmd, listCmd, actionsCmd, executeCmd };
var globalOptions = new Option[] { configOption, verboseOption, outputOption, ruleSetOption };

var root = new RootCommand("sfhcli — System Fitness Helper: inspect and manage processes and services");
root.AddGlobalOption(configOption);
root.AddGlobalOption(verboseOption);
root.AddGlobalOption(outputOption);
root.AddGlobalOption(ruleSetOption);

root.AddCommand(configCmd);
root.AddCommand(listCmd);
root.AddCommand(actionsCmd);
root.AddCommand(executeCmd);
root.AddCommand(HelpCommand.Create(subCommands, globalOptions));

return await new CommandLineBuilder(root)
    .UseDefaults()
    .Build()
    .InvokeAsync(args);
