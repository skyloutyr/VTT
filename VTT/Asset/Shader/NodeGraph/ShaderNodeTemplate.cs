namespace VTT.Asset.Shader.NodeGraph
{
    using System.Numerics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Network;
    using VTT.Util;

    public class ShaderNodeTemplate
    {
        public static Dictionary<Guid, ShaderNodeTemplate> TemplatesByID { get; } = new Dictionary<Guid, ShaderNodeTemplate>();

        public static ShaderNodeTemplate MaterialPBR { get; } = new ShaderNodeTemplate(Guid.Parse("bd57f653-c17d-4fa8-a0ba-3e6f3c2e5654"), ShaderTemplateCategory.None, "PBR Output", false,
            new NodeInput[] {
                new NodeInput() {
                    Name = "Albedo",
                    SelfType = NodeValueType.Vec3,
                    CurrentValue = Vector3.Zero
                },

                new NodeInput() {
                    Name = "Normal",
                    SelfType = NodeValueType.Vec3,
                    CurrentValue = Vector3.Zero
                },

                new NodeInput() {
                    Name = "Emission",
                    SelfType = NodeValueType.Vec3,
                    CurrentValue = Vector3.Zero
                },

                new NodeInput() {
                    Name = "Alpha",
                    SelfType = NodeValueType.Float,
                    CurrentValue = 0f
                },

                new NodeInput() {
                    Name = "Ambient Occlusion",
                    SelfType = NodeValueType.Float,
                    CurrentValue = 0f
                },

                new NodeInput()
                {
                    Name = "Metallic",
                    SelfType = NodeValueType.Float,
                    CurrentValue = 0f
                },

                new NodeInput()
                {
                    Name = "Roughness",
                    SelfType = NodeValueType.Float,
                    CurrentValue = 0f
                }
            },

            Array.Empty<NodeOutput>(),
@"albedo = $INPUT@0$;
normal = $INPUT@1$;
emissive = $INPUT@2$;
a = $INPUT@3$;
ao = $INPUT@4$;
m = $INPUT@5$;
r = $INPUT@6$;
",
(ctx, matrix, outIndex) => ctx.CreateMatrix(matrix[0])
        );

        #region Math - Vec3
        public static ShaderNodeTemplate Vec3Multiply { get; } = new ShaderNodeTemplate(Guid.Parse("76f5768c-56ab-4b2c-a111-6ca2b1ca8cd1"), ShaderTemplateCategory.MathVec3, "Vec3 * Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ * $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector3)x[0] * (Vector3)x[1])
        );

        public static ShaderNodeTemplate Vec3Add { get; } = new ShaderNodeTemplate(Guid.Parse("0a9e0b70-e1cc-492d-ae69-a141e1110175"), ShaderTemplateCategory.MathVec3, "Vec3 + Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ + $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector3)x[0] + (Vector3)x[1])
        );

        public static ShaderNodeTemplate Vec3Sub { get; } = new ShaderNodeTemplate(Guid.Parse("f2066610-60ba-447e-9ae0-08c68420334b"), ShaderTemplateCategory.MathVec3, "Vec3 - Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ - $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector3)x[0] - (Vector3)x[1])
        );

        public static ShaderNodeTemplate Vec3Div { get; } = new ShaderNodeTemplate(Guid.Parse("88e8842c-8b0f-4fbf-9c75-3ad38b066313"), ShaderTemplateCategory.MathVec3, "Vec3 / Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ / $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector3)x[0] / (Vector3)x[1])
);

        public static ShaderNodeTemplate Vec3Dot { get; } = new ShaderNodeTemplate(Guid.Parse("517946ec-5a52-4438-8039-ffb962fb184e"), ShaderTemplateCategory.MathVec3, "Vec3 ⋅ Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = dot($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Vector3.Dot((Vector3)x[0], (Vector3)x[1]))
);

        public static ShaderNodeTemplate Vec3Cross { get; } = new ShaderNodeTemplate(Guid.Parse("b6b2e438-5e9a-423f-a8b5-fa15a60c5565"), ShaderTemplateCategory.MathVec3, "Vec3 ⨯ Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = cross($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Vector3.Cross((Vector3)x[0], (Vector3)x[1]))
);

        public static ShaderNodeTemplate Vec3Reflect { get; } = new ShaderNodeTemplate(Guid.Parse("e8c81f43-daf4-482f-adac-007e92efcb9d"), ShaderTemplateCategory.MathVec3, "Reflect Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Normal",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = reflect($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Vector3.Reflect((Vector3)x[0], (Vector3)x[1]))
);

        public static ShaderNodeTemplate Vec3Invert { get; } = new ShaderNodeTemplate(Guid.Parse("7428236d-634e-46d4-82c3-8a2e852712f9"), ShaderTemplateCategory.MathVec3, "Invert Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = -$INPUT@0$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => -(Vector3)x[0])
);

        public static ShaderNodeTemplate Vec3Normalize { get; } = new ShaderNodeTemplate(Guid.Parse("f5e7894b-c804-42a9-9f3a-e531c6d74fbf"), ShaderTemplateCategory.MathVec3, "Normalize Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = normalize($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Normalized())
);

        public static ShaderNodeTemplate Vec3Length { get; } = new ShaderNodeTemplate(Guid.Parse("be522011-7b75-4b48-8689-b6d92283983c"), ShaderTemplateCategory.MathVec3, "Length of Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = length($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Length())
);

        public static ShaderNodeTemplate Vec3Abs { get; } = new ShaderNodeTemplate(Guid.Parse("bad0e1c7-a45e-4abc-b76e-41d221c7374a"), ShaderTemplateCategory.MathVec3, "Absolute of Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = abs($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Abs())
);

        public static ShaderNodeTemplate Vec3Ceil { get; } = new ShaderNodeTemplate(Guid.Parse("d8482ac4-41b7-4594-9427-156bf296a37d"), ShaderTemplateCategory.MathVec3, "Ceiling of Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = ceil($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Ceil())
);

        public static ShaderNodeTemplate Vec3Floor { get; } = new ShaderNodeTemplate(Guid.Parse("caa7dd8e-8c15-4b74-ba28-1dd7750e888a"), ShaderTemplateCategory.MathVec3, "Floor of Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = floor($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Floor())
);

        public static ShaderNodeTemplate Vec3Round { get; } = new ShaderNodeTemplate(Guid.Parse("09c24bb3-a3cc-420a-a36e-151e1573de96"), ShaderTemplateCategory.MathVec3, "Rounded Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = round($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Round()));

        public static ShaderNodeTemplate Vec3Clamp { get; } = new ShaderNodeTemplate(Guid.Parse("5affae06-677b-4671-ab36-103ff94ce2d9"), ShaderTemplateCategory.MathVec3, "Clamp Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Min",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

             new NodeInput() {
                Name = "Max",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
       },

@"$OUTPUT@0$ = clamp($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Clamp((Vector3)x[1], (Vector3)x[2]))
);

        public static ShaderNodeTemplate Vec3Min { get; } = new ShaderNodeTemplate(Guid.Parse("3bb3c6d0-eada-43e5-a271-a132f30ae3bb"), ShaderTemplateCategory.MathVec3, "Min Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = min($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Min((Vector3)x[1]))
);
        public static ShaderNodeTemplate Vec3Max { get; } = new ShaderNodeTemplate(Guid.Parse("1f7b8f45-148f-47a8-9ea8-7204c5190a22"), ShaderTemplateCategory.MathVec3, "Max Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = max($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Max((Vector3)x[1]))
);

        public static ShaderNodeTemplate Vec3Mix { get; } = new ShaderNodeTemplate(Guid.Parse("e7d7a3c5-4b89-4f13-a301-bd586db25d49"), ShaderTemplateCategory.MathVec3, "Mix Vec3 and Vec3", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Factor",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
       },

@"$OUTPUT@0$ = mix($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Vector3.Lerp((Vector3)x[0], (Vector3)x[1], (float)x[2]))
);

        public static ShaderNodeTemplate Vec3Mod { get; } = new ShaderNodeTemplate(Guid.Parse("d0675c67-d171-4990-8275-b14e369a4517"), ShaderTemplateCategory.MathVec3, "Modulo", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec3,
                CurrentValue = Vector3.Zero
            },

            new NodeInput() {
                Name = "Modulo",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec3
            }
       },

@"$OUTPUT@0$ = mod($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector3)x[0]).Mod((float)x[1]))
);

        public static ShaderNodeTemplate Vec3Pow { get; } = new ShaderNodeTemplate(Guid.Parse("5304451b-9d1e-4524-aa87-48eb8256a187"), ShaderTemplateCategory.MathVec3, "Vec3 ^ Vec3", true, new NodeInput[] {
                new NodeInput()
                {
                    Name = "Value",
                    SelfType = NodeValueType.Vec3,
                    CurrentValue = Vector3.Zero
                },

                new NodeInput()
                {
                    Name = "Operand",
                    SelfType = NodeValueType.Vec3,
                    CurrentValue = Vector3.Zero
                }
            },

            new NodeOutput[] {
                new NodeOutput() {
                    Name = "Result",
                    SelfType = NodeValueType.Vec3
                }
            },
@"$OUTPUT@0$ = pow($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => new Vector3(MathF.Pow(((Vector3)x[0]).X, ((Vector3)x[1]).X), MathF.Pow(((Vector3)x[0]).Y, ((Vector3)x[1]).Y), MathF.Pow(((Vector3)x[0]).Z, ((Vector3)x[1]).Z)))
);

        public static ShaderNodeTemplate Vec3GammaCorrect { get; } = new ShaderNodeTemplate(Guid.Parse("2711137b-689a-45fa-85e0-211fc6964d9d"), ShaderTemplateCategory.MathVec3, "Apply Gamma", true, new NodeInput[] {
                new NodeInput()
                {
                    Name = "Color",
                    SelfType = NodeValueType.Vec3,
                    CurrentValue = Vector3.Zero
                }
            },

            new NodeOutput[] {
                new NodeOutput() {
                    Name = "Color",
                    SelfType = NodeValueType.Vec3
                }
            },
@"$OUTPUT@0$ = pow($INPUT@0$, vec3(gamma_factor));",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => new Vector3(MathF.Pow(((Vector3)x[0]).X, 2.2f), MathF.Pow(((Vector3)x[0]).Y, 2.2f), MathF.Pow(((Vector3)x[0]).Z, 2.2f)))
);

        public static ShaderNodeTemplate Vec3Rgb2Hsv { get; } = new ShaderNodeTemplate(Guid.Parse("7ae9b861-7a4b-4144-823e-1b5e142f055d"), ShaderTemplateCategory.MathVec3, "RGB to HSV", true, new NodeInput[] {
                new NodeInput()
                {
                    Name = "RGB",
                    SelfType = NodeValueType.Vec3,
                    CurrentValue = Vector3.Zero
                }
            },

            new NodeOutput[] {
                new NodeOutput() {
                    Name = "HSV",
                    SelfType = NodeValueType.Vec3
                }
            },
@"$OUTPUT@0$ = rgb2hsv($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => {
    Color clr = new Color(new Rgba32((Vector3)x[0]));
    HSVColor hsv = (HSVColor)clr;
    return new Vector3(hsv.Hue / 360.0f, hsv.Saturation, hsv.Value);
}));

        public static ShaderNodeTemplate Vec3Hsv2Rgb { get; } = new ShaderNodeTemplate(Guid.Parse("4dd8f6cd-52f0-4f80-93c7-edb08657f434"), ShaderTemplateCategory.MathVec3, "HSV to RGB", true, new NodeInput[] {
                new NodeInput()
                {
                    Name = "HSV",
                    SelfType = NodeValueType.Vec3,
                    CurrentValue = Vector3.Zero
                }
            },

            new NodeOutput[] {
                new NodeOutput() {
                    Name = "RGB",
                    SelfType = NodeValueType.Vec3
                }
            },
@"$OUTPUT@0$ = hsv2rgb($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => {
    Vector3 v = (Vector3)x[0];
    HSVColor hsv = new HSVColor(v.X * 360.0f, v.Y, v.Z);
    Color clr = (Color)hsv;
    return new Vector3(clr.Red(), clr.Green(), clr.Blue());
}));

        #endregion

        #region Math - Vec2
        public static ShaderNodeTemplate Vec2Multiply { get; } = new ShaderNodeTemplate(Guid.Parse("68c41682-8d34-47c7-ab24-9c7d77e61111"), ShaderTemplateCategory.MathVec2, "Vec2 * Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ * $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector2)x[0] * (Vector2)x[1])
        );

        public static ShaderNodeTemplate Vec2Add { get; } = new ShaderNodeTemplate(Guid.Parse("4453c829-bead-4bb4-8574-79742c3af494"), ShaderTemplateCategory.MathVec2, "Vec2 + Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ + $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector2)x[0] + (Vector2)x[1])
        );

        public static ShaderNodeTemplate Vec2Sub { get; } = new ShaderNodeTemplate(Guid.Parse("f108e10f-7257-4b1f-8597-a7fe264bbf91"), ShaderTemplateCategory.MathVec2, "Vec2 - Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ - $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector2)x[0] - (Vector2)x[1])
        );

        public static ShaderNodeTemplate Vec2Div { get; } = new ShaderNodeTemplate(Guid.Parse("c8b41cac-3ab9-4ce3-abc0-a4e99ccffe2c"), ShaderTemplateCategory.MathVec2, "Vec2 / Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ / $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector2)x[0] / (Vector2)x[1])
);

        public static ShaderNodeTemplate Vec2Dot { get; } = new ShaderNodeTemplate(Guid.Parse("f0f9d5c0-1529-45d2-8a24-2c6a0d53f180"), ShaderTemplateCategory.MathVec2, "Vec2 ⋅ Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = dot($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Vector2.Dot((Vector2)x[0], (Vector2)x[1]))
);

        public static ShaderNodeTemplate Vec2Reflect { get; } = new ShaderNodeTemplate(Guid.Parse("85fc9cc4-9c66-4d05-8a81-2c47d221058d"), ShaderTemplateCategory.MathVec2, "Reflect Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Normal",
                SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = reflect($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Vector2.Reflect(((Vector2)x[0]), ((Vector2)x[1])))
);

        public static ShaderNodeTemplate Vec2Invert { get; } = new ShaderNodeTemplate(Guid.Parse("24ee2e34-ca37-4f2d-8062-3c36d0af4aa0"), ShaderTemplateCategory.MathVec2, "Invert Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = -$INPUT@0$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => -(Vector2)x[0])
);

        public static ShaderNodeTemplate Vec2Normalize { get; } = new ShaderNodeTemplate(Guid.Parse("4aadfe83-7027-44e0-b2c2-9ce301f8be10"), ShaderTemplateCategory.MathVec2, "Normalize Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = normalize($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Normalized())
);

        public static ShaderNodeTemplate Vec2Length { get; } = new ShaderNodeTemplate(Guid.Parse("1e9a2a09-c3e5-4da6-b218-ee92207670cc"), ShaderTemplateCategory.MathVec2, "Length of Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = length($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Length())
);

        public static ShaderNodeTemplate Vec2Abs { get; } = new ShaderNodeTemplate(Guid.Parse("9c81e22c-cb6b-43dd-865e-fd75759a8006"), ShaderTemplateCategory.MathVec2, "Absolute of Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = abs($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Abs())
);

        public static ShaderNodeTemplate Vec2Ceil { get; } = new ShaderNodeTemplate(Guid.Parse("a6092b21-d2d0-40a4-b099-4906f69d1b5a"), ShaderTemplateCategory.MathVec2, "Ceiling of Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = ceil($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Ceil())
);

        public static ShaderNodeTemplate Vec2Floor { get; } = new ShaderNodeTemplate(Guid.Parse("718ef954-441e-4518-854c-4625ce946a2a"), ShaderTemplateCategory.MathVec2, "Floor of Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = floor($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Floor())
);

        public static ShaderNodeTemplate Vec2Round { get; } = new ShaderNodeTemplate(Guid.Parse("2a16f33c-58ef-4dfa-8808-d1c24070d8b5"), ShaderTemplateCategory.MathVec2, "Rounded Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = round($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Round())
);

        public static ShaderNodeTemplate Vec2Clamp { get; } = new ShaderNodeTemplate(Guid.Parse("9c029bed-0c28-4b06-b4ea-0cca854bf048"), ShaderTemplateCategory.MathVec2, "Clamp Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Min",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

             new NodeInput() {
                Name = "Max",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
       },

@"$OUTPUT@0$ = clamp($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Clamp((Vector2)x[1], (Vector2)x[2]))
);

        public static ShaderNodeTemplate Vec2Min { get; } = new ShaderNodeTemplate(Guid.Parse("4e03cfd3-49f9-4afe-88be-de1c854d4d81"), ShaderTemplateCategory.MathVec2, "Min Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = min($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Min((Vector2)x[1]))
);

        public static ShaderNodeTemplate Vec2Max { get; } = new ShaderNodeTemplate(Guid.Parse("59adcba1-5e46-4dba-b38d-d37a6181ffc1"), ShaderTemplateCategory.MathVec2, "Max Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
        },

