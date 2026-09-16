namespace mysystem_bff.Services.Interfaces.Security
{
    public interface IPortalEmailService
    {
        Task SendPasswordVerificationCodeAsync(
            string email,
            string firstName,
            string verificationCode,
            CancellationToken ct = default);
    }
}