using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using VerifyEmail.API.Messages;
using VerifyEmail.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache(); // Add Memory cache to be used for TEMP storage in Client.

builder.Services.AddScoped<EmailVerificationService>();

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
app.MapPost("/send-code", async (string email, ServiceBusSender sender, IMemoryCache cache, EmailVerificationService service) => 
{
    if (!service.ValidateEmail(email))
        return Results.BadRequest("Invalid email");

    var code = service.GenerateCode();
     
    cache.Set($"verify:{email}", code, TimeSpan.FromMinutes(5)); // Save Email, code and expiration time in cache. After 5min the code expires.

    var message = new EmailVerificationMessage(email, code);

    var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message)); // Converts the message to JSON and wraps it into binarydata to the service bus.

    await sender.SendMessageAsync(sbMessage); // Sends the message to the service bus.

    return Results.Ok();
});

// Verify code.
app.MapPost("/verify-code", async (string email, string code, IMemoryCache cache, EmailVerificationService service) =>
{
    if (!service.ValidateEmail(email))
        return Results.BadRequest("Invalid email");

    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest("A verification code must be provided.");

    if (!cache.TryGetValue($"verify:{email}", out string? storedCode)) // Check -> Email in cache? , Fetch stored code.
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(storedCode))
        return Results.Unauthorized();

    var results = service.CompareCodes(storedCode, code);

    if (!service.CompareCodes(storedCode, code))
        return Results.Unauthorized();

    // If verify is success = remove from cache.
    cache.Remove($"verify:{email}");

    return Results.Ok(new { verified = true });
    
});

app.MapPost("/resend-code", async (string email, ServiceBusSender sender, IMemoryCache cache, EmailVerificationService service) =>
{
    if (!service.ValidateEmail(email))
        return Results.BadRequest("Invalid email");

    cache.Remove($"verify:{email}"); // Deletes the cached object associated with the email before creating a new one.

    var code = service.GenerateCode();

    cache.Set($"verify:{email}", code, TimeSpan.FromMinutes(5));

    var message = new EmailVerificationMessage(email, code);

    var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message));

    await sender.SendMessageAsync(sbMessage);

    return Results.Ok();
});

app.Run();