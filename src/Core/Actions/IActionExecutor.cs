namespace SystemFitnessHelper.Actions;

public interface IActionExecutor
{
    ActionResult Execute(ActionPlan plan);
}
