using Microsoft.Win32;

namespace SystemFitnessHelper.Installer;

/// <summary>
/// Start Menu shortcuts, tray autostart and the Add/Remove Programs entry.
/// </summary>
/// <remarks>
/// Everything here is machine-wide (common Start Menu, HKLM) rather than per-user. The installer
/// runs elevated, so HKCU and the per-user Start Menu would land in the administrator's profile
/// instead of the profile of whoever is actually installing.
/// </remarks>
public static class Shortcuts
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static void CreateStartMenuShortcuts(Paths paths)
    {
        Directory.CreateDirectory(paths.StartMenuDir);

        CreateShortcut(
            Path.Combine(paths.StartMenuDir, $"{Paths.DisplayName}.lnk"),
            paths.UiExe,
            "System Fitness Helper dashboard");

        CreateShortcut(
            Path.Combine(paths.StartMenuDir, $"{Paths.DisplayName} Tray.lnk"),
            paths.TrayAppExe,
            "System Fitness Helper tray application");

        Console.WriteLine($"Created Start Menu shortcuts in: {paths.StartMenuDir}");
    }

    public static void RemoveStartMenuShortcuts(Paths paths)
    {
        if (!Directory.Exists(paths.StartMenuDir))
            return;

        try
        {
            Directory.Delete(paths.StartMenuDir, recursive: true);
            Console.WriteLine($"Removed Start Menu shortcuts: {paths.StartMenuDir}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: could not remove Start Menu shortcuts: {ex.Message}");
        }
    }

    public static void EnableTrayAutostart(Paths paths)
    {
        using RegistryKey key = Registry.LocalMachine.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException($"Could not open HKLM\\{RunKey}.");

        key.SetValue(paths.RunValueName, $"\"{paths.TrayAppExe}\"", RegistryValueKind.String);
        Console.WriteLine("Tray application registered to start for all users at sign-in.");
    }

    public static void DisableTrayAutostart(Paths paths)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(paths.RunValueName) is not null)
            {
                key.DeleteValue(paths.RunValueName);
                Console.WriteLine("Removed tray autostart entry.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: could not remove tray autostart entry: {ex.Message}");
        }
    }

    public static void RegisterUninstallEntry(Paths paths, string version)
    {
        using RegistryKey key = Registry.LocalMachine.CreateSubKey(paths.UninstallRegistryKey, writable: true)
            ?? throw new InvalidOperationException($"Could not open HKLM\\{paths.UninstallRegistryKey}.");

        key.SetValue("DisplayName", Paths.DisplayName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "Luciano Spiguel");
        key.SetValue("InstallLocation", paths.InstallRoot);
        key.SetValue("DisplayIcon", paths.UiExe);
        key.SetValue("UninstallString", $"\"{paths.InstalledInstallerExe}\" uninstall --remove-files");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

        Console.WriteLine("Registered in Apps & Features.");
    }

    public static void RemoveUninstallEntry(Paths paths)
    {
        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(paths.UninstallRegistryKey, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: could not remove the Apps & Features entry: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a .lnk through the WScript.Shell COM object.
    /// </summary>
    /// <remarks>
    /// Shortcut files require IShellLink. Going through the scripting host's ProgID keeps this to
    /// late binding and avoids taking a COM interop dependency for two shortcuts.
    /// </remarks>
    private static void CreateShortcut(string linkPath, string targetPath, string description)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            Console.Error.WriteLine("Warning: WScript.Shell unavailable; skipping shortcut creation.");
            return;
        }

        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
                return;

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                [linkPath]);

            if (shortcut is null)
                return;

            Type shortcutType = shortcut.GetType();
            SetProperty(shortcutType, shortcut, "TargetPath", targetPath);
            SetProperty(shortcutType, shortcut, "WorkingDirectory", Path.GetDirectoryName(targetPath) ?? string.Empty);
            SetProperty(shortcutType, shortcut, "Description", description);
            SetProperty(shortcutType, shortcut, "IconLocation", targetPath + ",0");

            shortcutType.InvokeMember(
                "Save",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shortcut,
                []);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: could not create shortcut '{linkPath}': {ex.Message}");
        }
        finally
        {
            if (shortcut is not null)
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null)
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void SetProperty(Type type, object instance, string name, string value) =>
        type.InvokeMember(name, System.Reflection.BindingFlags.SetProperty, null, instance, [value]);
}
