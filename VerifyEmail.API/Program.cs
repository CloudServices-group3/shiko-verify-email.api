using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Scalar.AspNetCore;
using VerifyEmail.API.CORS;
using VerifyEmail.API.DTOs;
using VerifyEmail.API.Messages;
using VerifyEmail.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache(); // Add Memory cache to be used for TEMP storage in Client.
builder.Services.AddCorsConfiguration();

builder.Services.AddScoped<EmailVerificationService>();

// Register two ServiceBusSenders used to send messages to two separate queues in Azure Servvice Bus.
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["ServiceBus:ConnectionString"];

    var client = new ServiceBusClient(connectionString);

    var emailSender = client.CreateSender(config["ServiceBus:EmailQueueName"]);
    var userSender = client.CreateSender(config["ServiceBus:UserQueueName"]);

    return new Dictionary<string, ServiceBusSender>
    {
        ["email"] = emailSender,
        ["user"] = userSender
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Verify Email API";
        options.Theme = ScalarTheme.Default;
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

// Send verification code.
app.MapPost("/send-code", async (SendCodeRequest request, Dictionary<string, ServiceBusSender> senders, IMemoryCache cache, EmailVerificationService service) => 
{
    if (!service.ValidateEmail(request.Email))
        return Results.BadRequest("Invalid email");

    var code = service.GenerateCode();
     
    cache.Set($"verify:{request.Email}", code, TimeSpan.FromMinutes(5)); // Save Email, code and expiration time in cache. After 5min the code expires.

    var message = new EmailVerificationMessage(request.Email, code);

    var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message)); // Converts the message to JSON and wraps it into binarydata to the service bus.

    await senders["email"].SendMessageAsync(sbMessage); // Sends the message to the service bus.

    return Results.Ok();
});

// Verify code.
app.MapPost("/verify-code", async (VerifyCodeRequest request, Dictionary<string, ServiceBusSender> senders, IMemoryCache cache, EmailVerificationService service) =>
{
    if (!service.ValidateEmail(request.Email))
        return Results.BadRequest("Invalid email");

    if (string.IsNullOrWhiteSpace(request.Code))
        return Results.BadRequest("A verification code must be provided.");

    if (!cache.TryGetValue($"verify:{request.Email}", out string? storedCode)) // Check -> Email in cache? , Fetch stored code.
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(storedCode))
        return Results.Unauthorized();

    var isValid = service.CompareCodes(storedCode, request.Code);

    if (!isValid)
        return Results.Unauthorized();

    // If verify is success = remove from cache.
    cache.Remove($"verify:{request.Email}");

    // Creates an anonymous object and sends it to the service bus user queue.
    await senders["user"].SendMessageAsync(
        new ServiceBusMessage(BinaryData.FromObjectAsJson(new { Email = request.Email, Type = "EmailVerified" })));

    return Results.Ok(new { verified = true });
    
});

app.MapPost("/resend-code", async (SendCodeRequest request, Dictionary<string, ServiceBusSender> senders, IMemoryCache cache, EmailVerificationService service) =>
{
    if (!service.ValidateEmail(request.Email))
        return Results.BadRequest("Invalid email");

    cache.Remove($"verify:{request.Email}"); // Deletes the cached object associated with the email before creating a new one.

    var code = service.GenerateCode();

    cache.Set($"verify:{request.Email}", code, TimeSpan.FromMinutes(5));

    var message = new EmailVerificationMessage(request.Email, code);

    var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message));

    await senders["email"].SendMessageAsync(sbMessage);

    return Results.Ok();
});

app.Run();