namespace VTT.Control
{
    using System;
    using System.Numerics;
    using VTT.Util;

    public class Tag : ISerializable
    {
        public Guid ID { get; set; }

        public string Text { get; set; }
        public TagKind Kind { get; set; }
        public ShapeKind Shape { get; set; }
        public Vector4 Color1 { get; set; }
        public Vector4 Color2 { get; set; }
        public Vector4 TextColor { get; set; }
        public float BorderRounding { get; set; }
        public Guid AssetID { get; set; }
        public string EmbedB64Image { get; set; }
        public bool IsPublic { get; set; }

        public void Deserialize(DataElement e)
        {
            this.ID = e.GetGuid("ID");
            this.Kind = e.GetEnum<TagKind>("Kind");
            this.Text = e.GetString("Text");
            this.Color1 = e.GetVec4("Clr1");
            this.Color2 = e.GetVec4("Clr2");
            this.TextColor = e.GetVec4("Clr3");
            this.BorderRounding = e.GetSingle("BRound");
            this.AssetID = e.GetGuid("AssetID");
            this.EmbedB64Image = e.GetString("B64Img");
            this.IsPublic = e.GetBool("Public");
            this.Shape = e.GetEnum<ShapeKind>("Shape");
        }

        public DataElement Serialize()
        {
            DataElement ret = new();
            ret.SetGuid("ID", this.ID);
            ret.SetEnum("Kind", this.Kind);
            ret.SetString("Text", this.Text);
            ret.SetVec4("Clr1", this.Color1);
            ret.SetVec4("Clr2", this.Color2);
            ret.SetVec4("Clr3", this.TextColor);
            ret.SetSingle("BRound", this.BorderRounding);
            ret.SetGuid("AssetID", this.AssetID);
            ret.SetString("B64Img", this.EmbedB64Image);
            ret.SetBool("Public", this.IsPublic);
            ret.SetEnum("Shape", this.Shape);
            return ret;
        }

        public Tag Clone(bool copyID = false)
        {
            Tag ret = new Tag()
            {
                ID = copyID ? this.ID : Guid.NewGuid(),
                Kind = this.Kind,
                Text = this.Text,
                Color1 = this.Color1,
                Color2 = this.Color2,
                TextColor = this.TextColor,
                BorderRounding = this.BorderRounding,
                AssetID = this.AssetID,
                EmbedB64Image = this.EmbedB64Image,
            };

            return ret;
        }

        public enum TagKind
        {
            None,
            Shape,
            Text,
            CustomImageAsset,
            CustomImageB64
        }

        public enum ShapeKind
        {
            Circle,
            Square,
            RoundedSquare,
            UpTriangle,
            DownTriangle,
            LeftTriangle,
            RightTriangle,
            Star,
            OvalV,
            OvalH,
            Diamond,
            RhombusV,
            RhombusH,
            UpSemicircle,
            DownSemicircle,
            LeftSemicircle,
            RightSemicircle,
            Octagon,
            HexagonV,
            HexagonH,
            Pentagon,
            Flower
        }
    }
}
