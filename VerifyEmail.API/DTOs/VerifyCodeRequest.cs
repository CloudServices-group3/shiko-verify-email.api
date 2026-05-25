namespace VerifyEmail.API.DTOs;

public sealed record VerifyCodeRequest
(
    string Email,
    string Code
);