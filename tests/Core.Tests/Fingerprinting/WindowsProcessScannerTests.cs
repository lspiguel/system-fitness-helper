using FluentAssertions;
using Moq;
using SystemFitnessHelper.Fingerprinting;
using Xunit;

namespace SystemFitnessHelper.Tests.Fingerprinting;

public sealed class WindowsProcessScannerTests
{
    [Fact]
    public void IProcessScanner_CanBeMocked()
    {
        var mock = new Mock<IProcessScanner>();
        mock.Setup(s => s.Scan()).Returns([]);

        mock.Object.Scan().Should().BeEmpty();
    }

    [Fact]
    public void Scan_ReturnsNonEmptyList()
    {
        // Integration test — verifies scanner works on the current Windows machine
        var scanner = new WindowsProcessScanner();
        var result  = scanner.Scan();

        result.Should().NotBeNull();
        result.Should().NotBeEmpty("at least this test process should be present");
    }

    [Fact]
    public void Scan_AllFingerprintsHaveProcessName()
    {
        var scanner = new WindowsProcessScanner();
        var result  = scanner.Scan();

        result.Should().OnlyContain(fp => !string.IsNullOrEmpty(fp.ProcessName));
    }

    [Fact]
    public void Scan_ServiceFingerprintsHaveServiceName()
    {
        var scanner  = new WindowsProcessScanner();
        var services = scanner.Scan().Where(fp => fp.IsService).ToList();

        services.Should().OnlyContain(fp => fp.ServiceName != null);
    }
}
