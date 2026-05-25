using VerifyEmail.API.Services;

namespace VerifyEmail.Tests;

public class EmailVerificationServiceTests
{
    private readonly EmailVerificationService _service = new();

    [Fact]
    public void GenerateCode_ShouldReturnSixDigits()
    {
        var code = _service.GenerateCode();

        Assert.Equal(6, code.Length);
    }

    [Fact]
    public void ValidateEmail_ShouldReturnTrue_ForValidEmail()
    {
        var result = _service.ValidateEmail("test@domain.com");

        Assert.True(result);
    }

    [Fact]
    public void ValidateEmail_ShouldReturnFalse_ForInvalidEmail()
    {
        var result = _service.ValidateEmail("invalid-email");

        Assert.False(result);
    }

    [Fact]
    public void CompareCodes_ShouldReturnTrue_WhenCodesMatch()
    {
        var result = _service.CompareCodes("123456", "123456");

        Assert.True(result);
    }

    [Fact]
    public void CompareCodes_ShouldReturnFalse_WhenCodesDoNotMatch()
    {
        var result = _service.CompareCodes("123456", "012345");

        Assert.False(result);
    }

}
