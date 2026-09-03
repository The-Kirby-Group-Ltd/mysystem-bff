namespace mysystem_bff.Models.Auth
{
    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string VerificationCode { get; set; } = "";
    }
}
