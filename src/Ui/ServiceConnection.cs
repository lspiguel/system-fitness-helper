using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Pipes;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Ui;

public sealed class ServiceConnection
{
    private readonly CommandPipeClient _client = new();

    public async Task<ConfigResult> GetConfigAsync(CancellationToken ct = default) =>
        await this._client.SendAsync<ConfigResult>(Methods.Config, null, ct).ConfigureAwait(false);

    public async Task<ProcessListResult> GetProcessListAsync(string? ruleSetName = null, CancellationToken ct = default) =>
        await this._client.SendAsync<ProcessListResult>(
            Methods.List,
            new ListParams { RuleSetName = ruleSetName },
            ct).ConfigureAwait(false);

    public async Task<RuleSet> GetTemplateAsync(CancellationToken ct = default) =>
        await this._client.SendAsync<RuleSet>(Methods.ListTemplate, null, ct).ConfigureAwait(false);

    public async Task<ActionsResult> GetActionsAsync(string? ruleSetName = null, CancellationToken ct = default) =>
        await this._client.SendAsync<ActionsResult>(
            Methods.Actions,
            new ActionsParams { RuleSetName = ruleSetName },
            ct).ConfigureAwait(false);

    public async Task<ExecuteResult> ExecuteAsync(string? ruleSetName = null, CancellationToken ct = default) =>
        await this._client.SendAsync<ExecuteResult>(
            Methods.Execute,
            new ExecuteParams { RuleSetName = ruleSetName },
            ct).ConfigureAwait(false);

    public async Task<ConfigSaveResult> SaveConfigAsync(RuleSetsConfig ruleSetsConfig, CancellationToken ct = default) =>
        await this._client.SendAsync<ConfigSaveResult>(
            Methods.ConfigSave,
            new ConfigSaveParams { RuleSetsConfig = ruleSetsConfig },
            ct).ConfigureAwait(false);
}
