using System.Diagnostics;
using System.Management;
using System.ServiceProcess;

namespace SystemFitnessHelper.Fingerprinting;

/// <summary>
/// Windows implementation of <see cref="IProcessScanner"/>.
/// Combines <c>Process.GetProcesses()</c> with two WMI queries
/// (<c>Win32_Process</c> for command-line and parent PID, <c>Win32_Service</c> for service metadata)
/// to build a rich <see cref="ProcessFingerprint"/> for every running process.
/// </summary>
public sealed class WindowsProcessScanner : IProcessScanner
{
    public IReadOnlyList<ProcessFingerprint> Scan()
    {
        var wmiData = QueryWmiProcessData();
        var byPid = QueryRunningServices();
        var results = new List<ProcessFingerprint>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var pid = process.Id;
                wmiData.TryGetValue(pid, out var wmi);

                string? executablePath = null;
                try { executablePath = process.MainModule?.FileName; } catch { /* access denied */ }

                string? publisher = null;
                if (executablePath is not null)
                {
                    try { publisher = FileVersionInfo.GetVersionInfo(executablePath).CompanyName; } catch { }
                }

                string? parentName = null;
                if (wmi?.ParentProcessId is uint ppid)
                {
                    try { parentName = Process.GetProcessById((int)ppid).ProcessName; } catch { }
                }

                if (byPid.TryGetValue(pid, out var services) && services.Count > 0)
                {
                    foreach (var svc in services)
                    {
                        results.Add(new ProcessFingerprint(
                            ProcessId: pid,
                            ProcessName: process.ProcessName,
                            ExecutablePath: executablePath,
                            CommandLine: wmi?.CommandLine,
                            Publisher: publisher,
                            WorkingSetBytes: process.WorkingSet64,
                            ParentProcessName: parentName,
                            IsService: true,
                            ServiceName: svc.Name,
                            ServiceDisplayName: svc.DisplayName,
                            ServiceStatus: svc.Status));
                    }
                }
                else
                {
                    results.Add(new ProcessFingerprint(
                        ProcessId: pid,
                        ProcessName: process.ProcessName,
                        ExecutablePath: executablePath,
                        CommandLine: wmi?.CommandLine,
                        Publisher: publisher,
                        WorkingSetBytes: process.WorkingSet64,
                        ParentProcessName: parentName,
                        IsService: false,
                        ServiceName: null,
                        ServiceDisplayName: null,
                        ServiceStatus: null));
                }
            }
            catch
            {
                // Process exited or access denied — skip it
            }
        }

        return results;
    }

    private static Dictionary<int, WmiProcessData> QueryWmiProcessData()
    {
        var result = new Dictionary<int, WmiProcessData>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine, ParentProcessId FROM Win32_Process");
            foreach (ManagementObject mo in searcher.Get())
            {
                if (mo["ProcessId"] is not object pidObj)
                {
                    continue;
                }

                var pid = Convert.ToInt32(pidObj);
                var commandLine = mo["CommandLine"]?.ToString();
                var parentPid = mo["ParentProcessId"] is object ppObj
                    ? (uint?)Convert.ToUInt32(ppObj)
                    : null;
                result[pid] = new WmiProcessData(commandLine, parentPid);
            }
        }
        catch { /* WMI unavailable */ }

        return result;
    }

    private static Dictionary<int, List<ServiceInfo>> QueryRunningServices()
    {
        var result = new Dictionary<int, List<ServiceInfo>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DisplayName, ProcessId FROM Win32_Service WHERE State='Running'");
            foreach (ManagementObject mo in searcher.Get())
            {
                if (mo["ProcessId"] is not object pidObj)
                {
                    continue;
                }

                var pid = Convert.ToInt32(pidObj);
                var name = mo["Name"]?.ToString() ?? string.Empty;
                var displayName = mo["DisplayName"]?.ToString() ?? string.Empty;

                ServiceControllerStatus status = ServiceControllerStatus.Running;
                try
                {
                    using var sc = new ServiceController(name);
                    status = sc.Status;
                }
                catch { /* keep default Running */ }

                if (!result.TryGetValue(pid, out var list))
                {
                    list = [];
                    result[pid] = list;
                }

                list.Add(new ServiceInfo(name, displayName, status));
            }
        }
        catch { /* WMI or ServiceController unavailable */ }

        return result;
    }

    private sealed record WmiProcessData(string? CommandLine, uint? ParentProcessId);
    private sealed record ServiceInfo(string Name, string DisplayName, ServiceControllerStatus Status);
}
