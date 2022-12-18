namespace VTT.Util
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.Intrinsics.Arm;
    using System.Security.Cryptography;

    public static class IDUtil
    {
        private static Guid DeviceID { get; set; }
        private static byte[] DeviceSecret { get; set; }

        public static byte[] GetSecret() => DeviceSecret;
        public static Guid GetDeviceID(Logger logger = null)
        {
            if (DeviceID == Guid.Empty)
            {
                string expectedLoc = Path.Combine(IOVTT.ClientDir, "guid.bin");
                try
                {
                    byte[] fBytes = File.ReadAllBytes(expectedLoc);
                    if (fBytes.Length == 16) // V1 Guid
                    {
                        logger?.Log(LogLevel.Warn, "Client using outdated unique id version, secret generated!");
                        DeviceID = new Guid(fBytes);
                        GenerateSecret();
                        SaveData(expectedLoc);
                    }
                    else
                    {
                        if (fBytes.Length == 48) // V2 Guid + Secret
                        {
                            DeviceID = new Guid(new ReadOnlySpan<byte>(fBytes, 0, 16));
                            DeviceSecret = new byte[32];
                            Array.Copy(fBytes, 16, DeviceSecret, 0, 32);
                        }
                        else
                        {
                            throw new Exception("Unsupported guid byte length!");
                        }
                    }

                    return DeviceID;
                }
                catch
                {
                    logger?.Log(LogLevel.Error, "Client ID info could not be parsed!");
                }

                DeviceID = Guid.NewGuid();
                GenerateSecret();
                SaveData(expectedLoc);
            }

            return DeviceID;
        }

        private static void GenerateSecret()
        {
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            DeviceSecret = new byte[32];
            rng.GetBytes(DeviceSecret);
        }

        private static void SaveData(string loc)
        {
            byte[] data = new byte[16 + DeviceSecret.Length];
            Array.Copy(DeviceID.ToByteArray(), 0, data, 0, 16);
            Array.Copy(DeviceSecret, 0, data, 16, DeviceSecret.Length);
            File.WriteAllBytes(loc, data);
        }
    }
}
