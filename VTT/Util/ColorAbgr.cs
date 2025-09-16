namespace VTT.Util
{
    using SixLabors.ImageSharp;
    using System;

    public readonly struct ColorAbgr
    {
        public static readonly uint AliceBlue = Color.AliceBlue.Abgr();

        public static readonly uint AntiqueWhite = Color.AntiqueWhite.Abgr();

        public static readonly uint Aqua = Color.Aqua.Abgr();

        public static readonly uint Aquamarine = Color.Aquamarine.Abgr();

        public static readonly uint Azure = Color.Azure.Abgr();

        public static readonly uint Beige = Color.Beige.Abgr();

        public static readonly uint Bisque = Color.Bisque.Abgr();

        public static readonly uint Black = Color.Black.Abgr();

        public static readonly uint BlanchedAlmond = Color.BlanchedAlmond.Abgr();

        public static readonly uint Blue = Color.Blue.Abgr();

        public static readonly uint BlueViolet = Color.BlueViolet.Abgr();

        public static readonly uint Brown = Color.Brown.Abgr();

        public static readonly uint BurlyWood = Color.BurlyWood.Abgr();

        public static readonly uint CadetBlue = Color.CadetBlue.Abgr();

        public static readonly uint Chartreuse = Color.Chartreuse.Abgr();

        public static readonly uint Chocolate = Color.Chocolate.Abgr();

        public static readonly uint Coral = Color.Coral.Abgr();

        public static readonly uint CornflowerBlue = Color.CornflowerBlue.Abgr();

        public static readonly uint Cornsilk = Color.Cornsilk.Abgr();

        public static readonly uint Crimson = Color.Crimson.Abgr();

        public static readonly uint Cyan = Aqua;

        public static readonly uint DarkBlue = Color.DarkBlue.Abgr();

        public static readonly uint DarkCyan = Color.DarkCyan.Abgr();

        public static readonly uint DarkGoldenrod = Color.DarkGoldenrod.Abgr();

        public static readonly uint DarkGray = Color.DarkGray.Abgr();

        public static readonly uint DarkGreen = Color.DarkGreen.Abgr();

        public static readonly uint DarkGrey = DarkGray;

        public static readonly uint DarkKhaki = Color.DarkKhaki.Abgr();

        public static readonly uint DarkMagenta = Color.DarkMagenta.Abgr();

        public static readonly uint DarkOliveGreen = Color.DarkOliveGreen.Abgr();

        public static readonly uint DarkOrange = Color.DarkOrange.Abgr();

        public static readonly uint DarkOrchid = Color.DarkOrchid.Abgr();

        public static readonly uint DarkRed = Color.DarkRed.Abgr();

        public static readonly uint DarkSalmon = Color.DarkSalmon.Abgr();

        public static readonly uint DarkSeaGreen = Color.DarkSeaGreen.Abgr();

        public static readonly uint DarkSlateBlue = Color.DarkSlateBlue.Abgr();

        public static readonly uint DarkSlateGray = Color.DarkSlateGray.Abgr();

        public static readonly uint DarkSlateGrey = DarkSlateGray;

        public static readonly uint DarkTurquoise = Color.DarkTurquoise.Abgr();

        public static readonly uint DarkViolet = Color.DarkViolet.Abgr();

        public static readonly uint DeepPink = Color.DeepPink.Abgr();

        public static readonly uint DeepSkyBlue = Color.DeepSkyBlue.Abgr();

        public static readonly uint DimGray = Color.DimGray.Abgr();

        public static readonly uint DimGrey = DimGray;

        public static readonly uint DodgerBlue = Color.DodgerBlue.Abgr();

        public static readonly uint Firebrick = Color.Firebrick.Abgr();

        public static readonly uint FloralWhite = Color.FloralWhite.Abgr();

        public static readonly uint ForestGreen = Color.ForestGreen.Abgr();

        public static readonly uint Fuchsia = Color.Fuchsia.Abgr();

        public static readonly uint Gainsboro = Color.Gainsboro.Abgr();

        public static readonly uint GhostWhite = Color.GhostWhite.Abgr();

        public static readonly uint Gold = Color.Gold.Abgr();

        public static readonly uint Goldenrod = Color.Goldenrod.Abgr();

        public static readonly uint Gray = Color.Gray.Abgr();

        public static readonly uint Green = Color.Green.Abgr();

        public static readonly uint GreenYellow = Color.GreenYellow.Abgr();

        public static readonly uint Grey = Gray;

        public static readonly uint Honeydew = Color.Honeydew.Abgr();

        public static readonly uint HotPink = Color.HotPink.Abgr();

        public static readonly uint IndianRed = Color.IndianRed.Abgr();

        public static readonly uint Indigo = Color.Indigo.Abgr();

        public static readonly uint Ivory = Color.Ivory.Abgr();

        public static readonly uint Khaki = Color.Khaki.Abgr();

        public static readonly uint Lavender = Color.Lavender.Abgr();

        public static readonly uint LavenderBlush = Color.LavenderBlush.Abgr();

        public static readonly uint LawnGreen = Color.LawnGreen.Abgr();

        public static readonly uint LemonChiffon = Color.LemonChiffon.Abgr();

        public static readonly uint LightBlue = Color.LightBlue.Abgr();

        public static readonly uint LightCoral = Color.LightCoral.Abgr();

        public static readonly uint LightCyan = Color.LightCyan.Abgr();

        public static readonly uint LightGoldenrodYellow = Color.LightGoldenrodYellow.Abgr();

        public static readonly uint LightGray = Color.LightGray.Abgr();

        public static readonly uint LightGreen = Color.LightGreen.Abgr();

        public static readonly uint LightGrey = LightGray;

        public static readonly uint LightPink = Color.LightPink.Abgr();

        public static readonly uint LightSalmon = Color.LightSalmon.Abgr();

        public static readonly uint LightSeaGreen = Color.LightSeaGreen.Abgr();

        public static readonly uint LightSkyBlue = Color.LightSkyBlue.Abgr();

        public static readonly uint LightSlateGray = Color.LightSlateGray.Abgr();

        public static readonly uint LightSlateGrey = LightSlateGray;

        public static readonly uint LightSteelBlue = Color.LightSteelBlue.Abgr();

        public static readonly uint LightYellow = Color.LightYellow.Abgr();

        public static readonly uint Lime = Color.Lime.Abgr();

        public static readonly uint LimeGreen = Color.LimeGreen.Abgr();

        public static readonly uint Linen = Color.Linen.Abgr();

        public static readonly uint Magenta = Fuchsia;

        public static readonly uint Maroon = Color.Maroon.Abgr();

        public static readonly uint MediumAquamarine = Color.MediumAquamarine.Abgr();

        public static readonly uint MediumBlue = Color.MediumBlue.Abgr();

        public static readonly uint MediumOrchid = Color.MediumOrchid.Abgr();

        public static readonly uint MediumPurple = Color.MediumPurple.Abgr();

        public static readonly uint MediumSeaGreen = Color.MediumSeaGreen.Abgr();

        public static readonly uint MediumSlateBlue = Color.MediumSlateBlue.Abgr();

        public static readonly uint MediumSpringGreen = Color.MediumSpringGreen.Abgr();

        public static readonly uint MediumTurquoise = Color.MediumTurquoise.Abgr();

        public static readonly uint MediumVioletRed = Color.MediumVioletRed.Abgr();

        public static readonly uint MidnightBlue = Color.MidnightBlue.Abgr();

        public static readonly uint MintCream = Color.MintCream.Abgr();

        public static readonly uint MistyRose = Color.MistyRose.Abgr();

        public static readonly uint Moccasin = Color.Moccasin.Abgr();

        public static readonly uint NavajoWhite = Color.NavajoWhite.Abgr();

        public static readonly uint Navy = Color.Navy.Abgr();

        public static readonly uint OldLace = Color.OldLace.Abgr();

        public static readonly uint Olive = Color.Olive.Abgr();

        public static readonly uint OliveDrab = Color.OliveDrab.Abgr();

        public static readonly uint Orange = Color.Orange.Abgr();

        public static readonly uint OrangeRed = Color.OrangeRed.Abgr();

        public static readonly uint Orchid = Color.Orchid.Abgr();

        public static readonly uint PaleGoldenrod = Color.PaleGoldenrod.Abgr();

        public static readonly uint PaleGreen = Color.PaleGreen.Abgr();

        public static readonly uint PaleTurquoise = Color.PaleTurquoise.Abgr();

        public static readonly uint PaleVioletRed = Color.PaleVioletRed.Abgr();

        public static readonly uint PapayaWhip = Color.PapayaWhip.Abgr();

        public static readonly uint PeachPuff = Color.PeachPuff.Abgr();

        public static readonly uint Peru = Color.Peru.Abgr();

        public static readonly uint Pink = Color.Pink.Abgr();

        public static readonly uint Plum = Color.Plum.Abgr();

        public static readonly uint PowderBlue = Color.PowderBlue.Abgr();

        public static readonly uint Purple = Color.Purple.Abgr();

        public static readonly uint RebeccaPurple = Color.RebeccaPurple.Abgr();

        public static readonly uint Red = Color.Red.Abgr();

        public static readonly uint RosyBrown = Color.RosyBrown.Abgr();

        public static readonly uint RoyalBlue = Color.RoyalBlue.Abgr();

        public static readonly uint SaddleBrown = Color.SaddleBrown.Abgr();

        public static readonly uint Salmon = Color.Salmon.Abgr();

        public static readonly uint SandyBrown = Color.SandyBrown.Abgr();

        public static readonly uint SeaGreen = Color.SeaGreen.Abgr();

        public static readonly uint SeaShell = Color.SeaShell.Abgr();

        public static readonly uint Sienna = Color.Sienna.Abgr();

        public static readonly uint Silver = Color.Silver.Abgr();

        public static readonly uint SkyBlue = Color.SkyBlue.Abgr();

        public static readonly uint SlateBlue = Color.SlateBlue.Abgr();

        public static readonly uint SlateGray = Color.SlateGray.Abgr();

        public static readonly uint SlateGrey = SlateGray;

        public static readonly uint Snow = Color.Snow.Abgr();

        public static readonly uint SpringGreen = Color.SpringGreen.Abgr();

        public static readonly uint SteelBlue = Color.SteelBlue.Abgr();

        public static readonly uint Tan = Color.Tan.Abgr();

        public static readonly uint Teal = Color.Teal.Abgr();

        public static readonly uint Thistle = Color.Thistle.Abgr();

        public static readonly uint Tomato = Color.Tomato.Abgr();

        public static readonly uint Transparent = Color.Transparent.Abgr();

        public static readonly uint Turquoise = Color.Turquoise.Abgr();

        public static readonly uint Violet = Color.Violet.Abgr();

        public static readonly uint Wheat = Color.Wheat.Abgr();

        public static readonly uint White = Color.White.Abgr();

        public static readonly uint WhiteSmoke = Color.WhiteSmoke.Abgr();

        public static readonly uint Yellow = Color.Yellow.Abgr();

        public static readonly uint YellowGreen = Color.YellowGreen.Abgr();

        private readonly uint _abgr;
        public readonly uint Abgr => this._abgr;
        public readonly uint Argb => (this._abgr & 0xff00ff00u) | ((this._abgr & 0x00ff0000u) >> 16) | ((this._abgr & 0xff) << 16);

        public ColorAbgr(uint abgr) => this._abgr = abgr;

        public ColorAbgr(byte r, byte g, byte b, byte a = byte.MaxValue)
        {
            this._abgr =
                ((uint)a << 24) |
                ((uint)b << 16) |
                ((uint)g << 8) |
                r;
        }

        public ColorAbgr(float r, float g, float b, float a = 1.0f) : this((byte)Math.Clamp(r * 255.0f, byte.MinValue, byte.MaxValue), (byte)Math.Clamp(g * 255.0f, byte.MinValue, byte.MaxValue), (byte)Math.Clamp(b * 255.0f, byte.MinValue, byte.MaxValue), (byte)Math.Clamp(a * 255.0f, byte.MinValue, byte.MaxValue))
        {
        }


        public static implicit operator uint(ColorAbgr self) => self._abgr;
        public static implicit operator ColorAbgr(uint self) => new ColorAbgr(self);
        public static implicit operator ColorAbgr(Color imgsharpclr) => new ColorAbgr(imgsharpclr.Abgr());
    }
}
