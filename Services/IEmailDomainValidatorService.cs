namespace AuthApi.Services;

public interface IEmailDomainValidatorService
{
    Task<bool> HasValidMailServerAsync(string email);
}