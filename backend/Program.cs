using System;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Azure.AI.Projects;
using Azure.Identity;
using Azure;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string connectionString = builder.Configuration.GetConnectionString("DocumentDbConnection");
string databaseName = builder.Configuration.GetConnectionString("DocumentDbName") ?? "BackendMongoDb";
string collectionName = builder.Configuration.GetConnectionString("DocumentCollectionName") ?? "LeadFormSubmissions";
string projectConnection = builder.Configuration.GetConnectionString("AzureAIProjectConnectionString");
string agentId = builder.Configuration.GetConnectionString("AgentId") ?? "AgentId";

builder.Services.AddTransient((_provider) => new MongoClient(connectionString));
builder.Services.AddSingleton((sp)=>{
    AgentsClient client = sp.GetRequiredService<AgentsClient>();
    return client.GetAgent(Environment.GetEnvironmentVariable("AGENT_ID")).Value;
});
builder.Services.AddSingleton((sp)=>{
    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = "8824975f-d18e-4a20-ada9-55b0710d414c"
        });
    return new AgentsClient(projectConnection, credential);
});

var app = builder.Build();

var isSwaggerEnabledFromConfig = bool.TrueString.Equals(builder.Configuration["EnableSwagger"] ?? "", StringComparison.OrdinalIgnoreCase);
if (isSwaggerEnabledFromConfig) 
{
    Console.WriteLine("Swagger enabled via appsettings.json");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || isSwaggerEnabledFromConfig)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/lead-form-submissions", async (MongoClient connection) =>
{
    try
    {
        var database = connection.GetDatabase(databaseName);
        var collection = database.GetCollection<LeadFormSubmission>(collectionName);
        var results = await collection.Find(_ => true).ToListAsync().ConfigureAwait(false);

        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.ToString());
    }
});

app.MapGet("/api/lead-form-submissions/{id}", async (string id, MongoClient connection) =>
{
    try
    {
        var database = connection.GetDatabase(databaseName);
        var collection = database.GetCollection<LeadFormSubmission>(collectionName);
        var result = await collection.FindAsync(record => record.Id == id).ConfigureAwait(false) as LeadFormSubmission;
        
        if (result is null) 
        {
            return Results.NotFound();
        }

        return Results.Created($"/lead-form-submissions/{result.Id}", result);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.ToString());
    }
});

app.MapGet("/api/delete-all-form-submissions", async (MongoClient connection) =>
{
    try
    {
        var database = connection.GetDatabase(databaseName);
        var collection = database.GetCollection<LeadFormSubmission>(collectionName);
        await collection.DeleteManyAsync(_ => true).ConfigureAwait(false);

        return Results.Ok("All form submissions deleted.");
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.ToString());
    }
});

app.MapPost("/api/lead-form-submissions", async (LeadFormSubmission record, MongoClient connection) =>
{
    try
    {
        var database = connection.GetDatabase(databaseName);
        var collection = database.GetCollection<LeadFormSubmission>(collectionName);
        await collection.InsertOneAsync(record).ConfigureAwait(false);
        
        var client = app.Services.GetRequiredService<AgentsClient>();

        Response<AgentThread> threadResponse = await client.CreateThreadAsync();
        AgentThread thread = threadResponse.Value;

        var messagePayload = $@"
        {{
            ""formInputId"": ""{record.Id}"",
            ""name"": ""Mads Bolaris"",
            ""industry"": ""{record.Industry}"",
            ""description"": ""{record.BusinessProcessDescription}"",
            ""frequency"": ""{record.ProcessFrequency}"",
            ""duration"": ""{record.ProcessDuration}""
        }}";

        Response<ThreadMessage> messageResponse = await client.CreateMessageAsync(
            thread.Id,
            MessageRole.User,
            messagePayload);
        ThreadMessage message = messageResponse.Value;

        await client.CreateRunAsync(thread.Id, agentId);

        return Results.Created($"/api/lead-form-submissions/{record.Id}", record);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.ToString());
    }
});

app.MapPost("/api/lead-form-submissions/start-approval", async (ApprovalStart record, MongoClient connection) =>
{
    try
    {
        if (record is null)
        {
            return Results.BadRequest("Invalid or missing record data.");
        }

        var database = connection.GetDatabase(databaseName);
        var collection = database.GetCollection<LeadFormSubmission>(collectionName);

        if (record.FormInputId is null)
        {
            // Create a new record
            var newRecord = new LeadFormSubmission
            {
                Id = ObjectId.GenerateNewId().ToString(),
                InstanceId = record.InstanceId,
                Subject = record.Subject,
                Message = record.Message,
                Name = record.Name,
                Industry = record.Industry,
                BusinessProcessDescription = record.BusinessProcessDescription,
                ProcessFrequency = record.ProcessFrequency,
                ProcessDuration = record.ProcessDuration
            };

            await collection.InsertOneAsync(newRecord).ConfigureAwait(false);

            return Results.Created($"/api/lead-form-submissions/{newRecord.Id}", newRecord);
        }
        else
        {
            // Combine update operations
            var updateDefinition = Builders<LeadFormSubmission>.Update.Combine(
                Builders<LeadFormSubmission>.Update.Set(l => l.InstanceId, record.InstanceId),
                Builders<LeadFormSubmission>.Update.Set(l => l.Subject, record.Subject),
                Builders<LeadFormSubmission>.Update.Set(l => l.Message, record.Message)
            );

            // Perform the update
            var result = await collection.FindOneAndUpdateAsync(
                Builders<LeadFormSubmission>.Filter.Eq(l => l.Id, record.FormInputId),
                updateDefinition
            ).ConfigureAwait(false);

            // if not, create a new record

            if (result is null)
            {
                return Results.NotFound($"No lead form submission found with ID: {record.FormInputId}");
            }
            return Results.Created($"/api/lead-form-submissions/{result.Id}", result);
        }
    }
    catch (Exception ex)
    {
        return Results.Problem($"An error occurred: {ex.Message}", statusCode: 500);
    }
});


app.Run();
