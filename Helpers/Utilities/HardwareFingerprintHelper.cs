using System.Management;
using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace TextureSwapper.Helpers.Utilities
{
    public static class HardwareFingerprintHelper
    {
        private const string HWIDSalt = "TS_HWID_SALT_2026_v2";

        private static readonly HashSet<string> GenericPlaceholders = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "to be filled by o.e.m.",
            "default string",
            "none",
            "chassis serial number",
            "system serial number",
            "not applicable",
            "empty",
            "0",
            "o.e.m.",
            "oem",
            "na",
            "n/a",
            "unknown",
            "ffffffff-ffff-ffff-ffff-ffffffffffff",
        };

        public static string GetMachineHwid()
        {
            string systemUuid = QueryWmi("Win32_ComputerSystemProduct", "UUID");
            string cpuId = QueryWmi("Win32_Processor", "ProcessorId");
            string moboSerial = QueryWmi("Win32_BaseBoard", "SerialNumber");
            string osDriveSerial = GetOsDriveVolumeSerial();

            string rawFingerprint = $"{systemUuid}|{cpuId}|{moboSerial}|{osDriveSerial}|{HWIDSalt}";

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawFingerprint));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string GetOsDriveVolumeSerial()
        {
            try
            {
                string systemDrive = Environment.GetFolderPath(Environment.SpecialFolder.System)[
                    ..2
                ];
                using ManagementObjectSearcher searcher = new(
                    $"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='{systemDrive}'"
                );
                using ManagementObjectCollection collection = searcher.Get();
                foreach (ManagementBaseObject baseObj in collection)
                {
                    using ManagementObject obj = (ManagementObject)baseObj;
                    string? value = obj["VolumeSerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(value) && !IsGenericPlaceholder(value))
                    {
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HardwareFingerprint: OS Drive Volume Serial query failed.");
            }
            return string.Empty;
        }

        private static string QueryWmi(string wmiClass, string propertyName)
        {
            try
            {
                using ManagementObjectSearcher searcher = new(
                    $"SELECT {propertyName} FROM {wmiClass}"
                );
                using ManagementObjectCollection collection = searcher.Get();
                foreach (ManagementBaseObject baseObj in collection)
                {
                    using ManagementObject obj = (ManagementObject)baseObj;
                    string? value = obj[propertyName]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(value) && !IsGenericPlaceholder(value))
                    {
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(
                    ex,
                    "HardwareFingerprint: WMI query for {Class}.{Property} failed.",
                    wmiClass,
                    propertyName
                );
            }

            return string.Empty;
        }

        private static bool IsGenericPlaceholder(string value)
        {
            return GenericPlaceholders.Contains(value.Trim());
        }
    }
}
