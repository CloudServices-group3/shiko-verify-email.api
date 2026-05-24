namespace VerifyEmail.API.Messages;

public sealed record EmailVerificationMessage
(
    string To,
    string VerificationCode
);
