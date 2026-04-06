using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DemoDurableFunctions.Activities;

public static class AssignPartnerGarage
{

    [Function(nameof(AssignPartnerGarage))]
    public async static Task<string> Run([ActivityTrigger] string claimId, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(AssignPartnerGarage));
        logger.LogInformation("Assigning partner garage for claim {claimId}", claimId);
        await Task.Delay(1000); // Simulate some work

        // Randomly raise an exception to demonstrate error handling
        if (Random.Shared.Next(0, 5) == 0) // 20% chance of failure
        {
            logger.LogError("Failed to assign partner garage for claim {claimId}", claimId);
            throw new Exception($"Failed to assign partner garage for claim {claimId}");
        }

        // Randomly return a garage name
        string[] garages = ["Garage A", "Garage B", "Garage C"];
        return garages[Random.Shared.Next(garages.Length)];
    }

}