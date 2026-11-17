using Microsoft.AI.Foundry.Local;

namespace ElBruno.MAF.FoundryLocal;

public sealed class FoundryLocalDecisionAdapter(FoundryLocalModelLifecycleService modelLifecycleService)
{
    private readonly FoundryLocalModelLifecycleService _modelLifecycleService = modelLifecycleService;

    public Task<DecisionResult> DecideAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
        => _modelLifecycleService.DecideAsync(request, cancellationToken);

    public Task<DecisionResult> DecideAsync(
        string modelAlias,
        DecisionRequest request,
        CancellationToken cancellationToken = default)
        => _modelLifecycleService.DecideAsync(modelAlias, request, cancellationToken);
}
