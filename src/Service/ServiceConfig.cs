namespace SystemFitnessHelper.Service;

public sealed class ServiceConfig
{
    public string ConfigPath { get; init; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SystemFitnessHelper",
            "rules.json");
}
