namespace VTT.Util
{
    using SixLabors.ImageSharp;
    using System;

    public class HSVColor
    {
        public float Hue { get; set; }
        public float Saturation { get; set; }
        public float Value { get; set; }

        public HSVColor(float hue, float saturation, float value)
        {
            this.Hue = hue;
            this.Saturation = saturation;
            this.Value = value;
        }

        public static implicit operator HSVColor(Color self)
        {
            static float ClampDeg(float deg) => deg < 0 ? deg + 360 : deg;

            float rF = self.Red();
            float gF = self.Green();
            float bF = self.Blue();
            float cMax = Math.Max(Math.Max(rF, gF), bF);
            float cMin = Math.Min(Math.Min(rF, gF), bF);
            float cDelta = cMax - cMin;

            // Undefined
            return cDelta < 0.00001F
                ? new HSVColor(0, 0, cMax)
                : cMax <= 0
                ? new HSVColor(0, 0, cMax)
                : new HSVColor(
                ClampDeg((rF == cMax ? (gF - bF) / cDelta :
                gF == cMax ? 2 + ((bF - rF) / cDelta) :
                4 + ((rF - gF) / cDelta)) * 60),
                cDelta / cMax,
                cMax
            );
        }

        // https://stackoverflow.com/questions/3018313/algorithm-to-convert-rgb-to-hsv-and-hsv-to-rgb-in-range-0-255-for-both
        public static implicit operator Color(HSVColor self)
        {
            float hh, p, q, t, ff;
            int i;
            if (self.Saturation <= 0)
            {
                return Extensions.FromArgb(1F, self.Value, self.Value, self.Value);
            }

            hh = self.Hue;
            if (hh > 360)
            {
                hh = 0;
            }

            hh /= 60;
            i = (int)hh;
            ff = hh - i;
            p = self.Value * (1 - self.Saturation);
            q = self.Value * (1 - (self.Saturation * ff));
            t = self.Value * (1 - (self.Saturation * (1 - ff)));
            switch (i)
            {
                case 0:
                {
                    return Extensions.FromArgb(1F, self.Value, t, p);
                }

                case 1:
                {
                    return Extensions.FromArgb(1F, q, self.Value, p);
                }

                case 2:
                {
                    return Extensions.FromArgb(1F, p, self.Value, t);
                }

                case 3:
                {
                    return Extensions.FromArgb(1F, p, q, self.Value);
                }

                case 4:
                {
                    return Extensions.FromArgb(1F, t, p, self.Value);
                }

                default:
                {
                    return Extensions.FromArgb(1F, self.Value, p, q);
                }
            }
        }
    }
}
