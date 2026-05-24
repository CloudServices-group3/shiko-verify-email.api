using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace VerifyEmail.API.Services;

public class EmailVerificationService
{
    public string GenerateCode()
    {
        var verificationCode = RandomNumberGenerator.GetInt32(100000, 1_000_000).ToString();

        return verificationCode;
    }

    public bool ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var regEx = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

        if (!Regex.IsMatch(email, regEx))
            return false;

        return true;  
    }

    public bool CompareCodes(string storedCode, string inputCode)
    {
        return storedCode == inputCode;
    }
}
