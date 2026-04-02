using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using SystemFitnessHelper.Configuration;

namespace SystemFitnessHelper.Actions;

/// <summary>
/// Windows implementation of <see cref="IActionExecutor"/>.
/// Dispatches Stop (via SCM), Kill (via <c>Process.Kill</c>), and Suspend
/// (via the native <c>NtSuspendProcess</c> syscall) actions.
/// </summary>
public sealed class WindowsActionExecutor : IActionExecutor
{
    private static readonly TimeSpan ServiceStopTimeout = TimeSpan.FromSeconds(30);

    public ActionResult Execute(ActionPlan plan)
    {
        var fp = plan.Fingerprint;

        if (fp.IsService && plan.Action == ActionType.Kill)
        {
            return ActionResult.Fail(
                $"Cannot Kill service '{fp.ServiceName}'. Use Stop instead to let SCM clean up gracefully.");
        }

        if (fp.IsService && plan.Action == ActionType.Suspend)
        {
            return ActionResult.Fail(
                $"Cannot Suspend service '{fp.ServiceName}'. Services must be stopped through SCM.");
        }

        return plan.Action switch
        {
            ActionType.Stop when fp.IsService => StopService(fp.ServiceName!),
            ActionType.Kill => KillProcess(fp.ProcessId),
            ActionType.Suspend => SuspendProcess(fp.ProcessId),
            ActionType.None => ActionResult.Ok("No action specified."),
            _ => ActionResult.Fail($"Unsupported action '{plan.Action}' for this target."),
        };
    }

    private static ActionResult StopService(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                return ActionResult.Ok($"Service '{serviceName}' is already stopped.");
            }

            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, ServiceStopTimeout);
            return ActionResult.Ok($"Service '{serviceName}' stopped successfully.");
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Failed to stop service '{serviceName}': {GetFullMessage(ex)}", ex);
        }
    }

    private static ActionResult KillProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            return ActionResult.Ok($"Process {processId} killed.");
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Failed to kill process {processId}: {GetFullMessage(ex)}", ex);
        }
    }

    private static ActionResult SuspendProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var ntstatus = NtSuspendProcess(process.Handle);
            if (ntstatus < 0)
            {
                return ActionResult.Fail(
                    $"NtSuspendProcess failed with NTSTATUS 0x{ntstatus:X8} for process {processId}.");
            }

            return ActionResult.Ok($"Process {processId} suspended.");
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Failed to suspend process {processId}: {GetFullMessage(ex)}", ex);
        }
    }

    private static string GetFullMessage(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        var current = ex;
        while (current is not null)
        {
            if (sb.Length > 0)
            {
                sb.Append(" ---> ");
            }

            sb.Append(current.Message);
            current = current.InnerException;
        }

        return sb.ToString();
    }

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr processHandle);
}
