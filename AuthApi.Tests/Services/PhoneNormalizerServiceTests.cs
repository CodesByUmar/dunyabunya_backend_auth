using AuthApi.Services;
using Xunit;

namespace AuthApi.Tests.Services;

public class PhoneNormalizerServiceTests
{
    private readonly PhoneNormalizerService _sut = new();

    [Theory]
    [InlineData("998901234567", "+998901234567")]
    [InlineData("+998901234567", "+998901234567")]
    [InlineData("901234567", "+998901234567")]
    [InlineData("0901234567", "+998901234567")]
    [InlineData("+998 90 123 45 67", "+998901234567")]
    public void TryNormalize_ValidFormats_ReturnsNormalized(string input, string expected)
    {
        var ok = _sut.TryNormalize(input, out var normalized);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("12345")]
    [InlineData("abcdefghij")]
    [InlineData("99901234567")] // 998 bilan boshlanmaydi, noto'g'ri kod
    public void TryNormalize_InvalidFormats_ReturnsFalse(string input)
    {
        var ok = _sut.TryNormalize(input, out var normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalize_Null_ReturnsFalse()
    {
        var ok = _sut.TryNormalize(null!, out var normalized);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalized);
    }
}