@"$OUTPUT@0$ = max($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Max((Vector2)x[1]))
);

        public static ShaderNodeTemplate Vec2Mix { get; } = new ShaderNodeTemplate(Guid.Parse("0952e733-dd8f-40c3-80af-7ab64ef5e202"), ShaderTemplateCategory.MathVec2, "Mix Vec2 and Vec2", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Factor",
                SelfType = NodeValueType.Float,
                CurrentValue = 0
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
       },

@"$OUTPUT@0$ = mix($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Vector2.Lerp((Vector2)x[0], (Vector2)x[1], (float)x[2]))
);

        public static ShaderNodeTemplate Vec2Mod { get; } = new ShaderNodeTemplate(Guid.Parse("de3c0d0b-1ff6-47cf-bae1-e0f114cf5ce3"), ShaderTemplateCategory.MathVec2, "Modulo", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec2,
                CurrentValue = Vector2.Zero
            },

            new NodeInput() {
                Name = "Modulo",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec2
            }
       },

@"$OUTPUT@0$ = mod($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector2)x[0]).Mod((float)x[1]))
);

        public static ShaderNodeTemplate Vec2Pow { get; } = new ShaderNodeTemplate(Guid.Parse("8b3972bd-c5d5-42f0-a2b4-a1d3b8205060"), ShaderTemplateCategory.MathVec2, "Vec2 ^ Vec2", true, new NodeInput[] {
                new NodeInput()
                {
                    Name = "Value",
                    SelfType = NodeValueType.Vec2,
                    CurrentValue = Vector2.Zero
                },

                new NodeInput()
                {
                    Name = "Operand",
                    SelfType = NodeValueType.Vec2,
                    CurrentValue = Vector2.Zero
                }
            },

            new NodeOutput[] {
                new NodeOutput() {
                    Name = "Result",
                    SelfType = NodeValueType.Vec2
                }
            },
@"$OUTPUT@0$ = pow($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => new Vector2(MathF.Pow(((Vector2)x[0]).X, ((Vector2)x[1]).X), MathF.Pow(((Vector2)x[0]).Y, ((Vector2)x[1]).Y)))
);

        #endregion

        #region Math - Vec4
        public static ShaderNodeTemplate Vec4Multiply { get; } = new ShaderNodeTemplate(Guid.Parse("cf1f6bf8-8420-4543-af0e-22c8ffd3b2db"), ShaderTemplateCategory.MathVec4, "Vec4 * Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ * $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector4)x[0] * (Vector4)x[1])
        );

        public static ShaderNodeTemplate Vec4Add { get; } = new ShaderNodeTemplate(Guid.Parse("84ee7d1f-c2d2-4915-ac9e-1ebe2c2e106d"), ShaderTemplateCategory.MathVec4, "Vec4 + Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ + $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector4)x[0] + (Vector4)x[1])
        );

        public static ShaderNodeTemplate Vec4Sub { get; } = new ShaderNodeTemplate(Guid.Parse("cdb567a5-b1ff-4f20-b558-35d4197500fa"), ShaderTemplateCategory.MathVec4, "Vec4 - Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ - $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector4)x[0] - (Vector4)x[1])
        );

        public static ShaderNodeTemplate Vec4Div { get; } = new ShaderNodeTemplate(Guid.Parse("59cff607-20e3-4e93-b7bd-972901b7a439"), ShaderTemplateCategory.MathVec4, "Vec4 / Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ / $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (Vector4)x[0] / (Vector4)x[1])
);

        public static ShaderNodeTemplate Vec4Dot { get; } = new ShaderNodeTemplate(Guid.Parse("12b7206f-12f2-4656-a7c3-8663e5ab1ea1"), ShaderTemplateCategory.MathVec4, "Vec4 ⋅ Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Vector 2",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = dot($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Vector4.Dot((Vector4)x[0], (Vector4)x[1]))
);

        public static ShaderNodeTemplate Vec4Reflect { get; } = new ShaderNodeTemplate(Guid.Parse("2f2dde94-1310-4c39-8ee4-4ea9f23011f2"), ShaderTemplateCategory.MathVec4, "Reflect Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector 1",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Normal",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = reflect($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => {
    Vector4 incident = (Vector4)x[0];
    Vector4 normal = (Vector4)x[1];
    return incident - (normal * 2f * Vector4.Dot(normal, incident));
}));

        public static ShaderNodeTemplate Vec4Invert { get; } = new ShaderNodeTemplate(Guid.Parse("fde5337e-2c3a-4018-b7bd-265a9a3a918a"), ShaderTemplateCategory.MathVec4, "Invert Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = -$INPUT@0$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => -(Vector4)x[0])
);

        public static ShaderNodeTemplate Vec4Normalize { get; } = new ShaderNodeTemplate(Guid.Parse("3b9e3152-c391-45d0-ad24-d8b1f4c6a39f"), ShaderTemplateCategory.MathVec4, "Normalize Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec4, CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = normalize($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Normalized())
);

        public static ShaderNodeTemplate Vec4Length { get; } = new ShaderNodeTemplate(Guid.Parse("1e961985-74f0-47a2-85f8-1cae9cfd1f45"), ShaderTemplateCategory.MathVec4, "Length of Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = length($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Length())
);

        public static ShaderNodeTemplate Vec4Abs { get; } = new ShaderNodeTemplate(Guid.Parse("2045bb08-3b82-4aec-a009-5f7f02088042"), ShaderTemplateCategory.MathVec4, "Absolute of Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = abs($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Abs())
);

        public static ShaderNodeTemplate Vec4Ceil { get; } = new ShaderNodeTemplate(Guid.Parse("9268f1ab-b60d-4cbe-97a0-1037f0d277d6"), ShaderTemplateCategory.MathVec4, "Ceiling of Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = ceil($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Ceil())
);

        public static ShaderNodeTemplate Vec4Floor { get; } = new ShaderNodeTemplate(Guid.Parse("34315b9c-d128-4fce-9d6b-79881dedfefa"), ShaderTemplateCategory.MathVec4, "Floor of Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = floor($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Floor())
);

        public static ShaderNodeTemplate Vec4Round { get; } = new ShaderNodeTemplate(Guid.Parse("1f2d5b49-a900-461e-9366-69d2645af2f7"), ShaderTemplateCategory.MathVec4, "Rounded Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Vector",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = round($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Round())
);

        public static ShaderNodeTemplate Vec4Clamp { get; } = new ShaderNodeTemplate(Guid.Parse("200eb291-1dd5-478a-9cd3-c7753a29a3a4"), ShaderTemplateCategory.MathVec4, "Clamp Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Min",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            },

             new NodeInput() {
                Name = "Max",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
       },

