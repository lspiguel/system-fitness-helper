using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace SystemFitnessHelper.Installer;

public static class Elevation
{
    public static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Relaunches this process elevated, waits for it, and returns its exit code.
    /// </summary>
    /// <remarks>
    /// The previous implementation joined the arguments with a plain space (mangling any path
    /// containing one), never waited for the child, and returned failure even when the child
    /// succeeded — so <c>sfhi install; if ($?) { ... }</c> never chained. The child also exited
    /// immediately, closing its own console before any output could be read; it is passed
    /// <c>--elevated-child</c> so it knows to hold the window open.
    /// </remarks>
    public static int RelaunchElevated(string[] args)
    {
        string exe = Environment.ProcessPath ?? "sfhi.exe";

        ProcessStartInfo psi = new()
        {
            FileName = exe,
            Arguments = QuoteArguments([.. args, "--elevated-child"]),
            Verb = "runas",
            UseShellExecute = true,
        };

        try
        {
            using Process? child = Process.Start(psi);
            if (child is null)
            {
                Console.Error.WriteLine("Failed to start the elevated process.");
                return 1;
            }

            child.WaitForExit();
            return child.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to elevate: {ex.Message}");
            Console.Error.WriteLine("Run this command again from an elevated prompt.");
            return 1;
        }
    }

    /// <summary>
    /// Joins arguments using the quoting rules the C runtime applies when splitting them again.
    /// </summary>
    public static string QuoteArguments(IEnumerable<string> args) =>
        string.Join(' ', args.Select(QuoteArgument));

    public static string QuoteArgument(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0)
            return arg;

        StringBuilder sb = new();
        sb.Append('"');

        for (int i = 0; i < arg.Length; i++)
        {
            int backslashes = 0;
            while (i < arg.Length && arg[i] == '\\')
            {
                backslashes++;
                i++;
            }

            if (i == arg.Length)
            {
                // Trailing backslashes precede the closing quote, so they must be doubled.
                sb.Append('\\', backslashes * 2);
                break;
            }

            if (arg[i] == '"')
            {
                sb.Append('\\', (backslashes * 2) + 1);
                sb.Append('"');
            }
            else
            {
                sb.Append('\\', backslashes);
                sb.Append(arg[i]);
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>Keeps a relaunched console readable instead of vanishing on exit.</summary>
    public static void PauseIfElevatedChild(InstallerOptions options)
    {
        if (!options.ElevatedChild)
            return;

        Console.WriteLine();
        Console.Write("Press any key to close this window...");
        try
        {
            Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
            // No console to read from (redirected); nothing to wait for.
        }

        Console.WriteLine();
    }
}
