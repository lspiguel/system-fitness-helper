using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SystemFitnessHelper.TrayApp;

public sealed class UiLauncher
{
    private const string UiProcessName = "SystemFitnessHelper.Ui";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public void LaunchOrActivate()
    {
        Process[] existing = Process.GetProcessesByName(UiProcessName);
        if (existing.Length > 0)
        {
            IntPtr hWnd = existing[0].MainWindowHandle;
            if (hWnd != IntPtr.Zero)
                SetForegroundWindow(hWnd);
            return;
        }

        string uiExePath = Path.Combine(
            AppContext.BaseDirectory, "..", "Ui", $"{UiProcessName}.exe");

        if (!File.Exists(uiExePath))
        {
            // Try sibling directory pattern used in development
            uiExePath = Path.Combine(AppContext.BaseDirectory, $"{UiProcessName}.exe");
        }

        if (File.Exists(uiExePath))
        {
            Process.Start(new ProcessStartInfo(uiExePath) { UseShellExecute = true });
        }
        else
        {
            MessageBox.Show(
                $"Could not locate {UiProcessName}.exe. Please ensure it is installed.",
                "System Fitness Helper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