@"$OUTPUT@0$ = clamp($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Clamp((Vector4)x[1], (Vector4)x[2]))
);

        public static ShaderNodeTemplate Vec4Min { get; } = new ShaderNodeTemplate(Guid.Parse("1e835e66-cab7-43af-951b-85ffab5d1384"), ShaderTemplateCategory.MathVec4, "Min Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = min($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Min((Vector4)x[1]))
);

        public static ShaderNodeTemplate Vec4Max { get; } = new ShaderNodeTemplate(Guid.Parse("36cb1ba1-ff86-41b7-be57-141624442843"), ShaderTemplateCategory.MathVec4, "Max Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
        },

@"$OUTPUT@0$ = max($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Max((Vector4)x[1]))
);

        public static ShaderNodeTemplate Vec4Mix { get; } = new ShaderNodeTemplate(Guid.Parse("8d164c56-6a3c-4b1c-9a6d-ed4c7c8eb389"), ShaderTemplateCategory.MathVec4, "Mix Vec4 and Vec4", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Factor",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
       },

@"$OUTPUT@0$ = mix($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Vector4.Lerp((Vector4)x[0], (Vector4)x[1], (float)x[2]))
);

        public static ShaderNodeTemplate Vec4Mod { get; } = new ShaderNodeTemplate(Guid.Parse("2db85763-ca37-4a3b-9db9-bfe2480919c7"), ShaderTemplateCategory.MathVec4, "Modulo", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Vec4,
                CurrentValue = Vector4.Zero
            },

            new NodeInput() {
                Name = "Modulo",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Vec4
            }
       },

@"$OUTPUT@0$ = mod($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((Vector4)x[0]).Mod((float)x[1]))
);

        public static ShaderNodeTemplate Vec4Pow { get; } = new ShaderNodeTemplate(Guid.Parse("0aced8c6-b77e-4e94-abfb-cea8541715ae"), ShaderTemplateCategory.MathVec4, "Vec4 ^ Vec4", true, new NodeInput[] {
                new NodeInput()
                {
                    Name = "Value",
                    SelfType = NodeValueType.Vec4,
                    CurrentValue = Vector4.Zero
                },

                new NodeInput()
                {
                    Name = "Operand",
                    SelfType = NodeValueType.Vec4,
                    CurrentValue = Vector4.Zero
                }
            },

            new NodeOutput[] {
                new NodeOutput() {
                    Name = "Result",
                    SelfType = NodeValueType.Vec4
                }
            },
@"$OUTPUT@0$ = pow($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => new Vector4(MathF.Pow(((Vector4)x[0]).X, ((Vector4)x[1]).X), MathF.Pow(((Vector4)x[0]).Y, ((Vector4)x[1]).Y), MathF.Pow(((Vector4)x[0]).Z, ((Vector4)x[1]).Z), MathF.Pow(((Vector4)x[0]).W, ((Vector4)x[1]).W)))
);

        #endregion

        #region Math - Float
        public static ShaderNodeTemplate FloatMultiply { get; } = new ShaderNodeTemplate(Guid.Parse("d77e8e21-aa30-4e3c-9063-9ddbc7d01796"), ShaderTemplateCategory.MathFloat, "Float * Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Float 1",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Float 2",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ * $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] * (float)x[1])
);

        public static ShaderNodeTemplate FloatAdd { get; } = new ShaderNodeTemplate(Guid.Parse("b6256d4b-7ed0-4062-a786-02102ebbde4b"), ShaderTemplateCategory.MathFloat, "Float + Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Float 1",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Float 2",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ + $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] + (float)x[1])
);

        public static ShaderNodeTemplate FloatSub { get; } = new ShaderNodeTemplate(Guid.Parse("6647310c-8493-409f-b834-8cd29443a980"), ShaderTemplateCategory.MathFloat, "Float - Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Float 1",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Float 2",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ - $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] - (float)x[1])
);

        public static ShaderNodeTemplate FloatDiv { get; } = new ShaderNodeTemplate(Guid.Parse("82524880-584d-47bf-9893-5887f16df3e4"), ShaderTemplateCategory.MathFloat, "Float / Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Float 1",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Float 2",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ / $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] / (float)x[1])
);

        public static ShaderNodeTemplate FloatAbs { get; } = new ShaderNodeTemplate(Guid.Parse("c6d36c01-00f9-40c1-b684-9f2e935cdaa2"), ShaderTemplateCategory.MathFloat, "Absolute of Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = abs($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Abs((float)x[0]))
);

        public static ShaderNodeTemplate FloatSin { get; } = new ShaderNodeTemplate(Guid.Parse("3133907d-2081-4d2e-8032-06e387265914"), ShaderTemplateCategory.MathFloat, "Sine of Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = sin($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Sin((float)x[0]))
);
        public static ShaderNodeTemplate FloatCos { get; } = new ShaderNodeTemplate(Guid.Parse("c4d10959-3d31-483b-9309-b6832a4291be"), ShaderTemplateCategory.MathFloat, "Cosine of Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = cos($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Cos((float)x[0]))
);
        public static ShaderNodeTemplate FloatASin { get; } = new ShaderNodeTemplate(Guid.Parse("58e09542-9b59-483d-a5a1-bf56b2b31d23"), ShaderTemplateCategory.MathFloat, "Arcsine of Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = asin($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Asin((float)x[0]))
);
        public static ShaderNodeTemplate FloatACos { get; } = new ShaderNodeTemplate(Guid.Parse("708e460f-1c1d-431e-a2c4-aa7956aa9aea"), ShaderTemplateCategory.MathFloat, "Arccosine of Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = acos($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Acos((float)x[0]))
);

        public static ShaderNodeTemplate FloatTan { get; } = new ShaderNodeTemplate(Guid.Parse("9ca905ef-6607-497d-b090-1d926a840edb"), ShaderTemplateCategory.MathFloat, "Tangent of Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = tan($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Tan((float)x[0]))
);
        public static ShaderNodeTemplate FloatATan { get; } = new ShaderNodeTemplate(Guid.Parse("af57fd11-cfbc-4923-a9ff-98033c604dec"), ShaderTemplateCategory.MathFloat, "Arctangent of Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Y over X",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = atan($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Atan((float)x[0]))
);

        public static ShaderNodeTemplate FloatATan2 { get; } = new ShaderNodeTemplate(Guid.Parse("7dcc1386-f6ff-46b9-a2bd-2d7ab4e60639"), ShaderTemplateCategory.MathFloat, "Arctangent of Float - 2 in", true, new NodeInput[] {
            new NodeInput() {
                Name = "Y",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "X",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = atan($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Atan2((float)x[0], (float)x[1]))
);

        public static ShaderNodeTemplate FloatCeil { get; } = new ShaderNodeTemplate(Guid.Parse("189bdce2-99c0-4ef8-8882-b389eeef8810"), ShaderTemplateCategory.MathFloat, "Ceiling of Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = ceil($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Ceiling((float)x[0]))
);

        public static ShaderNodeTemplate FloatFloor { get; } = new ShaderNodeTemplate(Guid.Parse("93c7e920-117f-47d7-9e23-c3502dad11a9"), ShaderTemplateCategory.MathFloat, "Floor of Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = floor($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Floor((float)x[0]))
);

        public static ShaderNodeTemplate FloatRound { get; } = new ShaderNodeTemplate(Guid.Parse("732d34cf-169f-450a-b523-29370a401d03"), ShaderTemplateCategory.MathFloat, "Rounded Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = round($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Round((float)x[0]))
);

        public static ShaderNodeTemplate FloatClamp { get; } = new ShaderNodeTemplate(Guid.Parse("4f0a02a2-86b9-4bbc-81bb-5eba1cbbcdb5"), ShaderTemplateCategory.MathFloat, "Clamp Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Min",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

             new NodeInput() {
                Name = "Max",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
       },

@"$OUTPUT@0$ = clamp($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Math.Clamp((float)x[0], (float)x[1], (float)x[2]))
);

        public static ShaderNodeTemplate FloatMin { get; } = new ShaderNodeTemplate(Guid.Parse("282feab0-abe5-4708-a309-1c0d8b6b5fcf"), ShaderTemplateCategory.MathFloat, "Min Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = min($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Min((float)x[0], (float)x[1]))
);

        public static ShaderNodeTemplate FloatMax { get; } = new ShaderNodeTemplate(Guid.Parse("3f4b7056-b282-49e8-a6fd-56cda492f278"), ShaderTemplateCategory.MathFloat, "Max Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = max($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Max((float)x[0], (float)x[1]))
);

        public static ShaderNodeTemplate FloatDeg { get; } = new ShaderNodeTemplate(Guid.Parse("72847689-d69b-4275-9c7b-2713dcfc671f"), ShaderTemplateCategory.MathFloat, "Convert to Degrees", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = degrees($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] * 180 / MathF.PI)
);

        public static ShaderNodeTemplate FloatRad { get; } = new ShaderNodeTemplate(Guid.Parse("09853fe7-c906-46a5-b791-d628064cb3b9"), ShaderTemplateCategory.MathFloat, "Convert to Radians", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = radians($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] * MathF.PI / 180)
);

        public static ShaderNodeTemplate FloatdFdx { get; } = new ShaderNodeTemplate(Guid.Parse("7119db08-d630-467a-a9b4-22e751580d38"), ShaderTemplateCategory.MathFloat, "Derivative (X)", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
       },

