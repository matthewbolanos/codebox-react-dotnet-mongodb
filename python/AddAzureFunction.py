
import os
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

# Create an Azure AI Client from a connection string, copied from your AI Studio project.
# At the moment, it should be in the format "<HostName>;<AzureSubscriptionId>;<ResourceGroup>;<HubName>"
# Customer needs to login to Azure subscription via Azure CLI and set the environment variables

project_client = AIProjectClient.from_connection_string(
    credential=DefaultAzureCredential(),
    conn_str=os.environ["PROJECT_CONNECTION_STRING"],
)

agent = project_client.agents.update_agent(
    tools=[
        {
            "type": "azure_function",
            "azure_function": {
                "function": {
                    "name": "foo",
                    "description": "Get answers from the foo bot.",
                    "parameters": {
                        "type": "object",
                        "properties": {
                            "query": {"type": "string", "description": "The question to ask."},
                            "outputqueueuri" : {"type": "string", "description": "The full output queue uri."}
                        },
                        "required": ["query"]
                    }
                },
                "input_binding": {
                    "type": "storage_queue",
                    "storage_queue": {
                        "queue_service_uri": "https://stpwieseai96608752216713.queue.core.windows.net",
                        "queue_name": "azure-function-foo-input"
                    }
                },
                "output_binding": {
                    "type": "storage_queue",
                    "storage_queue": {
                        "queue_service_uri": "https://stpwieseai96608752216713.queue.core.windows.net",
                        "queue_name": "azure-function-tool-output"
                    }
                }
            }
        }
    ],
    headers={"x-ms-enable-preview": "true"}
)
    