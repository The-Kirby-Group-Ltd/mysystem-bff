using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using mysystem_bff.Services.Interfaces;

namespace mysystem_bff.Services.Services
{
    public class GraphEmailService : IPortalEmailService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly string _senderUser;

        public GraphEmailService(
            IConfiguration configuration)
        {
            var tenantId =
                configuration["MicrosoftGraph:TenantId"];

            var clientId =
                configuration["MicrosoftGraph:ClientId"];

            var clientSecret =
                configuration["MicrosoftGraph:ClientSecret"];

            _senderUser =
                configuration["MicrosoftGraph:SenderUser"]
                ?? "";

            if (
                string.IsNullOrWhiteSpace(tenantId) ||
                string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(clientSecret) ||
                string.IsNullOrWhiteSpace(_senderUser)
            )
            {
                throw new InvalidOperationException(
                    "Microsoft Graph configuration is incomplete."
                );
            }

            var credential =
                new ClientSecretCredential(
                    tenantId,
                    clientId,
                    clientSecret
                );

            _graphClient =
                new GraphServiceClient(
                    credential,
                    new[]
                    {
                        "https://graph.microsoft.com/.default"
                    }
                );
        }

        // =====================================================
        // Password verification email
        // =====================================================

        public async Task SendPasswordVerificationCodeAsync(
            string email,
            string firstName,
            string verificationCode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(email)
                );
            }

            if (string.IsNullOrWhiteSpace(verificationCode))
            {
                throw new ArgumentException(
                    "Verification code is required.",
                    nameof(verificationCode)
                );
            }

            var safeFirstName =
                string.IsNullOrWhiteSpace(firstName)
                    ? "User"
                    : firstName.Trim();

            var message =
                new Message
                {
                    Subject =
                        "mysystem.info password reset verification code",

                    Body =
                        new ItemBody
                        {
                            ContentType =
                                BodyType.Html,

                            Content = $"""
                                <p>Hello {safeFirstName},</p>

                                <p>
                                    A password change was requested
                                    for your mysystem.info account.
                                </p>

                                <p>
                                    Your verification code is:
                                </p>

                                <h2 style="letter-spacing:4px;">
                                    {verificationCode}
                                </h2>

                                <p>
                                    This code expires after 10 minutes.
                                </p>

                                <p>
                                    If you did not request this change,
                                    you can ignore this email.
                                </p>
                                """
                        },

                    ToRecipients =
                    [
                        new Recipient
                        {
                            EmailAddress =
                                new EmailAddress
                                {
                                    Address = email
                                }
                        }
                    ]
                };

            var requestBody =
                new SendMailPostRequestBody
                {
                    Message = message,
                    SaveToSentItems = true
                };

            await _graphClient
                .Users[_senderUser]
                .SendMail
                .PostAsync(
                    requestBody,
                    cancellationToken: ct
                );
        }
    }
}