@"$OUTPUT@0$ = dFdx($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0]) // no derivative calculation possible?
);

        public static ShaderNodeTemplate FloatdFdy { get; } = new ShaderNodeTemplate(Guid.Parse("9a5a9051-4d73-4553-bf8c-b61c19529693"), ShaderTemplateCategory.MathFloat, "Derivative (Y)", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = dFdy($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0]) // no derivative calculation possible?
);

        public static ShaderNodeTemplate FloatExp { get; } = new ShaderNodeTemplate(Guid.Parse("170b02d7-b5bb-4555-8d68-01798d200fcd"), ShaderTemplateCategory.MathFloat, "Natural Exponent", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = exp($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Exp((float)x[0]))
);

        public static ShaderNodeTemplate FloatExp2 { get; } = new ShaderNodeTemplate(Guid.Parse("efcdc9bd-6013-446e-a6a9-3e58439312f7"), ShaderTemplateCategory.MathFloat, "Power of 2 Exponent", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = exp2($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Pow(2.0f, (float)x[0]))
);

        public static ShaderNodeTemplate FloatFract { get; } = new ShaderNodeTemplate(Guid.Parse("8fbd0890-b549-4d0a-95af-deb5f44113a8"), ShaderTemplateCategory.MathFloat, "Fraction", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = fract($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)((decimal)(float)x[0] % 1.0m))
);

        public static ShaderNodeTemplate FloatLog { get; } = new ShaderNodeTemplate(Guid.Parse("05182782-ff1f-4d67-ba25-bc862f467437"), ShaderTemplateCategory.MathFloat, "Natural Logarithm", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = log($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Log((float)x[0]))
);

        public static ShaderNodeTemplate FloatLog2 { get; } = new ShaderNodeTemplate(Guid.Parse("16c3eddc-176c-4a0d-ad2b-356fb76b0718"), ShaderTemplateCategory.MathFloat, "Power of 2 Logarithm", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = log2($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Log2((float)x[0]))
);

        public static ShaderNodeTemplate FloatMix { get; } = new ShaderNodeTemplate(Guid.Parse("5b17ad7e-ed30-4881-b9f8-9068fbf8af43"), ShaderTemplateCategory.MathFloat, "Mix Float and Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Factor",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
       },

@"$OUTPUT@0$ = mix($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((float)x[0] * (1.0f - (float)x[2])) + ((float)x[1] * (float)x[2]))
);

        public static ShaderNodeTemplate FloatMod { get; } = new ShaderNodeTemplate(Guid.Parse("40adde8b-84da-4f8d-9aaf-81af2004c464"), ShaderTemplateCategory.MathFloat, "Modulo", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Modulo",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
       },

@"$OUTPUT@0$ = mod($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] % (float)x[1])
);

        public static ShaderNodeTemplate FloatPow { get; } = new ShaderNodeTemplate(Guid.Parse("91b37e64-2774-45c0-99f6-19a22d5ede9a"), ShaderTemplateCategory.MathFloat, "Float ^ Float", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            },

            new NodeInput() {
                Name = "Exponent",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
       },

@"$OUTPUT@0$ = pow($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Pow((float)x[0], (float)x[1]))
);

        public static ShaderNodeTemplate FloatSqrt { get; } = new ShaderNodeTemplate(Guid.Parse("3725cb7e-7d5f-47d4-8b55-4b3fac9265ad"), ShaderTemplateCategory.MathFloat, "Square Root", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Float,
                CurrentValue = 0f
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Float
            }
        },

@"$OUTPUT@0$ = sqrt($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Sqrt((float)x[0]))
);

        #endregion

        #region Math - Int
        public static ShaderNodeTemplate IntAbs { get; } = new ShaderNodeTemplate(Guid.Parse("a2160c06-9975-4f32-a5b3-733293546c9a"), ShaderTemplateCategory.MathInt, "Absolute of Integer", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
        },

@"$OUTPUT@0$ = abs($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Math.Abs((int)x[0]))
);

        public static ShaderNodeTemplate IntClamp { get; } = new ShaderNodeTemplate(Guid.Parse("df4bdfc2-ad8d-4146-8fdc-901bb9066965"), ShaderTemplateCategory.MathInt, "Clamp Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Min",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

             new NodeInput() {
                Name = "Max",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
       },

@"$OUTPUT@0$ = clamp($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Math.Clamp((int)x[0], (int)x[1], (int)x[2]))
);

        public static ShaderNodeTemplate IntMin { get; } = new ShaderNodeTemplate(Guid.Parse("23743010-91b1-407d-bfa6-6181a1e1f998"), ShaderTemplateCategory.MathInt, "Min Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
        },

@"$OUTPUT@0$ = min($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Math.Min((int)x[0], (int)x[2]))
);

        public static ShaderNodeTemplate IntMax { get; } = new ShaderNodeTemplate(Guid.Parse("94a7a789-8811-4385-b0d2-40e412888668"), ShaderTemplateCategory.MathInt, "Max Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Value 2",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
        },

@"$OUTPUT@0$ = max($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => Math.Max((int)x[0], (int)x[2]))
);

        public static ShaderNodeTemplate IntMultiply { get; } = new ShaderNodeTemplate(Guid.Parse("35f9f8af-d60b-4aa0-aefa-e8364f188968"), ShaderTemplateCategory.MathInt, "Int * Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Int 1",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Int 2",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ * $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] * (int)x[1])
);

        public static ShaderNodeTemplate IntAdd { get; } = new ShaderNodeTemplate(Guid.Parse("02c429a8-0ef4-4f53-ba2f-e6c4a7bda831"), ShaderTemplateCategory.MathInt, "Int + Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Int 1",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Int 2",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ + $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] + (int)x[1])
);

        public static ShaderNodeTemplate IntSub { get; } = new ShaderNodeTemplate(Guid.Parse("a01fb561-5b3b-4030-beef-e00d270c0fdb"), ShaderTemplateCategory.MathInt, "Int - Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Int 1",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Int 2",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ - $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] - (int)x[1])
);

        public static ShaderNodeTemplate IntDiv { get; } = new ShaderNodeTemplate(Guid.Parse("079d8e61-7a70-4da2-be2a-0d46a30ee6fd"), ShaderTemplateCategory.MathInt, "Int / Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Int 1",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Int 2",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
        },

@"$OUTPUT@0$ = $INPUT@0$ / $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] / (int)x[1])
);

        public static ShaderNodeTemplate IntMod { get; } = new ShaderNodeTemplate(Guid.Parse("91b9cd21-602d-402b-8548-f3e502d919eb"), ShaderTemplateCategory.MathInt, "Modulo", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value 1",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Modulo",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
       },

@"$OUTPUT@0$ = $INPUT@0$ % $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] % (int)x[1])
);

        public static ShaderNodeTemplate IntPow { get; } = new ShaderNodeTemplate(Guid.Parse("51209d85-1375-4309-b821-025e788a13fb"), ShaderTemplateCategory.MathInt, "Int ^ Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Exponent",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
       },

@"$OUTPUT@0$ = pow($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)Math.Pow((int)x[0], (int)x[1]))
);

        public static ShaderNodeTemplate IntSqrt { get; } = new ShaderNodeTemplate(Guid.Parse("34c22f32-d37c-4e83-93c5-06c7789c6e3c"), ShaderTemplateCategory.MathInt, "Square Root", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

        new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
        },

@"$OUTPUT@0$ = sqrt($INPUT@0$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)Math.Sqrt((int)x[0]))
);

        public static ShaderNodeTemplate IntLSh { get; } = new ShaderNodeTemplate(Guid.Parse("6d954115-c4df-4c82-9f0f-843e6d2237b0"), ShaderTemplateCategory.MathInt, "Int << Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Operand",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
       },

@"$OUTPUT@0$ = $INPUT@0$ << $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((int)x[0] << (int)x[1]))
);

        public static ShaderNodeTemplate IntRSh { get; } = new ShaderNodeTemplate(Guid.Parse("61aa2ac5-6bce-4918-bf61-74300085b97a"), ShaderTemplateCategory.MathInt, "Int >> Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Operand",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
       },

@"$OUTPUT@0$ = $INPUT@0$ >> $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((int)x[0] >> (int)x[1]))
);

        public static ShaderNodeTemplate IntBitwiseAnd { get; } = new ShaderNodeTemplate(Guid.Parse("4ff676ae-e2b8-4125-a8a0-bdace7398e52"), ShaderTemplateCategory.MathInt, "Int & Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Operand",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
       },

@"$OUTPUT@0$ = $INPUT@0$ & $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((int)x[0] & (int)x[1]))
);

        public static ShaderNodeTemplate IntBitwiseOr { get; } = new ShaderNodeTemplate(Guid.Parse("26f5dc9a-e1a5-4b86-8b23-ca58db6c7c37"), ShaderTemplateCategory.MathInt, "Int | Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Operand",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
       },

