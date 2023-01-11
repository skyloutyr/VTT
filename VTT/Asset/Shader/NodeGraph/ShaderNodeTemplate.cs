namespace VTT.Asset.Shader.NodeGraph
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.Linq;

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

            new NodeOutput[0],
@"albedo = $INPUT@0$;
normal = $INPUT@1$;
emissive = $INPUT@2$;
a = $INPUT@3$;
ao = $INPUT@4$;
m = $INPUT@5$;
r = $INPUT@6$;
"
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
        
@"$OUTPUT@0$ = $INPUT@0$ * $INPUT@1$;
"

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

@"$OUTPUT@0$ = $INPUT@0$ + $INPUT@1$;
"

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

@"$OUTPUT@0$ = $INPUT@0$ - $INPUT@1$;
"

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

@"$OUTPUT@0$ = $INPUT@0$ / $INPUT@1$;
");

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
                SelfType = NodeValueType.Vec3
            }
        },

@"$OUTPUT@0$ = dot($INPUT@0$, $INPUT@1$);
");

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

@"$OUTPUT@0$ = cross($INPUT@0$, $INPUT@1$);
");

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

@"$OUTPUT@0$ = reflect($INPUT@0$, $INPUT@1$);
");

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

@"$OUTPUT@0$ = -$INPUT@0$;
");

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

@"$OUTPUT@0$ = normalize($INPUT@0$);
");

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

@"$OUTPUT@0$ = length($INPUT@0$);
");

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

@"$OUTPUT@0$ = $INPUT@0$ * $INPUT@1$;
");
        #endregion

        #region Material Uniforms

        public static ShaderNodeTemplate MaterialData = new ShaderNodeTemplate(Guid.Parse("fcb1fc5e-deaf-410d-b811-3ba90cef9da6"), ShaderTemplateCategory.Inputs, "Material Info", true, new NodeInput[0], new NodeOutput[] {
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
");
        
        public static ShaderNodeTemplate MaterialAlpha = new ShaderNodeTemplate(Guid.Parse("ce7189b5-c66d-4836-9cd4-e5c8937c32d0"), ShaderTemplateCategory.Inputs, "User Alpha", true, new NodeInput[0], new NodeOutput[] { new NodeOutput() { Name = "Alpha", SelfType = NodeValueType.Float } },
@"$OUTPUT@0$ = alpha;
");

        public static ShaderNodeTemplate MaterialTintColor = new ShaderNodeTemplate(Guid.Parse("1a220e15-4e06-4df2-ba80-4fa1bb2f2d32"), ShaderTemplateCategory.Inputs, "User Tint Color", true, new NodeInput[0], new NodeOutput[] { new NodeOutput() { Name = "Color", SelfType = NodeValueType.Vec3 }, new NodeOutput() { Name = "Alpha", SelfType = NodeValueType.Float } },
@"$OUTPUT@0$ = tint_color.rgb;
$OUTPUT@1$ = tint_color.a;
");

        #endregion

        #region Vector Decomposition and Composition
        public static ShaderNodeTemplate DecomposeVec3 { get; } = new ShaderNodeTemplate(Guid.Parse("c57508c2-af00-411e-8d8c-2779ba6bcf30"), ShaderTemplateCategory.VectorC, "Vec3 - Decompose", true, new NodeInput[] { new NodeInput() { CurrentValue = Vector3.Zero, Name = "Vec3", SelfType = NodeValueType.Vec3 } }, new NodeOutput[] {
            new NodeOutput(){ Name = "X", SelfType = NodeValueType.Float },
            new NodeOutput(){ Name = "Y", SelfType = NodeValueType.Float },
            new NodeOutput(){ Name = "Z", SelfType = NodeValueType.Float },
        },
@"$OUTPUT@0$ = $INPUT@0$.x;
$OUTPUT@1$ = $INPUT@0$.y;
$OUTPUT@2$ = $INPUT@0$.z;
");

        public static ShaderNodeTemplate ConstructVec3 { get; } = new ShaderNodeTemplate(Guid.Parse("b34a2485-1332-4cfa-b083-d896f6046bb5"), ShaderTemplateCategory.VectorC, "Vec3 - Construct", true, new NodeInput[] {
            new NodeInput(){ Name = "X", SelfType = NodeValueType.Float, CurrentValue = 0 },
            new NodeInput(){ Name = "Y", SelfType = NodeValueType.Float, CurrentValue = 0 },
            new NodeInput(){ Name = "Z", SelfType = NodeValueType.Float, CurrentValue = 0 },
        }, new NodeOutput[] {
            new NodeOutput(){ Name = "Result", SelfType = NodeValueType.Vec3 }
        },
@"$OUTPUT@0$ = vec3($INPUT@0$, $INPUT@1$, $INPUT@2$);
");

        #endregion

        public ShaderNodeTemplate(Guid id, ShaderTemplateCategory cat, string v, bool deletable, NodeInput[] nodeInputs, NodeOutput[] nodeOutputs, string code)
        {
            this.Name = v;
            this.NodeInputs = nodeInputs;
            this.NodeOutputs = nodeOutputs;
            this.Deletable = deletable;
            this.Code = code;
            this.Category = cat;
            this.Category.Templates.Add(this);
            this.ID = id;
            TemplatesByID[id] = this;
        }

        public string Name { get; }
        public NodeInput[] NodeInputs { get; }
        public NodeOutput[] NodeOutputs { get; }
        public bool Deletable { get; }
        public string Code { get; }
        public Guid ID { get; }
        public ShaderTemplateCategory Category { get; }

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
                Inputs = this.NodeInputs.Select(x => x.Copy()).ToList(),
                Outputs = this.NodeOutputs.Select(x => x.Copy()).ToList(),
                TemplateID = this.ID,
                Size = new Vector2(mW, 40 + maxS * 20)
            };
        }
    }

    public class ShaderTemplateCategory
    {
        public static List<ShaderTemplateCategory> Roots { get; } = new List<ShaderTemplateCategory>();

        public static ShaderTemplateCategory None { get; } = new ShaderTemplateCategory("Uncategorized");
        public static ShaderTemplateCategory Inputs { get; } = new ShaderTemplateCategory("Inputs");
        public static ShaderTemplateCategory Math { get; } = new ShaderTemplateCategory("Math");
        public static ShaderTemplateCategory MathFloat { get; } = new ShaderTemplateCategory("Float", Math);
        public static ShaderTemplateCategory MathInt { get; } = new ShaderTemplateCategory("Int", Math);
        public static ShaderTemplateCategory MathVec2 { get; } = new ShaderTemplateCategory("Vec2", Math);
        public static ShaderTemplateCategory MathVec3 { get; } = new ShaderTemplateCategory("Vec3", Math);
        public static ShaderTemplateCategory MathVec4 { get; } = new ShaderTemplateCategory("Vec4", Math);
        public static ShaderTemplateCategory VectorC { get; } = new ShaderTemplateCategory("Vector Data");

        public ShaderTemplateCategory ParentCategory { get; set; }
        public string Name { get; set; }
        public List<ShaderTemplateCategory> Children { get; } = new List<ShaderTemplateCategory>();
        public List<ShaderNodeTemplate> Templates { get; } = new List<ShaderNodeTemplate>();

        public ShaderTemplateCategory(string name, ShaderTemplateCategory parentCategory = null)
        {
            this.ParentCategory = parentCategory;
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
