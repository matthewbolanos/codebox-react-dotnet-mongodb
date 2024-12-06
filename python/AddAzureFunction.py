
import json
import os
from typing import Any
from dotenv import load_dotenv
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential
from azure.ai.projects.models import BingGroundingTool
from azure.ai.projects.models import AgentEventHandler
from azure.ai.projects.models import (
    MessageDeltaChunk,
    MessageDeltaTextContent,
    RunStep,
    ThreadMessage,
    ThreadRun,
)

load_dotenv()

class MyEventHandler(AgentEventHandler):

    def on_message_delta(self, delta: "MessageDeltaChunk") -> None:
        for content_part in delta.delta.content:
            if isinstance(content_part, MessageDeltaTextContent):
                text_value = content_part.text.value if content_part.text else "No text"
                print(text_value, end="")

    def on_thread_message(self, message: "ThreadMessage") -> None:
        print(f"ThreadMessage created. ID: {message.id}, Status: {message.status}")

    def on_thread_run(self, run: "ThreadRun") -> None:
        print(f"ThreadRun status: {run.status}")

        if run.status == "failed":
            print(f"Run failed. Error: {run.last_error}")

    def on_run_step(self, step: "RunStep") -> None:
        print(f"RunStep type: {step.type}, Status: {step.status}")
        # print(f"RunStep output: {step}")

    def on_error(self, data: str) -> None:
        print(f"An error occurred. Data: {data}")

    def on_done(self) -> None:
        print("Stream completed.")

    def on_unhandled_event(self, event_type: str, event_data: Any) -> None:
        print(f"Unhandled Event Type: {event_type}, Data: {event_data}")

# Create an Azure AI Client from a connection string, copied from your AI Studio project.
# At the moment, it should be in the format "<HostName>;<AzureSubscriptionId>;<ResourceGroup>;<HubName>"
# Customer needs to login to Azure subscription via Azure CLI and set the environment variables

project_client = AIProjectClient.from_connection_string(
    credential=DefaultAzureCredential(),
    conn_str=os.environ["PROJECT_CONNECTION_STRING"],
)

bing_connection = project_client.connections.get(
    connection_name=os.environ["BING_CONNECTION_NAME"]
)
conn_id = bing_connection.id

agent_id = "asst_RdNeVnLrvNVQ6ez4uNhEZHQ4"

with project_client:
    bing_tool = BingGroundingTool(connection_id=conn_id)
    azure_function_tool_definitions = [{
        "type": "azure_function",
        "azure_function": {
            "function": {
                "name": "SendEmailWithMessage",
                "description": "Allows you to send an email with a message.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "formInputId": {"type": "string", "description": "The ID of the form (required if provided))"},
                        "subject": {"type": "string", "description": "The subject of the email."},
                        "message": {"type": "string", "description": "The message to email."},
                        "name": {"type": "string", "description": "Name of customer (only required if formInputId is not provided)."},
                        "industry": {"type": "string", "description": "Industry of business (only required if formInputId is not provided)."},
                        "businessProcessDescription": {"type": "string", "description": "Description of business process (only required if formInputId is not provided)."},
                        "processFrequency": {"type": "string", "description": "Current frequency of process (only required if formInputId is not provided)."},
                        "processDuration": {"type": "string", "description": "Current duration of process (only required if formInputId is not provided)."}
                    },
                    "required": ["subject", "message"]
                }
            },
            "input_binding": {
                "type": "storage_queue",
                "storage_queue": {
                    "queue_service_uri": "https://rgamanda258b26.queue.core.windows.net",
                    "queue_name": "agents-sample-approval-input"
                }
            },
            "output_binding": {
                "type": "storage_queue",
                "storage_queue": {
                    "queue_service_uri": "https://rgamanda258b26.queue.core.windows.net",
                    "queue_name": "agents-sample-approval-output"
                }
            }
        }
    }]

    tools = bing_tool.definitions + azure_function_tool_definitions

    agent = project_client.agents.update_agent(
        agent_id,
        model="gpt-4o",
        instructions="""
            You will help generate a personalized email message for a customer who
            is interested in automating a business process with AI using Microsoft products.
            You will either get a JSON payload with the following information, or you will
            need to ask the sales representative for this information:

            - The name of the customer
            - The industry of the business
            - A description of the business process
            - The current frequency of the process
            - The current duration of the process

            You will then use this information to perform the following steps in order:

            1. **Perform research** – use the Bing search tool to find relevant information
               about the industry and business process and how AI can be used to automate it.
               It's currently November 21st, 2024, use announcements that just happened at Microsoft Ignite 2024.
               Create 3 parallel searches for features from Copilot Studio, Azure AI Foundry, and Microsoft 365 Copilot.

            2. **Generate email message** – use the SendEmailWithMessage tool to generate a personalized
               email message that includes the information provided by the customer and the
               information found in the research.

            - The name of the sales representative is Mona Whalin.
            - You **must** not include sources in the email.
            - You **must** ensure the email does not include any references to competitors to Microsoft.
            - You **must** tell the user what you're doing as your researching and right before you send the email.
            - You **must** include the name, industry, business process, frequency, and duration in the email if there is no form input ID.
            - You **must** send the email after performing the research whether or not your successful
              finding information about the industry and business process. Do not ask for approval.
              Just do your best!

            If the email fails to send, keep trying. Do not stop.
        """,
        tools=tools,
        headers={"x-ms-enable-preview": "true", "x-aml-bing-grounding-runsteps":"true"}
    )
    
    # Create thread for communication
    thread = project_client.agents.create_thread()
    print(f"Created thread, ID: {thread.id}")

    # Create message to thread
    message = project_client.agents.create_message(
        thread_id=thread.id,
        role="user",
        content="""
            Can you help me send an email to Mads Bolaris from Contoso. He's in the
            retail industry, and he's looking to automate the process of sending out email
            notifications to  customers when their orders are ready for pickup.
            He'd like this to happen daily, and ideally, it shouldn't take more
            than 30 minutes each time
        """,
    )
    print(f"Created message, ID: {message.id}")

    # Create run for agent
    with project_client.agents.create_stream(
        thread_id=thread.id, assistant_id=agent.id, event_handler=MyEventHandler()
    ) as stream:
        stream.until_done()