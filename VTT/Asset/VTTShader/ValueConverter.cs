namespace VTT.Asset.VTTShader
{
    using System.Collections.Generic;

    public readonly struct ValueConverter
    {
        public static ValueConverter Float2Float { get; } = new ValueConverter(ShaderFieldType.Float, ShaderFieldType.Float, "{0}");
        public static ValueConverter Float2Int { get; } = new ValueConverter(ShaderFieldType.Float, ShaderFieldType.Int, "int({0})");
        public static ValueConverter Float2UInt { get; } = new ValueConverter(ShaderFieldType.Float, ShaderFieldType.Uint, "uint({0})");
        public static ValueConverter Float2Vec2 { get; } = new ValueConverter(ShaderFieldType.Float, ShaderFieldType.Vec2, "vec2({0})");
        public static ValueConverter Float2Vec3 { get; } = new ValueConverter(ShaderFieldType.Float, ShaderFieldType.Vec3, "vec3({0})");
        public static ValueConverter Float2Vec4 { get; } = new ValueConverter(ShaderFieldType.Float, ShaderFieldType.Vec4, "vec4({0})");

        public static ValueConverter Int2Float { get; } = new ValueConverter(ShaderFieldType.Int, ShaderFieldType.Float, "float({0})");
        public static ValueConverter Int2Int { get; } = new ValueConverter(ShaderFieldType.Int, ShaderFieldType.Int, "{0}");
        public static ValueConverter Int2UInt { get; } = new ValueConverter(ShaderFieldType.Int, ShaderFieldType.Uint, "uint({0})");
        public static ValueConverter Int2Vec2 { get; } = new ValueConverter(ShaderFieldType.Int, ShaderFieldType.Vec2, "vec2(float({0}))");
        public static ValueConverter Int2Vec3 { get; } = new ValueConverter(ShaderFieldType.Int, ShaderFieldType.Vec3, "vec3(float({0}))");
        public static ValueConverter Int2Vec4 { get; } = new ValueConverter(ShaderFieldType.Int, ShaderFieldType.Vec4, "vec4(float({0}))");

        public static ValueConverter UInt2Float { get; } = new ValueConverter(ShaderFieldType.Uint, ShaderFieldType.Float, "float({0})");
        public static ValueConverter UInt2Int { get; } = new ValueConverter(ShaderFieldType.Uint, ShaderFieldType.Int, "int({0})");
        public static ValueConverter UInt2UInt { get; } = new ValueConverter(ShaderFieldType.Uint, ShaderFieldType.Uint, "{0}");
        public static ValueConverter UInt2Vec2 { get; } = new ValueConverter(ShaderFieldType.Uint, ShaderFieldType.Vec2, "vec2(float({0}))");
        public static ValueConverter UInt2Vec3 { get; } = new ValueConverter(ShaderFieldType.Uint, ShaderFieldType.Vec3, "vec3(float({0}))");
        public static ValueConverter UInt2Vec4 { get; } = new ValueConverter(ShaderFieldType.Uint, ShaderFieldType.Vec4, "vec4(float({0}))");

        public static ValueConverter Vec22Float { get; } = new ValueConverter(ShaderFieldType.Vec2, ShaderFieldType.Float, "float({0}.x)");
        public static ValueConverter Vec22Int { get; } = new ValueConverter(ShaderFieldType.Vec2, ShaderFieldType.Int, "int({0}.x)");
        public static ValueConverter Vec22UInt { get; } = new ValueConverter(ShaderFieldType.Vec2, ShaderFieldType.Uint, "uint({0}.x)");
        public static ValueConverter Vec22Vec2 { get; } = new ValueConverter(ShaderFieldType.Vec2, ShaderFieldType.Vec2, "{0}");
        public static ValueConverter Vec22Vec3 { get; } = new ValueConverter(ShaderFieldType.Vec2, ShaderFieldType.Vec3, "vec3({0}), 0.0f)");
        public static ValueConverter Vec22Vec4 { get; } = new ValueConverter(ShaderFieldType.Vec2, ShaderFieldType.Vec4, "vec4({0}), 0.0f, 0.0f)");

        public static ValueConverter Vec32Float { get; } = new ValueConverter(ShaderFieldType.Vec3, ShaderFieldType.Float, "float({0}.x)");
        public static ValueConverter Vec32Int { get; } = new ValueConverter(ShaderFieldType.Vec3, ShaderFieldType.Int, "int({0}.x)");
        public static ValueConverter Vec32UInt { get; } = new ValueConverter(ShaderFieldType.Vec3, ShaderFieldType.Uint, "uint({0}.x)");
        public static ValueConverter Vec32Vec2 { get; } = new ValueConverter(ShaderFieldType.Vec3, ShaderFieldType.Vec2, "{0}.xy");
        public static ValueConverter Vec32Vec3 { get; } = new ValueConverter(ShaderFieldType.Vec3, ShaderFieldType.Vec3, "{0}");
        public static ValueConverter Vec32Vec4 { get; } = new ValueConverter(ShaderFieldType.Vec3, ShaderFieldType.Vec4, "vec4({0}, 0.0f)");

        public static ValueConverter Vec42Float { get; } = new ValueConverter(ShaderFieldType.Vec4, ShaderFieldType.Float, "float({0}.x)");
        public static ValueConverter Vec42Int { get; } = new ValueConverter(ShaderFieldType.Vec4, ShaderFieldType.Int, "int({0}.x)");
        public static ValueConverter Vec42UInt { get; } = new ValueConverter(ShaderFieldType.Vec4, ShaderFieldType.Uint, "uint({0}.x)");
        public static ValueConverter Vec42Vec2 { get; } = new ValueConverter(ShaderFieldType.Vec4, ShaderFieldType.Vec2, "{0}.xy");
        public static ValueConverter Vec42Vec3 { get; } = new ValueConverter(ShaderFieldType.Vec4, ShaderFieldType.Vec3, "{0}.xyz");
        public static ValueConverter Vec42Vec4 { get; } = new ValueConverter(ShaderFieldType.Vec4, ShaderFieldType.Vec4, "{0}");

        public static Dictionary<int, ValueConverter> DynConverters { get; } = new Dictionary<int, ValueConverter>()
        {
            [(int)ShaderFieldType.Float | ((int)ShaderFieldType.Float << 3)] = Float2Float,
            [(int)ShaderFieldType.Float | ((int)ShaderFieldType.Int << 3)]   = Float2Int,
            [(int)ShaderFieldType.Float | ((int)ShaderFieldType.Uint << 3)]  = Float2UInt,
            [(int)ShaderFieldType.Float | ((int)ShaderFieldType.Vec2 << 3)]  = Float2Vec2,
            [(int)ShaderFieldType.Float | ((int)ShaderFieldType.Vec3 << 3)]  = Float2Vec3,
            [(int)ShaderFieldType.Float | ((int)ShaderFieldType.Vec4 << 3)]  = Float2Vec4,

            [(int)ShaderFieldType.Int | ((int)ShaderFieldType.Float << 3)] = Int2Float,
            [(int)ShaderFieldType.Int | ((int)ShaderFieldType.Int << 3)]   = Int2Int,
            [(int)ShaderFieldType.Int | ((int)ShaderFieldType.Uint << 3)]  = Int2UInt,
            [(int)ShaderFieldType.Int | ((int)ShaderFieldType.Vec2 << 3)]  = Int2Vec2,
            [(int)ShaderFieldType.Int | ((int)ShaderFieldType.Vec3 << 3)]  = Int2Vec3,
            [(int)ShaderFieldType.Int | ((int)ShaderFieldType.Vec4 << 3)]  = Int2Vec4,

            [(int)ShaderFieldType.Uint | ((int)ShaderFieldType.Float << 3)] = UInt2Float,
            [(int)ShaderFieldType.Uint | ((int)ShaderFieldType.Int << 3)]   = UInt2Int,
            [(int)ShaderFieldType.Uint | ((int)ShaderFieldType.Uint << 3)]  = UInt2UInt,
            [(int)ShaderFieldType.Uint | ((int)ShaderFieldType.Vec2 << 3)]  = UInt2Vec2,
            [(int)ShaderFieldType.Uint | ((int)ShaderFieldType.Vec3 << 3)]  = UInt2Vec3,
            [(int)ShaderFieldType.Uint | ((int)ShaderFieldType.Vec4 << 3)]  = UInt2Vec4,

            [(int)ShaderFieldType.Vec2 | ((int)ShaderFieldType.Float << 3)] = Vec22Float,
            [(int)ShaderFieldType.Vec2 | ((int)ShaderFieldType.Int << 3)]   = Vec22Int,
            [(int)ShaderFieldType.Vec2 | ((int)ShaderFieldType.Uint << 3)]  = Vec22UInt,
            [(int)ShaderFieldType.Vec2 | ((int)ShaderFieldType.Vec2 << 3)]  = Vec22Vec2,
            [(int)ShaderFieldType.Vec2 | ((int)ShaderFieldType.Vec3 << 3)]  = Vec22Vec3,
            [(int)ShaderFieldType.Vec2 | ((int)ShaderFieldType.Vec4 << 3)]  = Vec22Vec4,

            [(int)ShaderFieldType.Vec3 | ((int)ShaderFieldType.Float << 3)] = Vec32Float,
            [(int)ShaderFieldType.Vec3 | ((int)ShaderFieldType.Int << 3)]   = Vec32Int,
            [(int)ShaderFieldType.Vec3 | ((int)ShaderFieldType.Uint << 3)]  = Vec32UInt,
            [(int)ShaderFieldType.Vec3 | ((int)ShaderFieldType.Vec2 << 3)]  = Vec32Vec2,
            [(int)ShaderFieldType.Vec3 | ((int)ShaderFieldType.Vec3 << 3)]  = Vec32Vec3,
            [(int)ShaderFieldType.Vec3 | ((int)ShaderFieldType.Vec4 << 3)]  = Vec32Vec4,

            [(int)ShaderFieldType.Vec4 | ((int)ShaderFieldType.Float << 3)] = Vec42Float,
            [(int)ShaderFieldType.Vec4 | ((int)ShaderFieldType.Int << 3)]   = Vec42Int,
            [(int)ShaderFieldType.Vec4 | ((int)ShaderFieldType.Uint << 3)]  = Vec42UInt,
            [(int)ShaderFieldType.Vec4 | ((int)ShaderFieldType.Vec2 << 3)]  = Vec42Vec2,
            [(int)ShaderFieldType.Vec4 | ((int)ShaderFieldType.Vec3 << 3)]  = Vec42Vec3,
            [(int)ShaderFieldType.Vec4 | ((int)ShaderFieldType.Vec4 << 3)]  = Vec42Vec4,
        };

        public static ValueConverter GetFor(ShaderFieldType sIn, ShaderFieldType sOut) => DynConverters[(int)sIn | ((int)sOut << 3)];

        public ShaderFieldType From { get; }
        public ShaderFieldType To { get; }
        public string ConvStatement { get; }

        public ValueConverter(ShaderFieldType from, ShaderFieldType to, string convStatement)
        {
            this.From = from;
            this.To = to;
            this.ConvStatement = convStatement;
        }

        public string Convert(string val) => string.Format(this.ConvStatement, val);
    }
}
