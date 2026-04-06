using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DemoDurableFunctions.Activities;

public static class AutoApproveEstimate
{

    public record Input(string ClaimId, int Amount);

    [Function(nameof(AutoApproveEstimate))]
    public async static Task<string> Run([ActivityTrigger] Input input, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(AutoApproveEstimate));
        await Task.Delay(1000); // Simulate some work
        logger.LogInformation("Auto-approving estimate of {amount} for claim {claimId}", input.Amount, input.ClaimId);
        return $"Estimate for claim {input.ClaimId} auto-approved.";
    }

}