namespace Contoso.SendEmailWithApproval
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using System.Text.Json;
    using System.Net.Http;
    using System.Text;
    using Markdig;

    public class WorkflowInput : Input
    {
        public string FormInputId { get; set; }
        public string Message { get; set; }
        public string Subject { get; set; }
        public string Name { get; set; }
        public string Industry { get; set; }
        public string BusinessProcessDescription { get; set; }
        public string ProcessFrequency { get; set; }
        public string ProcessDuration { get; set; }
    }


    public class ApprovalInput
    {
        public string InstanceId { get; set; }
        public string FormInputId { get; set; }
        public string Message { get; set; }
        public string Subject { get; set; }
        public string Name { get; set; }
        public string Industry { get; set; }
        public string BusinessProcessDescription { get; set; }
        public string ProcessFrequency { get; set; }
        public string ProcessDuration { get; set; }
    }

    public class ApprovalCompleteArgs
    {
        public bool Approved { get; set; }
        public string Reason { get; set; }
        public string CorrelationId { get; set; }
    }

    public class ApprovalState
    {
        public bool Approved { get; set; }
        public string Reason { get; set; }
    }

    public class ApprovalWithHumanInteraction
    {
        const string INPUT_QUEUE = "agents-sample-approval-input";
        const string OUTPUT_QUEUE = "agents-sample-approval-output";
        static readonly string AZURE_COMMUNICATION_SERVICE_URI = Environment.GetEnvironmentVariable("AZURE_COMMUNICATION_SERVICE_URI");
        static readonly string APPROVAL_HTTP_TRIGGER_FUNCTION_URL = Environment.GetEnvironmentVariable("APPROVAL_HTTP_TRIGGER_FUNCTION_URL");
        static readonly string SEND_EMAIL_URL = "https://prod-111.westus.logic.azure.com:443/workflows/2c9379aa818c43f2b9a56abd291309bb/triggers/manual/paths/invoke?api-version=2016-06-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=e6yQdSJKCEdWdbySWBZoF7_UcPNC2pfFs_dJomAOLpw";
        static readonly string START_APPROVAL_URL = "https://7ba508bde09d.ngrok.app/api/lead-form-submissions/start-approval";

        [FunctionName(nameof(TriggerOrchestrator))]
        public static async Task TriggerOrchestrator(
            [QueueTrigger(INPUT_QUEUE, Connection = "AzureWebJobsStorage")] WorkflowInput input,
            [DurableClient] IDurableOrchestrationClient durableOrchestrationClient,
            ILogger log)
        {
            // initiate the overall workflow by starting the orchestrator.
            var instanceId = await durableOrchestrationClient.StartNewAsync(nameof(WorkflowOrchestrator), input);
            log.LogInformation($"Started orchestration instanceId: {instanceId}");
        }

        /// <summary>
        /// Orchestration function to start approval process
        /// </summary>
        [FunctionName(nameof(WorkflowOrchestrator))]
        public static async Task WorkflowOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            log.LogInformation($"{nameof(WorkflowOrchestrator)} triggered, orchestration instanceId: {context.InstanceId}");
            var workflowInput = context.GetInput<WorkflowInput>();

            ApprovalInput approvalInput = new ApprovalInput
            {
                InstanceId = context.InstanceId,
                FormInputId = workflowInput.FormInputId,
                Message = workflowInput.Message,
                Subject = workflowInput.Subject,
                Name = workflowInput.Name,
                Industry = workflowInput.Industry,
                BusinessProcessDescription = workflowInput.BusinessProcessDescription,
                ProcessFrequency = workflowInput.ProcessFrequency,
                ProcessDuration = workflowInput.ProcessDuration
            };

            // Call the activity function to send the approval request
            await context.CallActivityAsync(nameof(SendApprovalRequestAsync), approvalInput);

            // wait for the approval response
            var response = await context.WaitForExternalEvent<ApprovalState>("ApprovalResponse");

            log.LogInformation($"{nameof(WorkflowOrchestrator)} ApprovalResponse received: {response}, orchestration instanceId: {context.InstanceId}");

            // After the approval process is complete, send the email
            if (response.Approved) // Optional: Only send an email if approved
            {
                await context.CallActivityAsync(nameof(SendEmailAfterApproval), workflowInput);
            }

            // complete the approval process
            await context.CallActivityAsync(
                nameof(CompleteApprovalAsync),
                new ApprovalCompleteArgs
                {
                    Approved = response.Approved,
                    Reason = response.Reason,
                    CorrelationId = workflowInput.CorrelationId,
                });
        }

        [FunctionName(nameof(SendApprovalRequestAsync))]
        public static async Task SendApprovalRequestAsync(
            [ActivityTrigger] ApprovalInput approvalInput,
            ILogger log)
        {
            using var httpClient = new HttpClient();

            // Prepare the request payload
            var approvalRequest = new
            {
                instanceId = approvalInput.InstanceId,
                formInputId = approvalInput.FormInputId,
                subject = approvalInput.Subject,
                message = approvalInput.Message,
                name = approvalInput.Name,
                industry = approvalInput.Industry,
                businessProcessDescription = approvalInput.BusinessProcessDescription,
                processFrequency = approvalInput.ProcessFrequency,
                processDuration = approvalInput.ProcessDuration
            };
            
            string jsonRequest = JsonSerializer.Serialize(approvalRequest);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            log.LogInformation($"{nameof(SendApprovalRequestAsync)} Sending approval request.");

            // Send the HTTP request
            var response = await httpClient.PostAsync(START_APPROVAL_URL, content);

            // Check if the request was successful
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to send approval request. Status code: {response.StatusCode}");
            }

            log.LogInformation($"{nameof(SendApprovalRequestAsync)} Approval request sent successfully.");
        }

        [FunctionName(nameof(TriggerApproval))]
        public static async Task<IActionResult> TriggerApproval(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient durableOrchestrationClient,
            ILogger log)
        {
            // Ensure the request content is JSON
            if (req.ContentType == "application/json")
            {
                // Read the request body as a JSON document
                using var stream = req.Body;
                using var jsonDoc = await JsonDocument.ParseAsync(stream);

                // Access the top-level fields
                var root = jsonDoc.RootElement;
                if (root.TryGetProperty("instanceId", out var instanceIdElement) &&
                    root.TryGetProperty("approved", out var approvedElement))
                {
                    var  instanceId = instanceIdElement.GetString();
                    bool approved = approvedElement.GetBoolean();

                    var reason = string.Empty;
                    if (!approved)
                    {
                        root.TryGetProperty("reason", out var r);
                        reason = r.GetString();
                    }

                    ApprovalState approvalState = new ApprovalState
                    {
                        Approved = approved,
                        Reason = reason
                    };

                    log.LogInformation($"{nameof(TriggerApproval)} triggered, target orchestration instanceId:{instanceId}");
                    await durableOrchestrationClient.RaiseEventAsync(instanceId, "ApprovalResponse", approvalState);
                    return new OkObjectResult(new { Approved = approved });
                }
            }

            return new BadRequestResult();
        }

        /// <summary>
        /// Activity function to complete the approval process. This function writes the result to the output queue configured on the assistant.
        /// </summary>
        [FunctionName(nameof(CompleteApprovalAsync))]
        [return: Queue(OUTPUT_QUEUE, Connection = "AzureWebJobsStorage")]
        public static Task<Response> CompleteApprovalAsync(
            [ActivityTrigger] IDurableActivityContext context,
            string instanceId,
            ILogger log)
        {
            log.LogInformation($"{nameof(CompleteApprovalAsync)} triggered, orchestration instanceId:{instanceId}");
            var approvalCompleteArgs = context.GetInput<ApprovalCompleteArgs>();

            string response = approvalCompleteArgs.Approved ? "APPROVED" : "REJECTED"; 
            if (!approvalCompleteArgs.Approved)
            {
                response += $", Reason: {approvalCompleteArgs.Reason}";
            }

            // return the result to the output queue using the standard Response POCO.
            return Task.FromResult(new Response
            {
                Value = response,
                CorrelationId = approvalCompleteArgs.CorrelationId
            });
        }

        [FunctionName(nameof(SendEmailAfterApproval))]
        public static async Task SendEmailAfterApproval(
            [ActivityTrigger] WorkflowInput workflowInput,
            ILogger log)
        {
            using var httpClient = new HttpClient();

            // Prepare the request payload
            var approvalRequest = new
            {
                subject = workflowInput.Subject,
                message = Markdown.ToHtml(workflowInput.Message)
            };
            
            string jsonRequest = JsonSerializer.Serialize(approvalRequest);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            log.LogInformation($"{nameof(SendApprovalRequestAsync)} Sending email.");

            // Send the HTTP request
            var response = await httpClient.PostAsync(SEND_EMAIL_URL, content);

            // Check if the request was successful
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to send approval request. Status code: {response.StatusCode}");
            }

            log.LogInformation($"{nameof(SendApprovalRequestAsync)} email sent successfully.");
        }
    }
}