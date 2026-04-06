using System.Text.Json;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Service.Handlers;

public sealed class ListTemplateHandler : IRequestHandler
{
    private readonly IListService _listService;

    public string Method => Methods.ListTemplate;

    public ListTemplateHandler(IListService listService)
    {
        this._listService = listService;
    }

    public Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct)
    {
        var template = this._listService.BuildTemplate();
        return Task.FromResult<object?>(template);
    }
}
