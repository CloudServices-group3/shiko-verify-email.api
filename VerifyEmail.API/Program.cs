using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache(); // Add Memory cache to be used for TEMP storage in Client.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Temporarily "in-memory" storage in cache. (Fetch from DI)
var codes = app.Services.GetRequiredService<IMemoryCache>();

// Send verification code.
app.MapPost("/send-code", (string email) => 
{
    if (string.IsNullOrWhiteSpace(email))
        return Results.BadRequest("An Email address must be provided.");

    var code = RandomNumberGenerator.GetInt32(100000, 1_000_000).ToString(); // Creates a random number between 100000 and 999999.
     
    codes.Set($"verify:{email}", code, TimeSpan.FromMinutes(5)); // Save Email, code and expiration time in cache. After 5min the code expires.

    return Results.Ok(new { message = "code sent" });
});

// Verify code.
app.MapPost("/verify-code", (string email, string code) =>
{
    if (string.IsNullOrWhiteSpace(email))
        return Results.BadRequest("An Email address must be provided.");

    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest("A verification code must be provided.");

    if (codes.TryGetValue($"verify:{email}", out string? storedCode) && storedCode == code) // Check -> Email in cache? , Fetch stored code, compare with user input.
    {

        return Results.Ok(new { verified = true });
    }

    return Results.Unauthorized();
});

app.MapPost("/resend-code", (string email) =>
{
    if (string.IsNullOrWhiteSpace(email))
        return Results.BadRequest("An Email address must be provided.");

    codes.Remove($"verify:{email}"); // Deletes the cached object associated with the email before creating a new one.

    var code = RandomNumberGenerator.GetInt32(100000, 1_000_000).ToString();

    codes.Set($"verify:{email}", code, TimeSpan.FromMinutes(5));

    return Results.Ok(new { message = "new code sent" });
});

app.Run();