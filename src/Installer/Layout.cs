namespace SystemFitnessHelper.Installer;

/// <summary>
/// Copies the published payload into the install root.
/// </summary>
public static class Layout
{
    /// <summary>The component directories the publish layout must contain.</summary>
    public static readonly string[] Components =
    [
        Paths.ServiceComponent,
        Paths.TrayAppComponent,
        Paths.UiComponent,
    ];

    /// <summary>
    /// Verifies the source layout before anything is modified, so a wrong working directory
    /// fails cleanly instead of half-installing.
    /// </summary>
    public static bool ValidateSource(Paths paths, out string? error)
    {
        foreach (string component in Components)
        {
            string dir = paths.SourceComponentDir(component);
            if (!Directory.Exists(dir))
            {
                error =
                    $"Missing component directory '{component}' under {paths.SourceRoot}.\n" +
                    "Run build.ps1 from the repository root and run sfhi from the publish folder it creates.";
                return false;
            }

            if (Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly).Length == 0)
            {
                error = $"Component directory '{component}' contains no executable ({dir}).";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Copies the whole published payload into the install root.
    /// </summary>
    /// <remarks>
    /// Copies everything rather than picking out the component directories, because sfhi.exe sits
    /// at the payload root alongside its own dependencies (and a <c>runtimes</c> folder). Copying
    /// the executable alone would leave an installed installer that cannot start — and that is
    /// exactly the command Apps &amp; Features invokes as the UninstallString.
    /// </remarks>
    public static void CopyPayload(Paths paths)
    {
        if (string.Equals(
                Path.TrimEndingDirectorySeparator(paths.SourceRoot),
                Path.TrimEndingDirectorySeparator(paths.InstallRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Running from the install directory; skipping the file copy.");
            return;
        }

        CopyDirectory(paths.SourceRoot, paths.InstallRoot);
    }

    /// <summary>
    /// Recursively copies a directory.
    /// </summary>
    /// <remarks>
    /// Recursive on purpose: the previous installer used
    /// <see cref="SearchOption.TopDirectoryOnly"/>, which silently dropped subdirectories such as
    /// the <c>runtimes</c> folder that appears as soon as a native or RID-specific dependency is
    /// added.
    /// </remarks>
    public static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);

        foreach (string dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
    }
}
