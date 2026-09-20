using FluentAssertions;
using SystemFitnessHelper.Installer;
using Xunit;

namespace SystemFitnessHelper.Installer.Tests;

/// <summary>
/// The elevated relaunch used to join arguments with a plain space, so
/// <c>--prefix "C:\Program Files\X"</c> arrived at the child as three separate arguments.
/// </summary>
public sealed class ElevationTests
{
    [Theory]
    [InlineData("install", "install")]
    [InlineData("--prefix", "--prefix")]
    [InlineData(@"C:\Temp\sfh", @"C:\Temp\sfh")]
    public void QuoteArgument_LeavesSimpleArgumentsAlone(string input, string expected) =>
        Elevation.QuoteArgument(input).Should().Be(expected);

    [Fact]
    public void QuoteArgument_QuotesPathsContainingSpaces() =>
        Elevation.QuoteArgument(@"C:\Program Files\SystemFitnessHelper")
            .Should().Be(@"""C:\Program Files\SystemFitnessHelper""");

    [Fact]
    public void QuoteArgument_EscapesEmbeddedQuotes() =>
        Elevation.QuoteArgument(@"say ""hi""").Should().Be(@"""say \""hi\""""");

    [Fact]
    public void QuoteArgument_DoublesBackslashesBeforeTheClosingQuote() =>
        Elevation.QuoteArgument(@"C:\Program Files\X\").Should().Be(@"""C:\Program Files\X\\""");

    [Fact]
    public void QuoteArguments_RoundTripsThroughCommandLineSplitting()
    {
        string[] original = ["install", "--prefix", @"C:\Program Files\Custom Location", "--purge"];

        string commandLine = Elevation.QuoteArguments(original);

        SplitCommandLine(commandLine).Should().Equal(original);
    }

    /// <summary>
    /// Splits a command line the way the C runtime does, to confirm the quoting survives the
    /// round trip the elevated child actually performs.
    /// </summary>
    private static List<string> SplitCommandLine(string commandLine)
    {
        List<string> args = [];
        System.Text.StringBuilder current = new();
        bool inQuotes = false;
        bool has = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char c = commandLine[i];

            if (c == '\\')
            {
                int backslashes = 0;
                while (i < commandLine.Length && commandLine[i] == '\\')
                {
                    backslashes++;
                    i++;
                }

                if (i < commandLine.Length && commandLine[i] == '"')
                {
                    current.Append('\\', backslashes / 2);
                    if (backslashes % 2 == 0)
                    {
                        inQuotes = !inQuotes;
                    }
                    else
                    {
                        current.Append('"');
                    }

                    has = true;
                }
                else
                {
                    current.Append('\\', backslashes);
                    i--;
                    has = true;
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                has = true;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (has)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    has = false;
                }

                continue;
            }

            current.Append(c);
            has = true;
        }

        if (has)
            args.Add(current.ToString());

        return args;
    }
}
