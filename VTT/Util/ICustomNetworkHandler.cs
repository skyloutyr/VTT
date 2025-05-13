namespace VTT.Util
{
    using System.IO;

    public interface ICustomNetworkHandler
    {
        void Write(BinaryWriter bw);
        void Read(BinaryReader br);
    }
}
