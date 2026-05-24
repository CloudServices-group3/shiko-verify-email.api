using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using VerifyEmail.API.Messages;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache(); // Add Memory cache to be used for TEMP storage in Client.

// Register a singleton ServiceBusSender used to send messages to a queue in Azure Servvice Bus.
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["ServiceBus:ConnectionString"];

    var client = new ServiceBusClient(connectionString); 

    var queueName = config["ServiceBus:QueueName"];

    return client.CreateSender(queueName); 
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Send verification code.
app.MapPost("/send-code", async (string email, ServiceBusSender sender, IMemoryCache cache) => 
{
    if (string.IsNullOrWhiteSpace(email))
        return Results.BadRequest("An Email address must be provided.");

    var code = RandomNumberGenerator.GetInt32(100000, 1_000_000).ToString(); // Creates a random number between 100000 and 999999.
     
    cache.Set($"verify:{email}", code, TimeSpan.FromMinutes(5)); // Save Email, code and expiration time in cache. After 5min the code expires.

    var message = new EmailVerificationMessage(email, code);

    var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message)); // Converts the message to JSON and wraps it into binarydata to the service bus.

    await sender.SendMessageAsync(sbMessage); // Sends the message to the service bus.

    return Results.Ok();
});

// Verify code.
app.MapPost("/verify-code", async (string email, string code, IMemoryCache cache) =>
{
    if (string.IsNullOrWhiteSpace(email))
        return Results.BadRequest("An Email address must be provided.");

    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest("A verification code must be provided.");

    if (!cache.TryGetValue($"verify:{email}", out string? storedCode)) // Check -> Email in cache? , Fetch stored code, compare with user input.
        return Results.Unauthorized();

    if (storedCode != code)
        return Results.Unauthorized();

    // If verify is success = remove from cache.
    cache.Remove($"verify:{email}");

    return Results.Ok(new { verified = true });
    
});

app.MapPost("/resend-code", async (string email, ServiceBusSender sender, IMemoryCache cache) =>
{
    if (string.IsNullOrWhiteSpace(email))
        return Results.BadRequest("An Email address must be provided.");

    cache.Remove($"verify:{email}"); // Deletes the cached object associated with the email before creating a new one.

    var code = RandomNumberGenerator.GetInt32(100000, 1_000_000).ToString();

    cache.Set($"verify:{email}", code, TimeSpan.FromMinutes(5));

    var message = new EmailVerificationMessage(email, code);

    var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message));

    await sender.SendMessageAsync(sbMessage);

    return Results.Ok();
});

app.Run();