using System.CommandLine;
using System.Text.RegularExpressions;
using SystemFitnessHelper.Cli.Commands;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands
{
    /// <summary>
    /// Marks tests that redirect Console.Out and must not run in parallel with each other.
    /// </summary>
    [CollectionDefinition(nameof(ConsoleOutputCollection), DisableParallelization = true)]
    public class ConsoleOutputCollection { }

    /// <summary>
    /// Contains unit tests for the HelpCommand class, verifying help output for command-line scenarios.
    /// </summary>
    /// <remarks>
    /// This test was BADLY written by GitHub Copilot.
    /// </remarks>
    [Collection(nameof(ConsoleOutputCollection))]
    public class HelpCommandTests
    {
        private static string RemoveAnsi(string input) =>
            Regex.Replace(input ?? string.Empty, @"\x1B\[[0-9;]*m", string.Empty);

        [Fact]
        public async Task AllCommands_ListsAllCommands()
        {
            // Arrange
            var subCommands = new List<Command>
            {
                new Command("execute", "Execute something"),
                new Command("list", "List items")
            };
            var globalOptions = new List<Option>();

            var helpCmd = HelpCommand.Create(subCommands, globalOptions);
            var root = new RootCommand();
            root.AddCommand(helpCmd);

            var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            try
            {
                // Act
                await root.InvokeAsync(new[] { "help" });

                // Assert
                var output = RemoveAnsi(sw.ToString());
                Assert.Contains("System Fitness Helper", output);
                Assert.Contains("execute", output);
                Assert.Contains("Execute something", output);
                Assert.Contains("list", output);
                Assert.Contains("List items", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public async Task UnknownCommand_ShowsErrorMessage()
        {
            // Arrange
            var subCommands = new List<Command>
            {
                new Command("execute", "Execute something")
            };
            var globalOptions = new List<Option>();

            var helpCmd = HelpCommand.Create(subCommands, globalOptions);
            var root = new RootCommand();
            root.AddCommand(helpCmd);

            var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            try
            {
                // Act
                await root.InvokeAsync(new[] { "help", "unknown" });

                // Assert
                var output = RemoveAnsi(sw.ToString());
                Assert.Contains("Error: Unknown command 'unknown'.", output);
                Assert.Contains("Run sfhcli help to list available commands.", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public async Task CommandHelp_IncludesOwnAndGlobalOptions()
        {
            // Arrange
            var execCmd = new Command("execute", "Execute something");
            execCmd.AddOption(new Option<bool>("--force", "Force execution"));

            var subCommands = new List<Command> { execCmd };

            var globalOptions = new List<Option>
            {
                new Option<string>("--config", "Path to configuration")
            };

            var helpCmd = HelpCommand.Create(subCommands, globalOptions);
            var root = new RootCommand();
            root.AddCommand(helpCmd);

            var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);

            try
            {
                // Act
                await root.InvokeAsync(new[] { "help", "execute" });

                // Assert
                var output = RemoveAnsi(sw.ToString());
                Assert.Contains("sfhcli execute", output);
                Assert.Contains("Execute something", output);
                Assert.Contains("Options", output);
                Assert.Contains("--force", output);
                Assert.Contains("Global Options", output);
                Assert.Contains("--config", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}