using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DemoDurableFunctions.Activities;

public static class EscalateToHumanInspector
{

    [Function(nameof(EscalateToHumanInspector))]
    public async static Task<string> Run([ActivityTrigger] string claimId, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(EscalateToHumanInspector));
        await Task.Delay(1000); // Simulate some work
        logger.LogInformation("Escalating claim {claimId} to human inspector", claimId);
        return $"Claim {claimId} escalated to human inspector.";
    }

}