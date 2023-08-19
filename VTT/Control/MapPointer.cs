namespace VTT.Control
{
    using System;
    using VTT.GL;

    public class MapPointer
    {
        public Guid MapID { get; set; }
        public Guid PreviewID { get; set; }
        public string MapName { get; set; }
        public bool IsServer { get; set; }

        public Texture MapPreview { get; set; } // Client only, nullable
    }
}
