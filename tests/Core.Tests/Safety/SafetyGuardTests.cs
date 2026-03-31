using FluentAssertions;
using System.ServiceProcess;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Safety;
using Xunit;

namespace SystemFitnessHelper.Tests.Safety;

public sealed class SafetyGuardTests
{
    private static readonly SafetyGuard Guard = new();

    private static ProcessFingerprint MakeServiceFp(string processName, string serviceName) =>
        new(1234, processName, null, null, null, 0, null, true, serviceName, serviceName,
            ServiceControllerStatus.Running);

    private static ProcessFingerprint MakeProcessFp(string processName) =>
        new(1234, processName, null, null, null, 0, null, false, null, null, null);

    private static ActionPlan MakePlan(ProcessFingerprint fp, ActionType action = ActionType.Stop) =>
        new(fp, action, "test-rule");

    [Theory]
    [InlineData("lsass")]
    [InlineData("wuauserv")]
    [InlineData("WinDefend")]
    public void HardCodedProtectedService_IsBlocked(string serviceName)
    {
        var fp = MakeServiceFp(serviceName, serviceName);
        var (allowed, reason) = Guard.IsAllowed(MakePlan(fp));

        allowed.Should().BeFalse();
        reason.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("csrss")]
    [InlineData("svchost")]
    [InlineData("lsass")]
    [InlineData("wininit")]
    public void HardCodedProtectedProcess_IsBlocked(string processName)
    {
        var fp = MakeProcessFp(processName);
        var (allowed, reason) = Guard.IsAllowed(MakePlan(fp, ActionType.Kill));

        allowed.Should().BeFalse();
        reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void UserProtectedService_IsBlocked()
    {
        var userProtected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MySvc" };
        var guard = new SafetyGuard(userProtected);

        var fp = MakeServiceFp("myservice.exe", "MySvc");
        var (allowed, reason) = guard.IsAllowed(MakePlan(fp));

        allowed.Should().BeFalse();
        reason.Should().Contain("MySvc");
    }

    [Fact]
    public void UserProtectedService_CaseInsensitive()
    {
        var userProtected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MySvc" };
        var guard = new SafetyGuard(userProtected);

        var fp = MakeServiceFp("myservice.exe", "MYSVC");
        var (allowed, _) = guard.IsAllowed(MakePlan(fp));

        allowed.Should().BeFalse();
    }

    [Fact]
    public void NormalService_IsAllowed()
    {
        var fp = MakeServiceFp("steamservice.exe", "SteamClientService");
        var (allowed, _) = Guard.IsAllowed(MakePlan(fp));

        allowed.Should().BeTrue();
    }

    [Fact]
    public void NormalProcess_IsAllowed()
    {
        var fp = MakeProcessFp("notepad");
        var (allowed, _) = Guard.IsAllowed(MakePlan(fp, ActionType.Kill));

        allowed.Should().BeTrue();
    }
}
