namespace mysystem_bff.Services.Interfaces
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