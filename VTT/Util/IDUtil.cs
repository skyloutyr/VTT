namespace VTT.Util
{
    using System;
    using System.IO;

    public static class IDUtil
    {
        private static Guid DeviceID { get; set; }

        public static Guid GetDeviceID()
        {
            if (DeviceID == Guid.Empty)
            {
                string expectedLoc = Path.Combine(IOVTT.ClientDir, "guid.bin");
                try
                {
                    DeviceID = new Guid(File.ReadAllBytes(expectedLoc));
                    return DeviceID;
                }
                catch
                {
                    // NOOP
                }

                DeviceID = Guid.NewGuid();
                File.WriteAllBytes(expectedLoc, DeviceID.ToByteArray());
            }

            return DeviceID;
        }
    }
}
