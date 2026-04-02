namespace SystemFitnessHelper.Actions;

/// <summary>
/// Executes a planned remediation action against a process or service.
/// Implementations are platform-specific (e.g. <see cref="WindowsActionExecutor"/>).
/// </summary>
public interface IActionExecutor
{
    ActionResult Execute(ActionPlan plan);
}
