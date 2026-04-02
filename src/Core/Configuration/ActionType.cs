namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Specifies the remediation action to take against a matched process or service.
/// </summary>
public enum ActionType
{
    None,
    Stop,
    Kill,
    Suspend,
}