@"$OUTPUT@0$ = $INPUT@0$ | $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((int)x[0] | (int)x[1]))
);

        public static ShaderNodeTemplate IntBitwiseXor { get; } = new ShaderNodeTemplate(Guid.Parse("e39e1cde-8c20-4060-9c80-006aa1150388"), ShaderTemplateCategory.MathInt, "XOR Int, Int", true, new NodeInput[] {
            new NodeInput() {
                Name = "Value",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            },

            new NodeInput() {
                Name = "Operand",
                SelfType = NodeValueType.Int,
                CurrentValue = 0
            }
        },

       new NodeOutput[] {
            new NodeOutput() {
                Name = "Result",
                SelfType = NodeValueType.Int
            }
       },

@"$OUTPUT@0$ = $INPUT@0$ ^ $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((int)x[0] ^ (int)x[1]))
);

        #endregion

        #region Material Uniforms

        public static ShaderNodeTemplate MaterialData { get; } = new ShaderNodeTemplate(Guid.Parse("fcb1fc5e-deaf-410d-b811-3ba90cef9da6"), ShaderTemplateCategory.Inputs, "Material Info", true, Array.Empty<NodeInput>(), new NodeOutput[] {
            new NodeOutput(){
                SelfType = NodeValueType.Vec3,
                Name = "Albedo Color"
            },

            new NodeOutput(){
                SelfType = NodeValueType.Float,
                Name = "Albedo Alpha"
            },

            new NodeOutput(){
                SelfType = NodeValueType.Vec3,
                Name = "Normal"
            },

            new NodeOutput(){
                SelfType = NodeValueType.Vec3,
                Name = "Emissive Color"
            },

            new NodeOutput(){
                SelfType = NodeValueType.Float,
                Name = "Ambient Occlusion"
            },

             new NodeOutput(){
                SelfType = NodeValueType.Float,
                Name = "Metallic"
            },

              new NodeOutput(){
                SelfType = NodeValueType.Float,
                Name = "Roughness"
            },

            new NodeOutput(){
                SelfType = NodeValueType.Vec3,
                Name = "Material Color"
            },

            new NodeOutput(){
                SelfType = NodeValueType.Float,
                Name = "Material Alpha"
            },

            new NodeOutput(){
                SelfType = NodeValueType.Float,
                Name = "Material Metallic"
            },

            new NodeOutput(){
                SelfType = NodeValueType.Float,
                Name = "Material Roughness"
            },

            new NodeOutput(){
                SelfType = NodeValueType.UInt,
                Name = "Material Index"
            },
        },

@"vec4 $TEMP@0$ = sampleMap(m_texture_diffuse, m_diffuse_frame);
vec4 $TEMP@1$ = sampleMap(m_texture_aomr, m_aomr_frame);
vec4 $TEMP@2$ = sampleMap(m_texture_emissive, m_emissive_frame);
$OUTPUT@0$ = $TEMP@0$.rgb;
$OUTPUT@1$ = $TEMP@0$.a;
$OUTPUT@2$ = getNormalFromMap();
$OUTPUT@3$ = $TEMP@2$.rgb;
$OUTPUT@4$ = $TEMP@1$.r;
$OUTPUT@5$ = $TEMP@1$.g;
$OUTPUT@6$ = $TEMP@1$.b;
$OUTPUT@7$ = m_diffuse_color.rgb;
$OUTPUT@8$ = m_diffuse_color.a;
$OUTPUT@9$ = m_metal_factor;
$OUTPUT@10$ = m_roughness_factor;
$OUTPUT@11$ = material_index;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.CreateAlbedo().Cast<Vector4, Vector3>(x => x.Xyz()),
    1 => ctx.CreateMatrix(1.0f),
    2 => ctx.CreateNormal().Cast<Vector4, Vector3>(x => x.Xyz()),
    3 => ctx.CreateMatrix(Vector3.Zero),
    4 => ctx.CreateAOMR().Cast<Vector4, float>(x => x.X),
    5 => ctx.CreateMatrix(0.0f),
    6 => ctx.CreateAOMR().Cast<Vector4, float>(x => x.Z),
    7 => ctx.CreateMatrix(Vector3.One),
    8 => ctx.CreateMatrix(1.0f),
    9 => ctx.CreateMatrix(1.0f),
    10 => ctx.CreateMatrix(1.0f),
    11 => ctx.CreateMatrix(0u),
    _ => ctx.CreateMatrix(0u)
});

        public static ShaderNodeTemplate MaterialAlpha { get; } = new ShaderNodeTemplate(Guid.Parse("ce7189b5-c66d-4836-9cd4-e5c8937c32d0"), ShaderTemplateCategory.Inputs, "User Alpha", true, Array.Empty<NodeInput>(), new NodeOutput[] { new NodeOutput() { Name = "Alpha", SelfType = NodeValueType.Float } },
@"$OUTPUT@0$ = alpha;",
(ctx, matrix, outIndex) => ctx.CreateMatrix(1.0f)
);

        public static ShaderNodeTemplate MaterialTintColor { get; } = new ShaderNodeTemplate(Guid.Parse("1a220e15-4e06-4df2-ba80-4fa1bb2f2d32"), ShaderTemplateCategory.Inputs, "User Tint Color", true, Array.Empty<NodeInput>(), new NodeOutput[] { new NodeOutput() { Name = "Color", SelfType = NodeValueType.Vec3 }, new NodeOutput() { Name = "Alpha", SelfType = NodeValueType.Float } },
@"$OUTPUT@0$ = tint_color.rgb;
$OUTPUT@1$ = tint_color.a;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.CreateMatrix(Vector3.One),
    _ => ctx.CreateMatrix(1.0f)
}
);

        public static ShaderNodeTemplate TimeData { get; } = new ShaderNodeTemplate(Guid.Parse("12266c37-51b8-4eb3-bcde-73580ea23b0d"), ShaderTemplateCategory.Inputs, "Time Data", true, Array.Empty<NodeInput>(), new NodeOutput[] { new NodeOutput() { Name = "Frame", SelfType = NodeValueType.UInt }, new NodeOutput() { Name = "Update", SelfType = NodeValueType.UInt } },
@"$OUTPUT@0$ = frame;
$OUTPUT@1$ = update;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.CreateMatrix((uint)Client.Instance.Frontend.FramesExisted),
    _ => ctx.CreateMatrix((uint)Client.Instance.Frontend.UpdatesExisted)
}
);

        public static ShaderNodeTemplate GeometryData { get; } = new ShaderNodeTemplate(Guid.Parse("2d314027-1f83-469c-9d22-29c8f4539a63"), ShaderTemplateCategory.Inputs, "Geometry Data", true, Array.Empty<NodeInput>(),
            new NodeOutput[] {
                new NodeOutput() { Name = "World Tangent", SelfType = NodeValueType.Vec3 },
                new NodeOutput() { Name = "World Bitangent", SelfType = NodeValueType.Vec3 },
                new NodeOutput() { Name = "World Normal", SelfType = NodeValueType.Vec3 },
                new NodeOutput() { Name = "World Position", SelfType = NodeValueType.Vec3 },
                new NodeOutput() { Name = "Geometry Tangent", SelfType = NodeValueType.Vec3 },
                new NodeOutput() { Name = "Geometry Bitangent", SelfType = NodeValueType.Vec3 },
                new NodeOutput() { Name = "Geometry Normal", SelfType = NodeValueType.Vec3 },
                new NodeOutput() { Name = "Geometry Position", SelfType = NodeValueType.Vec3 },
                new NodeOutput() { Name = "Screen Position", SelfType = NodeValueType.Vec3 },
                new NodeOutput() { Name = "Screen Size", SelfType = NodeValueType.Vec2 }
            },
@"$OUTPUT@0$ = f_tbn[0];
$OUTPUT@1$ = f_tbn[1];
$OUTPUT@2$ = f_tbn[2];
$OUTPUT@3$ = f_world_position;
$OUTPUT@4$ = f_tangent;
$OUTPUT@5$ = f_bitangent;
$OUTPUT@6$ = f_normal;
$OUTPUT@7$ = f_position;
$OUTPUT@8$ = gl_FragCoord.xyz;
$OUTPUT@9$ = viewport_size;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.CreateMatrix(Vector3.UnitX),
    1 => ctx.CreateMatrix(Vector3.UnitY),
    2 => ctx.CreateMatrix(Vector3.UnitZ),
    3 => ctx.CreateMatrix(Vector3.Zero),
    4 => ctx.CreateMatrix(Vector3.UnitX),
    5 => ctx.CreateMatrix(Vector3.UnitY),
    6 => ctx.CreateMatrix(Vector3.UnitZ),
    7 => ctx.CreateMatrix(Vector3.Zero),
    8 => ctx.CreateScreenPositions(),
    9 => ctx.CreateViewportSize(),
    _ => ctx.CreateMatrix(Vector3.Zero)
});

        public static ShaderNodeTemplate Mapping { get; } = new ShaderNodeTemplate(Guid.Parse("a278f63a-57f0-4aa0-b10c-f65b5d248739"), ShaderTemplateCategory.Inputs, "Texture Coordinates", true, Array.Empty<NodeInput>(), new NodeOutput[] { new NodeOutput() { Name = "Geometry UVs", SelfType = NodeValueType.Vec2 } },
@"$OUTPUT@0$ = f_texture;",
(ctx, matrix, outIndex) => ctx.CreateUVs()
);

        public static ShaderNodeTemplate CursorLocation { get; } = new ShaderNodeTemplate(Guid.Parse("9d52d71d-117b-4bb3-8949-48e85f57bf35"), ShaderTemplateCategory.Inputs, "Cursor Position", true, Array.Empty<NodeInput>(), new NodeOutput[] {
            new NodeOutput() { Name = "Worldspace", SelfType = NodeValueType.Vec3 },
            new NodeOutput() { Name = "Screenspace", SelfType = NodeValueType.Vec2 }
        },
@"$OUTPUT@0$ = cursor_position;
$OUTPUT@1$ = (projection * view * vec4(cursor_position, 1.0)).xy;",
(ctx, matrix, outIndex) => outIndex switch { 
    0 => ctx.CreateMatrix(Vector3.Zero),
    _ => ctx.CreateMatrix(Vector2.Zero),
});

        public static ShaderNodeTemplate ParticleInfo { get; } = new ShaderNodeTemplate(Guid.Parse("8da1f120-4bc6-400b-898c-65eb0323a656"), ShaderTemplateCategory.Inputs, "Particle Data", true, Array.Empty<NodeInput>(), new NodeOutput[] {
            new NodeOutput(){ Name = "Spritemap Frame", SelfType = NodeValueType.Int },
            new NodeOutput(){ Name = "Particle Index", SelfType = NodeValueType.Int },
            new NodeOutput(){ Name = "Particle Color", SelfType = NodeValueType.Vec4 },
            new NodeOutput(){ Name = "Particle Lifespan", SelfType = NodeValueType.Float },
        },
@"$OUTPUT@0$ = f_frame;
$OUTPUT@1$ = inst_id;
$OUTPUT@2$ = inst_color;
$OUTPUT@3$ = inst_lifespan;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.CreateMatrix(0),
    1 => ctx.CreateMatrix(0),
    2 => ctx.CreateMatrix(Vector4.One),
    _ => ctx.CreateMatrix(1.0f - (Client.Instance.Frontend.UpdatesExisted % 60 / 60.0f)),
});

        #endregion

        #region Samplers
        public static ShaderNodeTemplate SampleAlbedo { get; } = new ShaderNodeTemplate(Guid.Parse("e057ff81-0699-4e02-8eef-5f808d71fe34"), ShaderTemplateCategory.Samplers, "Albedo Sampler", true,
            new NodeInput[] {
                new NodeInput(){ Name = "Texture Coordinates", SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero }
            },
            new NodeOutput[] {
                new NodeOutput(){ Name = "Color", SelfType = NodeValueType.Vec3 },
                new NodeOutput(){ Name = "Alpha", SelfType = NodeValueType.Float }
            },
@"vec4 $TEMP@0$ = sampleMapCustom(m_texture_diffuse, $INPUT@0$, m_diffuse_frame);
$OUTPUT@0$ = $TEMP@0$.rgb;
$OUTPUT@1$ = $TEMP@0$.a;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.CreateAlbedo(matrix[0]).Cast<Vector4, Vector3>(x => x.Xyz()),
    _ => ctx.CreateMatrix(1.0f)
});

        public static ShaderNodeTemplate SampleEmission { get; } = new ShaderNodeTemplate(Guid.Parse("a3664983-c17d-42e7-8493-bb0ceca9937d"), ShaderTemplateCategory.Samplers, "Emissive Sampler", true,
            new NodeInput[] {
                new NodeInput(){ Name = "Texture Coordinates", SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero }
            },
            new NodeOutput[] {
                new NodeOutput(){ Name = "Color", SelfType = NodeValueType.Vec3 },
                new NodeOutput(){ Name = "Alpha", SelfType = NodeValueType.Float }
            },
@"vec4 $TEMP@0$ = sampleMapCustom(m_texture_emissive, $INPUT@0$, m_emissive_frame);
$OUTPUT@0$ = $TEMP@0$.rgb;
$OUTPUT@1$ = $TEMP@0$.a;",
(ctx, matrix, outIndex) => outIndex switch
{
    0 => ctx.CreateMatrix(Vector3.Zero),
    _ => ctx.CreateMatrix(1.0f)
});

        public static ShaderNodeTemplate SampleNormal { get; } = new ShaderNodeTemplate(Guid.Parse("370e5679-2d46-464d-b5d6-f4938b2c9328"), ShaderTemplateCategory.Samplers, "Normal Sampler", true,
            new NodeInput[] {
                new NodeInput(){ Name = "Texture Coordinates", SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero }
            },
            new NodeOutput[] {
                new NodeOutput(){ Name = "Normal", SelfType = NodeValueType.Vec3 },
            },
@"$OUTPUT@0$ = getNormalFromMapCustom($INPUT@0$);",
(ctx, matrix, outIndex) => outIndex switch
{
    0 => ctx.CreateNormal(matrix[0]).Cast<Vector4, Vector3>(x => x.Xyz()),
    _ => ctx.CreateMatrix(1.0f)
});

        public static ShaderNodeTemplate SampleAOMR { get; } = new ShaderNodeTemplate(Guid.Parse("4ee9d9b9-7cbd-4526-a827-f8b280e73ac7"), ShaderTemplateCategory.Samplers, "AoMR Sampler", true,
            new NodeInput[] {
                new NodeInput(){ Name = "Texture Coordinates", SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero }
            },
            new NodeOutput[] {
                new NodeOutput(){ Name = "Ambient Occlusion", SelfType = NodeValueType.Float },
                new NodeOutput(){ Name = "Metallic", SelfType = NodeValueType.Float },
                new NodeOutput(){ Name = "Roughness", SelfType = NodeValueType.Float }
            },
@"vec4 $TEMP@0$ = sampleMapCustom(m_texture_aomr, $INPUT@0$, m_aomr_frame);
$OUTPUT@0$ = $TEMP@0$.r;
$OUTPUT@1$ = $TEMP@0$.g;
$OUTPUT@2$ = $TEMP@0$.b;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.CreateAOMR(matrix[0]).Cast<Vector4, float>(x => x.X),
    1 => ctx.CreateMatrix(0.0f),
    _ => ctx.CreateAOMR(matrix[0]).Cast<Vector4, float>(x => x.Z)
});

        public static ShaderNodeTemplate SampleCustomTexture { get; } = new ShaderNodeTemplate(Guid.Parse("924608ae-46d5-4b05-ab4d-7cbbaa23cdbc"), ShaderTemplateCategory.Samplers, "Custom Sampler", true,
            new NodeInput[] {
                new NodeInput(){ Name = "Texture Index", SelfType = NodeValueType.Int, CurrentValue = 0 },
                new NodeInput(){ Name = "Texture Coordinates", SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero }
            },
            new NodeOutput[] {
                new NodeOutput(){ Name = "Color", SelfType = NodeValueType.Vec3 },
                new NodeOutput(){ Name = "Alpha", SelfType = NodeValueType.Float }
            },
@"vec4 $TEMP@0$ = sampleExtraTexture($INPUT@0$, $INPUT@1$);
$OUTPUT@0$ = $TEMP@0$.rgb;
$OUTPUT@1$ = $TEMP@0$.a;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.CreateCustomImage(GetCustomTextureForIndex((int)matrix[0].SimulationPixels[0]).Value, matrix[1]).Cast<Vector4, Vector3>(x => x.Xyz()),
    _ => ctx.CreateMatrix(1.0f),
});

        public static ShaderNodeTemplate SampleAlbedoAtFrame { get; } = new ShaderNodeTemplate(Guid.Parse("f6a8d5bd-4774-4be3-ae55-e7b7f1cafbe1"), ShaderTemplateCategory.Samplers, "Particle Albedo Sampler", true,
            new NodeInput[] {
                new NodeInput(){ Name = "Texture Coordinates", SelfType = NodeValueType.Vec2, CurrentValue = Vector2.Zero },
                new NodeInput(){ Name = "Spritesheet Frame", SelfType = NodeValueType.Int, CurrentValue = 0 },
            },

            new NodeOutput[] {
                new NodeOutput(){ Name = "Color", SelfType = NodeValueType.Vec3 },
                new NodeOutput(){ Name = "Alpha", SelfType = NodeValueType.Float }
            },
@"vec4 $TEMP@0$ = sampleCustomMapAtFrame(m_texture_diffuse, $INPUT@0$, m_diffuse_frame, $INPUT@1$);
$OUTPUT@0$ = $TEMP@0$.rgb;
$OUTPUT@1$ = $TEMP@0$.a;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.CreateAlbedo(matrix[0]).Cast<Vector4, Vector3>(x => x.Xyz()),
    _ => ctx.CreateMatrix(1.0f)
});

        #endregion

        #region Vector Decomposition and Composition

        public static ShaderNodeTemplate DecomposeVec2 { get; } = new ShaderNodeTemplate(Guid.Parse("0563de9b-42b7-46ec-a850-824b1570f85b"), ShaderTemplateCategory.VectorC, "Vec2 - Decompose", true, new NodeInput[] {
            new NodeInput() { CurrentValue = Vector2.Zero, Name = "Vec2", SelfType = NodeValueType.Vec2 } }, new NodeOutput[] {
            new NodeOutput(){ Name = "X", SelfType = NodeValueType.Float },
            new NodeOutput(){ Name = "Y", SelfType = NodeValueType.Float }
        },
@"$OUTPUT@0$ = $INPUT@0$.x;
$OUTPUT@1$ = $INPUT@0$.y;",
(ctx, matrix, outIndex) => outIndex switch { 
    0 => ctx.Parallel(matrix, x => ((Vector2)x[0]).X),
    _ => ctx.Parallel(matrix, x => ((Vector2)x[0]).Y),
});

        public static ShaderNodeTemplate ConstructVec2 { get; } = new ShaderNodeTemplate(Guid.Parse("644be138-5ddc-4d2b-b84a-b3dadf9dd31d"), ShaderTemplateCategory.VectorC, "Vec2 - Construct", true, new NodeInput[] {
            new NodeInput(){ Name = "X", SelfType = NodeValueType.Float, CurrentValue = 0f },
            new NodeInput(){ Name = "Y", SelfType = NodeValueType.Float, CurrentValue = 0f }
        }, new NodeOutput[] {
            new NodeOutput(){ Name = "Result", SelfType = NodeValueType.Vec2 }
        },
@"$OUTPUT@0$ = vec2($INPUT@0$, $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => new Vector2((float)x[0], (float)x[1]))
);

        public static ShaderNodeTemplate DecomposeVec3 { get; } = new ShaderNodeTemplate(Guid.Parse("c57508c2-af00-411e-8d8c-2779ba6bcf30"), ShaderTemplateCategory.VectorC, "Vec3 - Decompose", true, new NodeInput[] { new NodeInput() { CurrentValue = Vector3.Zero, Name = "Vec3", SelfType = NodeValueType.Vec3 } }, new NodeOutput[] {
            new NodeOutput(){ Name = "X", SelfType = NodeValueType.Float },
            new NodeOutput(){ Name = "Y", SelfType = NodeValueType.Float },
            new NodeOutput(){ Name = "Z", SelfType = NodeValueType.Float },
        },
@"$OUTPUT@0$ = $INPUT@0$.x;
$OUTPUT@1$ = $INPUT@0$.y;
$OUTPUT@2$ = $INPUT@0$.z;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.Parallel(matrix, x => ((Vector3)x[0]).X),
    1 => ctx.Parallel(matrix, x => ((Vector3)x[0]).Y),
    _ => ctx.Parallel(matrix, x => ((Vector3)x[0]).Z),
});

        public static ShaderNodeTemplate ConstructVec3 { get; } = new ShaderNodeTemplate(Guid.Parse("b34a2485-1332-4cfa-b083-d896f6046bb5"), ShaderTemplateCategory.VectorC, "Vec3 - Construct", true, new NodeInput[] {
            new NodeInput(){ Name = "X", SelfType = NodeValueType.Float, CurrentValue = 0f },
            new NodeInput(){ Name = "Y", SelfType = NodeValueType.Float, CurrentValue = 0f },
            new NodeInput(){ Name = "Z", SelfType = NodeValueType.Float, CurrentValue = 0f },
        }, new NodeOutput[] {
            new NodeOutput(){ Name = "Result", SelfType = NodeValueType.Vec3 }
        },
@"$OUTPUT@0$ = vec3($INPUT@0$, $INPUT@1$, $INPUT@2$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => new Vector3((float)x[0], (float)x[1], (float)x[2]))
);

        public static ShaderNodeTemplate DecomposeVec4 { get; } = new ShaderNodeTemplate(Guid.Parse("b880b1af-3bdc-498b-92bd-1f38f728d9a3"), ShaderTemplateCategory.VectorC, "Vec4 - Decompose", true, new NodeInput[] {
            new NodeInput() { CurrentValue = Vector4.Zero, Name = "Vec4", SelfType = NodeValueType.Vec4 } }, new NodeOutput[] {
            new NodeOutput(){ Name = "X", SelfType = NodeValueType.Float },
            new NodeOutput(){ Name = "Y", SelfType = NodeValueType.Float },
            new NodeOutput(){ Name = "Z", SelfType = NodeValueType.Float },
            new NodeOutput(){ Name = "W", SelfType = NodeValueType.Float },
        },
@"$OUTPUT@0$ = $INPUT@0$.x;
$OUTPUT@1$ = $INPUT@0$.y;
$OUTPUT@2$ = $INPUT@0$.z;
$OUTPUT@3$ = $INPUT@0$.w;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.Parallel(matrix, x => ((Vector4)x[0]).X),
    1 => ctx.Parallel(matrix, x => ((Vector4)x[0]).Y),
    2 => ctx.Parallel(matrix, x => ((Vector4)x[0]).Z),
    _ => ctx.Parallel(matrix, x => ((Vector4)x[0]).W),
});

        public static ShaderNodeTemplate ConstructVec4 { get; } = new ShaderNodeTemplate(Guid.Parse("083c4d73-88ed-43aa-936b-248f199d27dd"), ShaderTemplateCategory.VectorC, "Vec4 - Construct", true, new NodeInput[] {
            new NodeInput(){ Name = "X", SelfType = NodeValueType.Float, CurrentValue = 0f },
            new NodeInput(){ Name = "Y", SelfType = NodeValueType.Float, CurrentValue = 0f },
            new NodeInput(){ Name = "Z", SelfType = NodeValueType.Float, CurrentValue = 0f },
            new NodeInput(){ Name = "W", SelfType = NodeValueType.Float, CurrentValue = 0f },
        }, new NodeOutput[] {
            new NodeOutput(){ Name = "Result", SelfType = NodeValueType.Vec4 }
        },
@"$OUTPUT@0$ = vec4($INPUT@0$, $INPUT@1$, $INPUT@2$, $INPUT@3$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => new Vector4((float)x[0], (float)x[1], (float)x[2], (float)x[3]))
);

        public static ShaderNodeTemplate ConstructColor4 { get; } = new ShaderNodeTemplate(Guid.Parse("8cbd90a3-ef17-4258-ac68-da70a527de33"), ShaderTemplateCategory.VectorC, "Vec4 - Color", true, new NodeInput[] {
            new NodeInput(){ Name = "Vec4", SelfType = NodeValueType.Vec4, CurrentValue = new Vector4(1, 1, 1, 1) },
        }, new NodeOutput[] {
            new NodeOutput(){ Name = "Result", SelfType = NodeValueType.Vec4 },
            new NodeOutput(){ Name = "Color", SelfType = NodeValueType.Vec3 },
            new NodeOutput(){ Name = "Alpha", SelfType = NodeValueType.Float },
        },
@"$OUTPUT@0$ = $INPUT@0$;
$OUTPUT@1$ = $INPUT@0$.rgb;
$OUTPUT@2$ = $INPUT@0$.a;",
(ctx, matrix, outIndex) => outIndex switch {
    0 => ctx.Parallel(matrix, x => (Vector4)x[0]),
    1 => ctx.Parallel(matrix, x => ((Vector4)x[0]).Xyz()),
    _ => ctx.Parallel(matrix, x => ((Vector4)x[0]).W),
});

        #endregion

        #region Logic

        public static ShaderNodeTemplate IntEq { get; } = new ShaderNodeTemplate(Guid.Parse("d11e45e0-96c6-45fa-b8a8-429b94cf7819"), ShaderTemplateCategory.LogicInt, "Equals", true,
                new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0, Name = "Value 1", SelfType = NodeValueType.Int },
                    new NodeInput(){ CurrentValue = 0, Name = "Value 2", SelfType = NodeValueType.Int },
                },

                new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
                },
