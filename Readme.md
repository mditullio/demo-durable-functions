# Azure Durable Functions PoC: Insurance Hail Claim Workflow

This Proof of Concept (PoC) demonstrates how to implement a stateful, long-running business process using **Azure Durable Functions** (built on the **DurableTask Framework**). 

The scenario focuses on an insurance claim process (Hail Damage) that involves automated retries, human/external interaction, and time-based escalations.

## Overview

### Durable Functions & DurableTask
Durable Functions is an extension of Azure Functions that allows you to write stateful functions in a serverless environment. Behind the scenes, it leverages the **DurableTask Framework (DTFx)** to manage state via Azure Storage (Tables, Queues, and Blobs). This enables "checkpointing": the orchestrator can sleep while waiting for tasks and wake up exactly where it left off, ensuring reliability across restarts or infrastructure failures.

## Core Concepts

### 1. Orchestrator and Activities
* **Orchestrator**: The "brain" of the workflow. It defines the logic and sequence of execution. It must be deterministic because it is frequently replayed to reconstruct state.
* **Activities**: The "workers" that perform the actual I/O operations (e.g., calling an API, database updates).

### 2. Monitoring and Progress
* **customStatus**: Allows the orchestrator to report its internal progress (e.g., "Waiting for Garage") to external observers before the workflow is completed.
* **output**: The final serialized result returned by the orchestrator upon completion.

### 3. Interaction and Timing
* **waitForEvent**: Pauses the orchestrator until a specific external signal (JSON payload or simple trigger) is received.
* **Timers**: Native support for delays or timeouts without consuming CPU cycles during the wait.

### 4. Versioning
In production environments, changing the code of an active orchestrator can lead to non-deterministic errors. This PoC considers **Side-by-Side deployment** (e.g., `Orchestrator_v1`, `Orchestrator_v2`) as the safest strategy for long-running processes (e.g., 30-day timers).

## Scenario: Hail Claim Processing

The provided C# example models a claim workflow with the following features:

* **Automatic Retry Policy**: When assigning a garage, the system uses a `RetryPolicy` (3 attempts with exponential backoff) to handle transient failures in external legacy APIs.
* **Fan-in Pattern (Timer vs. Event)**: The orchestrator waits for either a garage estimate (`EstimateReceived`) or a 7-day timeout using `Task.WhenAny`. 
    * If the estimate arrives, the timer is canceled.
    * If the timer expires first, the workflow triggers an escalation to a human inspector.

## Admin APIs & Monitoring

The framework provides built-in HTTP endpoints to manage and inspect instances. In this PoC, we use the **Instance History API** to audit execution:

* **Endpoint**: `GET /runtime/webhooks/durabletask/instances/{instanceId}?showHistory=true&showHistoryOutput=true`
* **Usage**: This allows developers and support teams to see every step the orchestrator took, including timestamps and activity inputs/outputs.

## Project Structure

* **C# Source Code**: Developed using the **.NET 8 Isolated Worker Model**.
* **Bruno Collection**: A set of `.yml` files for the Bruno API client to test starting workflows, raising events, and checking status.
* **PowerShell / AZ CLI**: A deployment script to provision the Azure Resource Group, Storage Account, and Function App (Flex Consumption Plan).

## Deployment

To deploy the infrastructure, use the provided PowerShell script:

#### Example (AZ CLI snippet):
```bash
az functionapp create `
    --resource-group $RG `
    --name $FUNC `
    --storage-account $SA `
    --flexconsumption-location $LOC `
    --runtime dotnet-isolated `
    --runtime-version 8.0 `
    --functions-version 4
```

## Additional Considerations for Production

If you decide to evolve this PoC, consider the following:

1.  **Application Insights**: Essential for advanced KQL querying of workflow logs.
2.  **Security**: Use **VNet Integration** (available in Flex Consumption) to connect securely to on-premise or enterprise systems.
3.  **Scalability**: Configure `maxConcurrentOrchestratorFunctions` and `maxConcurrentActivityFunctions` in `host.json` to prevent overwhelming downstream legacy systems.
4.  **Error Handling**: Implement **Compensating Transactions** if an activity fails after other activities have already committed data.