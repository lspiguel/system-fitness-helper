using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using SystemFitnessHelper.Ipc.Pipes;
using Xunit;

namespace SystemFitnessHelper.Ipc.Tests.Pipes;

public sealed class PipeSecurityFactoryTests
{
    private static List<PipeAccessRule> Rules() =>
        PipeSecurityFactory.CreateDefault()
            .GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

    [Fact]
    public void CreateDefault_GrantsAuthenticatedUsersReadWrite()
    {
        SecurityIdentifier authenticated = new(WellKnownSidType.AuthenticatedUserSid, null);

        PipeAccessRule? rule = Rules().FirstOrDefault(r => r.IdentityReference.Equals(authenticated));

        rule.Should().NotBeNull(
            "a non-elevated TrayApp or Ui cannot connect to a LocalSystem-owned pipe otherwise");
        rule!.AccessControlType.Should().Be(AccessControlType.Allow);
        rule.PipeAccessRights.Should().HaveFlag(PipeAccessRights.Read);
        rule.PipeAccessRights.Should().HaveFlag(PipeAccessRights.Write);
    }

    [Fact]
    public void CreateDefault_DoesNotLetAuthenticatedUsersCreatePipeInstances()
    {
        SecurityIdentifier authenticated = new(WellKnownSidType.AuthenticatedUserSid, null);

        PipeAccessRule rule = Rules().First(r => r.IdentityReference.Equals(authenticated));

        rule.PipeAccessRights.Should().NotHaveFlag(
            PipeAccessRights.CreateNewInstance,
            "an ordinary user must not be able to squat additional instances of the service pipe");
    }

    [Fact]
    public void CreateDefault_GrantsTheCreatingAccountFullControl()
    {
        using WindowsIdentity current = WindowsIdentity.GetCurrent();

        PipeAccessRule? rule = Rules().FirstOrDefault(r => r.IdentityReference.Equals(current.User));

        rule.Should().NotBeNull();
        rule!.PipeAccessRights.Should().HaveFlag(
            PipeAccessRights.CreateNewInstance,
            "the accept loop creates a fresh pipe instance for every connection it accepts");
    }

    [Fact]
    public void CreateDefault_GrantsAdministratorsAndSystemFullControl()
    {
        List<PipeAccessRule> rules = Rules();

        foreach (WellKnownSidType sid in new[]
                 {
                     WellKnownSidType.BuiltinAdministratorsSid,
                     WellKnownSidType.LocalSystemSid,
                 })
        {
            SecurityIdentifier identity = new(sid, null);
            rules.Should().Contain(
                r => r.IdentityReference.Equals(identity)
                     && r.PipeAccessRights == PipeAccessRights.FullControl);
        }
    }
}
