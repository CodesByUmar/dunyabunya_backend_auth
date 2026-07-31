using DnsClient;

namespace AuthApi.Services;

public class EmailDomainValidatorService : IEmailDomainValidatorService
{
    private readonly LookupClient _dnsClient = new();

    public async Task<bool> HasValidMailServerAsync(string email)
    {
        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1) return false;

        var domain = email[(atIndex + 1)..];

        try
        {
            var mxResult = await _dnsClient.QueryAsync(domain, QueryType.MX);
            var mxRecords = mxResult.Answers.MxRecords().ToList();

            if (!mxRecords.Any()) return false;

            // RFC 7505 "Null MX": exchange = "." degani - bu domen ATAYIN
            // xat qabul qilmasligini bildiradi (masalan example.com shunday sozlangan)
            var hasRealMailServer = mxRecords.Any(mx => mx.Exchange.Value != ".");

            return hasRealMailServer;
        }
        catch
        {
            return false;
        }
    }
}