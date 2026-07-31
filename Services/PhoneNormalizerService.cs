using System.Text.RegularExpressions;

namespace AuthApi.Services;

public interface IPhoneNormalizerService
{
    bool TryNormalize(string input, out string normalized);
}

public class PhoneNormalizerService : IPhoneNormalizerService
{
    public bool TryNormalize(string input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var digits = Regex.Replace(input, @"[^\d]", "");

        if (digits.Length == 12 && digits.StartsWith("998"))
        {
            normalized = "+" + digits;
            return true;
        }
        if (digits.Length == 9)
        {
            normalized = "+998" + digits;
            return true;
        }
        if (digits.Length == 10 && digits.StartsWith("0"))
        {
            normalized = "+998" + digits.Substring(1);
            return true;
        }

        return false;
    }
}