using FluentAssertions;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Service.Handlers;
using Xunit;

namespace SystemFitnessHelper.Service.Tests.Handlers;

public sealed class PingHandlerTests
{
    [Fact]
    public void Method_IsPing() =>
        new PingHandler(TestServiceConfig.Create()).Method.Should().Be(Methods.Ping);

    [Fact]
    public async Task HandleAsync_ReportsTheResolvedConfigPath()
    {
        // Reporting the path here is what lets 'sfhi status' and the tray tooltip surface a
        // misconfigured rules file without anyone reading the log.
        PingHandler handler = new(TestServiceConfig.Create(@"C:\ProgramData\SystemFitnessHelper\rules.json"));

        object? output = await handler.HandleAsync(null, CancellationToken.None);

        PingResult result = output.Should().BeOfType<PingResult>().Subject;
        result.Ok.Should().BeTrue();
        result.ConfigPath.Should().Be(@"C:\ProgramData\SystemFitnessHelper\rules.json");
        result.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task HandleAsync_ReportsWhetherTheConfigFileExists()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"sfh-{Guid.NewGuid():N}.json");
        PingHandler handler = new(TestServiceConfig.Create(missing));

        object? output = await handler.HandleAsync(null, CancellationToken.None);

        ((PingResult)output!).ConfigExists.Should().BeFalse();
    }
}
