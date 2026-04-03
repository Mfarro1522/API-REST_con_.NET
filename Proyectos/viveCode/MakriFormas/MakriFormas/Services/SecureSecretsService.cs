using System;
using System.Security.Cryptography;
using System.Text;

namespace MakriFormas.Services
{
    public static class SecureSecretsService
    {
        private const string GoogleApiKeySecret = "AI.GoogleApiKey.Enc";
        private const string GroqApiKeySecret = "AI.GroqApiKey.Enc";

        public static void SetGoogleApiKey(string apiKey) => SetSecret(GoogleApiKeySecret, apiKey);
        public static void SetGroqApiKey(string apiKey) => SetSecret(GroqApiKeySecret, apiKey);

        public static string GetGoogleApiKey() => GetSecret(GoogleApiKeySecret);
        public static string GetGroqApiKey() => GetSecret(GroqApiKeySecret);

        private static void SetSecret(string key, string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                DatabaseService.SetSetting(key, string.Empty);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            DatabaseService.SetSetting(key, Convert.ToBase64String(encrypted));
        }

        private static string GetSecret(string key)
        {
            var encryptedBase64 = DatabaseService.GetSetting(key, string.Empty);
            if (string.IsNullOrWhiteSpace(encryptedBase64))
                return string.Empty;

            try
            {
                var encrypted = Convert.FromBase64String(encryptedBase64);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