@"$OUTPUT@0$ = $INPUT@0$ == $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] == (int)x[1])
);

        public static ShaderNodeTemplate IntNEq { get; } = new ShaderNodeTemplate(Guid.Parse("84338ece-1f10-4d72-9e6d-dc9c830c8b85"), ShaderTemplateCategory.LogicInt, "Not Equal", true,
                new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0, Name = "Value 1", SelfType = NodeValueType.Int },
                    new NodeInput(){ CurrentValue = 0, Name = "Value 2", SelfType = NodeValueType.Int },
                },

                new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
                },
@"$OUTPUT@0$ = $INPUT@0$ != $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] != (int)x[1])
);

        public static ShaderNodeTemplate IntGr { get; } = new ShaderNodeTemplate(Guid.Parse("90f2a168-11eb-4040-85e5-174bd648dadd"), ShaderTemplateCategory.LogicInt, "Greater", true,
                new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0, Name = "Value 1", SelfType = NodeValueType.Int },
                    new NodeInput(){ CurrentValue = 0, Name = "Value 2", SelfType = NodeValueType.Int },
                },

                new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
                },
@"$OUTPUT@0$ = $INPUT@0$ > $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] > (int)x[1])
        );

        public static ShaderNodeTemplate IntGrEq { get; } = new ShaderNodeTemplate(Guid.Parse("08149435-4af8-47ba-9aa4-fa8f4a09d411"), ShaderTemplateCategory.LogicInt, "Greater or Equal", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0, Name = "Value 1", SelfType = NodeValueType.Int },
                    new NodeInput(){ CurrentValue = 0, Name = "Value 2", SelfType = NodeValueType.Int },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = $INPUT@0$ >= $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] >= (int)x[1])
        );

        public static ShaderNodeTemplate IntLess { get; } = new ShaderNodeTemplate(Guid.Parse("e7ecf128-e03b-4684-898b-9708a2ffa979"), ShaderTemplateCategory.LogicInt, "Less", true,
                new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0, Name = "Value 1", SelfType = NodeValueType.Int },
                    new NodeInput(){ CurrentValue = 0, Name = "Value 2", SelfType = NodeValueType.Int },
                },

                new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
                },
