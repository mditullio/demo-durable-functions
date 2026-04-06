using DemoDurableFunctions.Activities;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

public static class HailClaimWorkflow
{

    public record Status(string StatusCode, string ClaimId, string? GarageAssigned = null, int? Estimate = null);

    [Function(nameof(HailClaimWorkflow))]
    public static async Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var claimId = context.GetInput<string>()!;
        var status = new Status("Started", claimId);
        context.SetCustomStatus(status);

        // Retry policy: 3 attempts, starting at 1 minute apart
        var retryOptions = TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromMinutes(1),
            backoffCoefficient: 2
        ));

        // 1. Assign garage via unstable external API
        status = new Status("WaitingForGarageAssignment", claimId);
        context.SetCustomStatus(status);
        var garageAssigned = await context.CallActivityAsync<string>(nameof(AssignPartnerGarage), claimId, retryOptions);
               
        // 2. Wait for the garage estimate OR a 7-day timeout
        using var timeoutCts = new CancellationTokenSource();
        DateTime dueTime = context.CurrentUtcDateTime.AddMinutes(1);

        Task timeoutTask = context.CreateTimer(dueTime, timeoutCts.Token);
        Task<int> estimateTask = context.WaitForExternalEvent<int>("EstimateReceived");

        // Fan-in: wait for the first task to complete
        status = new Status("WaitingForEstimateOrTimeout", claimId, garageAssigned);
        context.SetCustomStatus(status);
        Task winner = await Task.WhenAny(estimateTask, timeoutTask);

        if (winner == estimateTask)
        {
            // Estimate received. Cancel the timer and proceed.
            timeoutCts.Cancel();
            var estimateAmount = estimateTask.Result;
            await context.CallActivityAsync(nameof(AutoApproveEstimate), new AutoApproveEstimate.Input(claimId, estimateAmount));
            status = new Status("EstimateApproved", claimId, garageAssigned, estimateAmount);
            context.SetCustomStatus(status);
        }
        else
        {
            // Timer expired before the event arrived. Escalate.
            await context.CallActivityAsync(nameof(EscalateToHumanInspector), claimId);
            status = new Status("EscalatedToHumanInspector", claimId, garageAssigned);
            context.SetCustomStatus(status);
        
        }

        return "Completed successfully.";
    }

    [Function($"{nameof(HailClaimWorkflow)}_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [FromQuery] string claimId,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger($"{nameof(HailClaimWorkflow)}_HttpStart");

        // Function input comes from the request content.
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(HailClaimWorkflow), claimId);

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function($"{nameof(HailClaimWorkflow)}_HttpRaiseEstimateReceived")]
    public static async Task<IActionResult> HttpRaiseEstimateReceived(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req,
        [FromQuery] string instanceId,
        [FromQuery] int estimate,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger($"{nameof(HailClaimWorkflow)}_HttpRaiseEstimateReceived");
        logger.LogInformation("Raising 'EstimateReceived' event for instance ID = '{instanceId}' with estimate = {estimate}.", instanceId, estimate);
        try
        {
            await client.RaiseEventAsync(instanceId, "EstimateReceived", estimate);
        }
        catch (RpcException exc)
        {
            return new BadRequestObjectResult(exc.Status.Detail);
        }

        return new AcceptedResult();
    }

}