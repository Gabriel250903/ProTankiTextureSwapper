using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Serilog;

namespace TextureSwapper.Helpers.Utilities
{
    public static class IntegrityChecker
    {
        private const string RegistrySubKey = @"Software\ProTankiTextureSwapper";
        private const string HashRegistryValue = "AssemblyIntegrity";

        public static bool ValidateAssemblyIntegrity()
        {
#if DEBUG
            try
            {
                string? dbgPath = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(dbgPath) && File.Exists(dbgPath))
                {
                    StoreHash(ComputeFileHash(dbgPath));
                }
            }
            catch { }
            return true;
#else
            try
            {
                string? assemblyPath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
                {
                    Log.Error("Assembly location unavailable. Integrity check failed.");
                    return false;
                }

                string sigPath = assemblyPath + ".sig";
                if (!File.Exists(sigPath))
                {
                    Log.Warning(
                        "Missing companion cryptographic signature (.sig). Integrity check failed."
                    );
                    return false;
                }

                try
                {
                    byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
                    byte[] sigBytes = File.ReadAllBytes(sigPath);

                    using ECDsa ecdsa = ECDsa.Create();
                    ecdsa.ImportSubjectPublicKeyInfo(
                        Convert.FromBase64String(Core.Constants.PublisherPublicKey),
                        out _
                    );

                    if (!ecdsa.VerifyData(assemblyBytes, sigBytes, HashAlgorithmName.SHA256))
                    {
                        Log.Warning(
                            "Assembly cryptographic signature check FAILED! Binary has been tampered with."
                        );
                        return false;
                    }

                    Log.Debug("Assembly cryptographic signature verified successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to verify assembly cryptographic signature.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Assembly integrity check encountered a fatal error.");
                return false;
            }
#endif
        }

        public static void ResetStoredHash()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    RegistrySubKey,
                    writable: true
                );
                key?.DeleteValue(HashRegistryValue, throwOnMissingValue: false);
                Log.Debug("Assembly integrity hash reset.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to reset assembly integrity hash.");
            }
        }

        private static string ComputeFileHash(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            byte[] hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash);
        }

        private static void StoreHash(string hash)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistrySubKey);

                byte[] encryptedBytes = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(hash),
                    null,
                    DataProtectionScope.CurrentUser
                );
                key.SetValue(HashRegistryValue, Convert.ToBase64String(encryptedBytes));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to store assembly hash to registry.");
            }
        }
    }
}