@"$OUTPUT@0$ = $INPUT@0$ < $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] < (int)x[1])
        );

        public static ShaderNodeTemplate IntLEq { get; } = new ShaderNodeTemplate(Guid.Parse("63583b40-278f-4199-a1a8-0fbdf2d8b780"), ShaderTemplateCategory.LogicInt, "Less or Equal", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0, Name = "Value 1", SelfType = NodeValueType.Int },
                    new NodeInput(){ CurrentValue = 0, Name = "Value 2", SelfType = NodeValueType.Int },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = $INPUT@0$ <= $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (int)x[0] <= (int)x[1])
        );

        public static ShaderNodeTemplate IntBounds { get; } = new ShaderNodeTemplate(Guid.Parse("98ec85c6-5632-42a0-a81f-897dfceb7e2e"), ShaderTemplateCategory.LogicInt, "Is in bounds", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0, Name = "Value 1", SelfType = NodeValueType.Int },
                    new NodeInput(){ CurrentValue = 0, Name = "Min", SelfType = NodeValueType.Int },
                    new NodeInput(){ CurrentValue = 0, Name = "Max", SelfType = NodeValueType.Int },
                    new NodeInput(){ CurrentValue = false, Name = "Inclusive", SelfType = NodeValueType.Bool },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = $INPUT@3$ ? $INPUT@0$ >= $INPUT@1$ && $INPUT@0$ <= $INPUT@2$ : $INPUT@0$ > $INPUT@1$ && $INPUT@0$ < $INPUT@2$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (bool)x[3] ? (int)x[0] >= (int)x[1] && (int)x[0] <= (int)x[2] : (int)x[0] > (int)x[1] && (int)x[0] < (int)x[2])
        );

        public static ShaderNodeTemplate FloatEq { get; } = new ShaderNodeTemplate(Guid.Parse("5ca94b7b-8a9e-4a45-8ec9-96356119525f"), ShaderTemplateCategory.LogicFloat, "Equals", true,
                new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 1", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 2", SelfType = NodeValueType.Float },
                },

                new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
                },
@"$OUTPUT@0$ = abs($INPUT@0$ - $INPUT@1$) <= eff_epsilon;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Abs((float)x[0] - (float)x[1]) <= 0.001f)
        );

        public static ShaderNodeTemplate FloatREq { get; } = new ShaderNodeTemplate(Guid.Parse("1861fa12-f135-44ef-ac0d-b231e8a210a0"), ShaderTemplateCategory.LogicFloat, "Roughly Equals", true,
                new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 1", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 2", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0.001f, Name = "Allowed Deviation", SelfType = NodeValueType.Float },
                },

                new NodeOutput[] {
                    new NodeOutput() { Name = "Result",  SelfType = NodeValueType.Bool },
                },
@"$OUTPUT@0$ = abs($INPUT@0$ - $INPUT@1$) <= $INPUT@2$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => MathF.Abs((float)x[0] - (float)x[1]) <= (float)x[2])
        );

        public static ShaderNodeTemplate FloatNEq { get; } = new ShaderNodeTemplate(Guid.Parse("5dbf310a-2e68-49c1-903b-224901b0dc5a"), ShaderTemplateCategory.LogicFloat, "Not Equal", true,
                new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 1", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 2", SelfType = NodeValueType.Float },
                },

                new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
                },
@"$OUTPUT@0$ = $INPUT@0$ != $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] != (float)x[1])
        );

        public static ShaderNodeTemplate FloatGr { get; } = new ShaderNodeTemplate(Guid.Parse("8a7f33e6-6b88-44a4-a4ad-b4b7b1909b5f"), ShaderTemplateCategory.LogicFloat, "Greater", true,
                new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 1", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 2", SelfType = NodeValueType.Float },
                },

                new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
                },
@"$OUTPUT@0$ = $INPUT@0$ > $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] > (float)x[1])

        );

        public static ShaderNodeTemplate FloatGrEq { get; } = new ShaderNodeTemplate(Guid.Parse("8d3e1a06-505f-4589-b9b2-3b689455a470"), ShaderTemplateCategory.LogicFloat, "Greater or Equal", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 1", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 2", SelfType = NodeValueType.Float },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = $INPUT@0$ >= $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] >= (float)x[1])
        );

        public static ShaderNodeTemplate FloatLess { get; } = new ShaderNodeTemplate(Guid.Parse("a9b4da5f-cb87-41ab-9717-fa5dc180b6ae"), ShaderTemplateCategory.LogicFloat, "Less", true,
                new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 1", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 2", SelfType = NodeValueType.Float },
                },

                new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
                },
@"$OUTPUT@0$ = $INPUT@0$ < $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] < (float)x[1])
        );

        public static ShaderNodeTemplate FloatLEq { get; } = new ShaderNodeTemplate(Guid.Parse("0bdab4c9-fa03-4229-ba64-ea7afce91485"), ShaderTemplateCategory.LogicFloat, "Less or Equal", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 1", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 2", SelfType = NodeValueType.Float },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = $INPUT@0$ <= $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (float)x[0] <= (float)x[1])
        );

        public static ShaderNodeTemplate FloatBounds { get; } = new ShaderNodeTemplate(Guid.Parse("f3182bad-4d9d-4aeb-84f0-8d87f8c19733"), ShaderTemplateCategory.LogicFloat, "Is in bounds", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = 0f, Name = "Value 1", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0f, Name = "Min", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = 0f, Name = "Max", SelfType = NodeValueType.Float },
                    new NodeInput(){ CurrentValue = false, Name = "Inclusive", SelfType = NodeValueType.Bool },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = $INPUT@3$ ? $INPUT@0$ >= $INPUT@1$ && $INPUT@0$ <= $INPUT@2$ : $INPUT@0$ > $INPUT@1$ && $INPUT@0$ < $INPUT@2$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (bool)x[3] ? (float)x[0] >= (float)x[1] && (float)x[0] <= (float)x[2] : (float)x[0] > (float)x[1] && (float)x[0] < (float)x[2])
        );

        public static ShaderNodeTemplate Bool2Int { get; } = new ShaderNodeTemplate(Guid.Parse("79974674-fcf3-4021-ba37-e0e48b3e9ed4"), ShaderTemplateCategory.Logic, "Logic to Int", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = false, Name = "Logic", SelfType = NodeValueType.Bool },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Int },
               },
@"$OUTPUT@0$ = $INPUT@0$ ? 1 : 0;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (bool)x[0] ? 1 : 0)
        );

        public static ShaderNodeTemplate Bool2Float { get; } = new ShaderNodeTemplate(Guid.Parse("51b0296b-936d-43d9-b164-0030f67ce5d6"), ShaderTemplateCategory.Logic, "Logic to Float", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = false, Name = "Logic", SelfType = NodeValueType.Bool },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Float },
               },
