namespace VTT.Control
{
    using SixLabors.ImageSharp;
    using System;
    using System.IO;
    using VTT.Util;

    public class ChatBlock
    {
        private Color color;

        public string Text { get; set; }
        public string Tooltip { get; set; }
        public Color Color
        {
            get => this.color.Rgba() == 0 ? Extensions.FromAbgr(ImGuiNET.ImGui.GetColorU32(ImGuiNET.ImGuiCol.Text)) : this.color;
            set => this.color = value;
        }

        public ChatBlockType Type { get; set; }
        public bool DoNotPersist { get; set; }

        public void Write(BinaryWriter bw)
        {
            bw.Write(this.Text);
            bw.Write(this.Tooltip);
            bw.Write(this.color.Argb());
            bw.Write((byte)this.Type);
        }

        public void Read(BinaryReader br)
        {
            this.Text = br.ReadString();
            this.Tooltip = br.ReadString();
            this.Color = Extensions.FromArgb(br.ReadUInt32());
            this.Type = (ChatBlockType)br.ReadByte();
        }
    }

    [Flags]
    public enum ChatBlockType
    {
        Text = 1,
        Expression = 2,
        Error = 4,
        Compound = 8,

        ExpressionError = Expression | Error,
        TextError = Text | Error,
    }
}