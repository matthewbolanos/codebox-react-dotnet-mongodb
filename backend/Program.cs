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

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string connectionString = builder.Configuration.GetConnectionString("DocumentDbConnection");
string databaseName = builder.Configuration.GetConnectionString("DocumentDbName") ?? "BackendMongoDb";
string collectionName = builder.Configuration.GetConnectionString("DocumentCollectionName") ?? "LeadFormSubmissions";

builder.Services.AddTransient((_provider) => new MongoClient(connectionString));
builder.Services.AddSingleton((sp)=>{
    var connectionString = Environment.GetEnvironmentVariable("PROJECT_CONNECTION_STRING");
    return new AgentsClient(connectionString, new DefaultAzureCredential());
});
builder.Services.AddSingleton((sp)=>{
    AgentsClient client = sp.GetRequiredService<AgentsClient>();
    return client.GetAgent(Environment.GetEnvironmentVariable("AGENT_ID")).Value;
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

app.MapPost("/api/lead-form-submissions", async (LeadFormSubmission record, MongoClient connection) =>
{
    try
    {
        var database = connection.GetDatabase(databaseName);
        var collection = database.GetCollection<LeadFormSubmission>(collectionName);
        await collection.InsertOneAsync(record).ConfigureAwait(false);

        // Use agent to generate

        return Results.Created($"/api/lead-form-submissions/{record.Id}", record);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.ToString());
    }
});

app.Run();