@"$OUTPUT@0$ = $INPUT@0$ ? 1.0 : 0.0;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (bool)x[0] ? 1f : 0f)
        );

        public static ShaderNodeTemplate BoolInverse { get; } = new ShaderNodeTemplate(Guid.Parse("1b6077f5-7608-46a9-a73b-4c25e4910373"), ShaderTemplateCategory.Logic, "Not", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = false, Name = "Logic", SelfType = NodeValueType.Bool },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = !$INPUT@0$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => !(bool)x[0])
        );

        public static ShaderNodeTemplate BoolAnd { get; } = new ShaderNodeTemplate(Guid.Parse("21cd5531-4dec-4803-9e3f-40c315134edd"), ShaderTemplateCategory.Logic, "And", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = false, Name = "Logic 1", SelfType = NodeValueType.Bool },
                    new NodeInput(){ CurrentValue = false, Name = "Logic 2", SelfType = NodeValueType.Bool },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = $INPUT@0$ && $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (bool)x[0] && (bool)x[1])
        );

        public static ShaderNodeTemplate BoolOr { get; } = new ShaderNodeTemplate(Guid.Parse("ff0a341b-e2fa-4c30-b21c-844ca8f05860"), ShaderTemplateCategory.Logic, "Or", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = false, Name = "Logic 1", SelfType = NodeValueType.Bool },
                    new NodeInput(){ CurrentValue = false, Name = "Logic 2", SelfType = NodeValueType.Bool },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = $INPUT@0$ || $INPUT@1$;",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => (bool)x[0] || (bool)x[1])
        );

        public static ShaderNodeTemplate BoolXor { get; } = new ShaderNodeTemplate(Guid.Parse("477b3509-0037-446f-a508-0159169d08c0"), ShaderTemplateCategory.Logic, "Exclusive Or", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = false, Name = "Logic 1", SelfType = NodeValueType.Bool },
                    new NodeInput(){ CurrentValue = false, Name = "Logic 2", SelfType = NodeValueType.Bool },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = ($INPUT@0$ || $INPUT@1$) && !($INPUT@0$ && $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => ((bool)x[0] || (bool)x[1]) && !((bool)x[0] && (bool)x[1]))
        );

        public static ShaderNodeTemplate BoolNand { get; } = new ShaderNodeTemplate(Guid.Parse("d7bdd24f-fb9e-403d-a67d-21d713bb2c61"), ShaderTemplateCategory.Logic, "Sheffer Stroke", true,
               new NodeInput[] {
                    new NodeInput(){ CurrentValue = false, Name = "Logic 1", SelfType = NodeValueType.Bool },
                    new NodeInput(){ CurrentValue = false, Name = "Logic 2", SelfType = NodeValueType.Bool },
               },

               new NodeOutput[] {
                    new NodeOutput() { Name = "Result", SelfType = NodeValueType.Bool },
               },
@"$OUTPUT@0$ = !($INPUT@0$ && $INPUT@1$);",
(ctx, matrix, outIndex) => ctx.Parallel(matrix, x => !((bool)x[0] && (bool)x[1]))
        );

        #endregion

        #region Images
        private static readonly Lazy<Image<Rgba32>> dummyAlbedo = new Lazy<Image<Rgba32>>(() => IOVTT.ResourceToImage<Rgba32>("VTT.Embed.dummyAlbedo.png"));
        private static readonly Lazy<Image<Rgba32>> dummyNormal = new Lazy<Image<Rgba32>>(() => IOVTT.ResourceToImage<Rgba32>("VTT.Embed.dummyNormal.png"));
        private static readonly Lazy<Image<Rgba32>> dummyAOMR = new Lazy<Image<Rgba32>>(() => IOVTT.ResourceToImage<Rgba32>("VTT.Embed.dummyAOMR.png"));
        private static readonly Lazy<Image<Rgba32>>[] dummyCustomTextures = new Lazy<Image<Rgba32>>[64];
        private static readonly Lazy<Image<Rgba32>> previewAlbedo = new Lazy<Image<Rgba32>>(() => IOVTT.ResourceToImage<Rgba32>("VTT.Embed.previewAlbedo.png"));
        private static readonly Lazy<Image<Rgba32>> previewNormal = new Lazy<Image<Rgba32>>(() => IOVTT.ResourceToImage<Rgba32>("VTT.Embed.previewNormal.png"));
        private static readonly Lazy<Image<Rgba32>> previewAOMR = new Lazy<Image<Rgba32>>(() => IOVTT.ResourceToImage<Rgba32>("VTT.Embed.previewAOMR.png"));
        public static Lazy<SimulationContext> DummyContext { get; } = new Lazy<SimulationContext>(() => new SimulationContext(32, 32, dummyAlbedo.Value, dummyNormal.Value, dummyAOMR.Value));
        public static Lazy<SimulationContext> PreviewContext { get; } = new Lazy<SimulationContext>(() => new SimulationContext(256, 256, previewAlbedo.Value, previewNormal.Value, previewAOMR.Value));

        private static Lazy<Image<Rgba32>> GetCustomTextureForIndex(int index)
        {
            index = Math.Abs(index);
            index %= 64;
            if (dummyCustomTextures[index] == null)
            {
                dummyCustomTextures[index] = new Lazy<Image<Rgba32>>(() => {
                    Random rand = new Random(index);
                    int idx0 = index & 7;
                    int idx1 = (index >> 7) & 7;
                    float rngHue = rand.NextSingle();
                    HSVColor hsvBack = new HSVColor(rngHue * 360, 1f, 1f);
                    Color hsvPattern0 = new HSVColor(((rngHue * 360) + 120) % 360, 1f, 1f);
                    Color hsvPattern1 = new HSVColor(((rngHue * 360) + 240) % 360, 1f, 1f);
                    Image<Rgba32> mainBgImg = new Image<Rgba32>(32, 32, ((Color)hsvBack).ToPixel<Rgba32>());
                    Image<Rgba32> pattern0 = IOVTT.ResourceToImage<Rgba32>($"VTT.Embed.pattern{idx0}.png");
                    for (int i = 0; i < 32 * 32; ++i)
                    {
                        int x = i % 32;
                        int y = i / 32;
                        Rgba32 pixel = pattern0[x, y];
                        pattern0[x, y] = new Rgba32(hsvPattern0.RedB(), hsvPattern0.GreenB(), hsvPattern0.BlueB(), pixel.A);
                    }

                    Image<Rgba32> pattern1 = IOVTT.ResourceToImage<Rgba32>($"VTT.Embed.pattern{idx1}.png");
                    for (int i = 0; i < 32 * 32; ++i)
                    {
                        int x = i % 32;
                        int y = i / 32;
                        Rgba32 pixel = pattern1[x, y];
                        pattern1[x, y] = new Rgba32(hsvPattern1.RedB(), hsvPattern1.GreenB(), hsvPattern1.BlueB(), pixel.A);
                    }

                    mainBgImg.Mutate(x => x.DrawImage(pattern0, PixelColorBlendingMode.Normal, 1f));
                    mainBgImg.Mutate(x => x.DrawImage(pattern1, PixelColorBlendingMode.Normal, 1f));
                    pattern0.Dispose();
                    pattern1.Dispose();
                    return mainBgImg;
                });
            }

            return dummyCustomTextures[index];
        }

        #endregion

        public delegate NodeSimulationMatrix SimulatorProgram(SimulationContext ctx, NodeSimulationMatrix[] inputs, int outputIndex);
        public ShaderNodeTemplate(Guid id, ShaderTemplateCategory cat, string v, bool deletable, NodeInput[] nodeInputs, NodeOutput[] nodeOutputs, string code, SimulatorProgram sim)
        {
            this.Name = v;
            this.NodeInputs = nodeInputs;
            this.NodeOutputs = nodeOutputs;
            this.Deletable = deletable;
            this.Code = code;
            this.Category = cat;
            this.Category.Templates.Add(this);
            this.ID = id;
            this.Simulator = sim;
            TemplatesByID[id] = this;
        }

        public string Name { get; }
        public NodeInput[] NodeInputs { get; }
        public NodeOutput[] NodeOutputs { get; }
        public bool Deletable { get; }
        public string Code { get; }
        public Guid ID { get; }
        public ShaderTemplateCategory Category { get; }
        public SimulatorProgram Simulator { get; } 

        public ShaderNode CreateNode()
        {
            int maxS = this.NodeInputs.Length + this.NodeOutputs.Length;
            int mW = Math.Max(this.Name.Length * 12, 100);
            return new ShaderNode()
            {
                NodeID = Guid.NewGuid(),
                Deletable = this.Deletable,
                IsDeleted = false,
                Name = this.Name,
                Inputs = this.NodeInputs.Select(x => x.Copy().ValidateValue()).ToList(),
                Outputs = this.NodeOutputs.Select(x => x.Copy()).ToList(),
                TemplateID = this.ID,
                Size = new Vector2(mW, 40 + (maxS * 20))
            };
        }
    }

    public class ShaderTemplateCategory
    {
        public static List<ShaderTemplateCategory> Roots { get; } = new List<ShaderTemplateCategory>();

        public static ShaderTemplateCategory None { get; } = new ShaderTemplateCategory("Uncategorized", Color.OrangeRed);
        public static ShaderTemplateCategory Inputs { get; } = new ShaderTemplateCategory("Inputs", Color.MidnightBlue);
        public static ShaderTemplateCategory Math { get; } = new ShaderTemplateCategory("Math", Color.Firebrick);
        public static ShaderTemplateCategory MathFloat { get; } = new ShaderTemplateCategory("Float", Color.DarkGreen, Math);
        public static ShaderTemplateCategory MathInt { get; } = new ShaderTemplateCategory("Int", Color.DarkSeaGreen, Math);
        public static ShaderTemplateCategory MathVec2 { get; } = new ShaderTemplateCategory("Vec2", Color.DarkKhaki, Math);
        public static ShaderTemplateCategory MathVec3 { get; } = new ShaderTemplateCategory("Vec3", Color.DarkGoldenrod, Math);
        public static ShaderTemplateCategory MathVec4 { get; } = new ShaderTemplateCategory("Vec4", Color.SaddleBrown, Math);
        public static ShaderTemplateCategory VectorC { get; } = new ShaderTemplateCategory("Vector Data", Color.SteelBlue);
        public static ShaderTemplateCategory Samplers { get; } = new ShaderTemplateCategory("Samplers", Color.DimGray, Inputs);
        public static ShaderTemplateCategory Logic { get; } = new ShaderTemplateCategory("Logic", Color.DarkSlateBlue);
        public static ShaderTemplateCategory LogicInt { get; } = new ShaderTemplateCategory("Int", Color.Indigo, Logic);
        public static ShaderTemplateCategory LogicFloat { get; } = new ShaderTemplateCategory("Float", Color.DarkSlateGrey, Logic);

        public ShaderTemplateCategory ParentCategory { get; set; }
        public string Name { get; set; }
        public Color DisplayColor { get; set; }
        public List<ShaderTemplateCategory> Children { get; } = new List<ShaderTemplateCategory>();
        public List<ShaderNodeTemplate> Templates { get; } = new List<ShaderNodeTemplate>();

        public ShaderTemplateCategory(string name, Color clr, ShaderTemplateCategory parentCategory = null)
        {
            this.ParentCategory = parentCategory;
            this.DisplayColor = clr;
            if (parentCategory != null)
            {
                parentCategory.Children.Add(this);
            }
            else
            {
                Roots.Add(this);
            }

            this.Name = name;
        }
    }
}
