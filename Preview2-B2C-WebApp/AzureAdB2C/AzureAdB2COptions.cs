namespace Microsoft.AspNetCore.Authentication.Extensions
{
    public class AzureAdB2COptions
    {
        public const string PolicyAuthenticationProperty = "Policy";
        public string ClientId { get; set; }
        public string Instance { get; set; }
        public string Domain { get; set; }
        public string EditProfilePolicyId { get; set; }
        public string SignUpSignInPolicyId { get; set; }
        public string ResetPasswordPolicyId { get; set; }
        public string CallbackPath { get; set; }
        public string DefaultPolicy => SignUpSignInPolicyId;

        public string Authority => $"{Instance}/{Domain}/{SignUpSignInPolicyId}/v2.0";

        public string ClientSecret { get; set; }
        public string RedirectUri { get; set; }
        public string ApiUrl { get; set; }
        public string ApiScopes { get; set; }
    }
}
