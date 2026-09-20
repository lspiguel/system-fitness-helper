using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SystemFitnessHelper.Ipc.Pipes;

/// <summary>
/// Builds the <see cref="PipeSecurity"/> applied to the service's named pipes.
/// </summary>
/// <remarks>
/// The service runs as LocalSystem under the SCM. Without an explicit DACL the pipes inherit the
/// default one from the LocalSystem token, which denies access to ordinary interactive users, so
/// a non-elevated TrayApp or Ui fails to connect with <see cref="UnauthorizedAccessException"/>.
/// This never surfaces in development because both ends run as the same interactive user.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class PipeSecurityFactory
{
    /// <summary>
    /// Creates the default DACL: authenticated users may read and write, administrators and
    /// LocalSystem get full control.
    /// </summary>
    /// <remarks>
    /// Authenticated users are deliberately <em>not</em> granted
    /// <see cref="PipeAccessRights.CreateNewInstance"/>, so they cannot add further instances of
    /// the pipe and impersonate the service to other clients.
    /// </remarks>
    public static PipeSecurity CreateDefault()
    {
        PipeSecurity security = new();

        // The creating account must keep CreateNewInstance, otherwise the accept loop can create
        // the first pipe instance but not the second, and the server silently degrades to serving
        // one client at a time. This matters whenever the owner is not covered by the
        // Administrators ACE below: an unelevated process, or the service run as a console
        // application for debugging.
        using (WindowsIdentity current = WindowsIdentity.GetCurrent())
        {
            if (current.User is not null)
            {
                security.AddAccessRule(new PipeAccessRule(
                    current.User,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));
            }
        }

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return security;
    }
}
