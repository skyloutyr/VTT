namespace VTT.Asset.Shader.NodeGraph
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using VTT.GL;
    using VTT.Network;
    using VTT.Render;
    using VTT.Util;

    public class ShaderGraph : ISerializable
    {
        public List<ShaderNode> Nodes { get; set; } = new List<ShaderNode>();
        public object Lock = new object();
        public bool IsLoaded { get; set; } = false;

        public Dictionary<Guid, (ShaderNode, NodeOutput)> AllOutputsById { get; } = new Dictionary<Guid, (ShaderNode, NodeOutput)>();
        public Dictionary<Guid, (ShaderNode, NodeInput)> AllInputsById { get; } = new Dictionary<Guid, (ShaderNode, NodeInput)>();

        public LinkedTextureContainer ExtraTextures { get; set; } = new LinkedTextureContainer();

        private FastAccessShader _glData;
        private bool _glValid;
        private bool _glGen;

        private static void RemoveDefine(ref string lines, string define)
        {
            string r = "#define " + define;
            int idx = lines.IndexOf(r);
            if (idx != -1)
            {
                lines = lines.Remove(idx, lines.IndexOf('\n', idx) - idx - 1);
            }
        }

        public Image<Rgba32> GetNodeImage(SimulationContext ctx, ShaderNode node, int outputIndex, out NodeSimulationMatrix mat)
        {
            List<ShaderNode> recursionList = new List<ShaderNode>();
            try
            {
                mat = node.SimulateProcess(ctx, this, outputIndex, recursionList);
            }
            catch (StackOverflowException)
            {
                mat = ctx.CreateMatrix(0);
                return new Image<Rgba32>(1, 1, new Rgba32(255, 0, 0, 255));
            }

            Image<Rgba32> ret = new Image<Rgba32>(ctx.Width, ctx.Height);
            switch (mat.SimulationPixels[0])
            {
                case bool:
                {
                    for (int i = 0; i < ctx.Width * ctx.Height; ++i)
                    {
                        int x = i % ctx.Width;
                        int y = i / ctx.Height;
                        bool b = (bool)mat.SimulationPixels[i];
                        ret[x, y] = b ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 255);
                    }

                    break;
                }

                case int:
                {
                    for (int i = 0; i < ctx.Width * ctx.Height; ++i)
                    {
                        int x = i % ctx.Width;
                        int y = i / ctx.Height;
                        int it = (int)mat.SimulationPixels[i];
                        ret[x, y] = it > 0 ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 255);
                    }

                    break;
                }

                case uint:
                {
                    for (int i = 0; i < ctx.Width * ctx.Height; ++i)
                    {
                        int x = i % ctx.Width;
                        int y = i / ctx.Height;
                        uint it = (uint)mat.SimulationPixels[i];
                        ret[x, y] = it > 0 ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 255);
                    }

                    break;
                }

                case float:
                {
                    for (int i = 0; i < ctx.Width * ctx.Height; ++i)
                    {
                        int x = i % ctx.Width;
                        int y = i / ctx.Height;
                        float f = (float)mat.SimulationPixels[i];
                        ret[x, y] = new Rgba32(f, f, f, 1f);
                    }

                    break;
                }

                case Vector2:
                {
                    for (int i = 0; i < ctx.Width * ctx.Height; ++i)
                    {
                        int x = i % ctx.Width;
                        int y = i / ctx.Height;
                        Vector2 v = (Vector2)mat.SimulationPixels[i];
                        ret[x, y] = new Rgba32(v.X, v.Y, 0.0f, 1f);
                    }

                    break;
                }

                case Vector3:
                {
                    for (int i = 0; i < ctx.Width * ctx.Height; ++i)
                    {
                        int x = i % ctx.Width;
                        int y = i / ctx.Height;
                        Vector3 v = (Vector3)mat.SimulationPixels[i];
                        ret[x, y] = new Rgba32(v.X, v.Y, v.Z, 1f);
                    }

                    break;
                }

                case Vector4:
                {
                    for (int i = 0; i < ctx.Width * ctx.Height; ++i)
                    {
                        int x = i % ctx.Width;
                        int y = i / ctx.Height;
                        Vector4 v = (Vector4)mat.SimulationPixels[i];
                        ret[x, y] = new Rgba32(v.X * v.W, v.Y * v.W, v.Z * v.W, 1.0f);
                    }

                    break;
                }

                default:
                {
                    for (int i = 0; i < ctx.Width * ctx.Height; ++i)
                    {
                        int x = i % ctx.Width;
                        int y = i / ctx.Height;
                        Rgba32 rgba = (x >= 16 && y < 16) || (x < 16 && y >= 16) ? new Rgba32(255, 0, 255, 255) : new Rgba32(0, 0, 0, 255);
                        ret[x, y] = rgba;
                    }

                    break;
                }
            }

            return ret;
        }

        public static bool TryInjectCustomShaderCode(bool isParticleShader, string code, out string vertCode, out string fragCode)
        {
            fragCode = IOVTT.ResourceToString(isParticleShader ? "VTT.Embed.particle.frag" : "VTT.Embed.object.frag");
            vertCode = IOVTT.ResourceToString(isParticleShader ? "VTT.Embed.particle.vert" : "VTT.Embed.object.vert");
            if (fragCode.Contains("#pragma ENTRY_NODEGRAPH")) // have nodegraph entry point
            {
                fragCode = fragCode.Replace("#undef NODEGRAPH", "#define NODEGRAPH"); // Mark shader as nodegraph
                fragCode = fragCode.Replace("#pragma ENTRY_NODEGRAPH", code); // Inject compiled code
                if (!isParticleShader) // Particles aren't affected by shadows, no need to mess w/ defines
                {
                    bool dirShadows = Client.Instance.Settings.EnableSunShadows;
                    bool pointShadows = Client.Instance.Settings.EnableDirectionalShadows;
                    bool noBranches = Client.Instance.Settings.DisableShaderBranching;
                    if (!dirShadows)
                    {
                        RemoveDefine(ref vertCode, "HAS_DIRECTIONAL_SHADOWS");
                        RemoveDefine(ref fragCode, "HAS_DIRECTIONAL_SHADOWS");
                    }

                    if (!pointShadows)
                    {
                        RemoveDefine(ref vertCode, "HAS_POINT_SHADOWS");
                        RemoveDefine(ref fragCode, "HAS_POINT_SHADOWS");
                    }

                    if (noBranches)
                    {
                        RemoveDefine(ref vertCode, "BRANCHING");
                        RemoveDefine(ref fragCode, "BRANCHING");
                    }

                    fragCode = fragCode.Replace("#define PCF_ITERATIONS 2", $"#define PCF_ITERATIONS {Client.Instance.Settings.ShadowsPCF}");
                }

                return true;
            }

            vertCode = string.Empty;
            fragCode = string.Empty;
            return false;
        }

        public static bool TryCompileCustomShader(bool isParticleShader, string vertCode, string fragCode, out string err, out FastAccessShader shader)
        {
            if (!ShaderProgram.TryCompile(out ShaderProgram sp, vertCode, string.Empty, fragCode, out err))
            {
                shader = null;
                return false;
            }
            else
            {
                sp.Bind();
                if (isParticleShader) // Particle shaders don't have uniform block access, and don't have shadow maps
                {
                    sp["m_texture_diffuse"].Set(0);
                    sp["m_texture_normal"].Set(1);
                    sp["m_texture_emissive"].Set(2);
                    sp["m_texture_aomr"].Set(3);
                    sp["tex_skybox"].Set(6);
                    sp["unifiedTexture"].Set(12);
                }
                else
                {
                    sp.BindUniformBlock("FrameData", 1); // Bind uniform block to slot 1
                    sp["m_texture_diffuse"].Set(0); // Setup texture locations
                    sp["m_texture_normal"].Set(1);
                    sp["m_texture_emissive"].Set(2);
                    sp["m_texture_aomr"].Set(3);
                    sp["tex_skybox"].Set(6);
                    sp["unifiedTexture"].Set(12);
                    sp["pl_shadow_maps"].Set(13);
                    sp["dl_shadow_map"].Set(14);
                }

                shader = new FastAccessShader(sp);
                return true;
            }
        }

        public FastAccessShader GetGLShader(bool isParticleShader)
        {
            if (!this.IsLoaded)
            {
                return null;
            }

            if (!this._glValid && this._glGen)
            {
                return null;
            }

            if (!this._glGen)
            {
                Logger l = Client.Instance.Logger;
                l.Log(LogLevel.Info, "Compiling custom shader");
                if (this.TryCompileShaderCode(out string code) && TryInjectCustomShaderCode(isParticleShader, code, out string fullVertCode, out string fullFragCode))
                {
                    if (!TryCompileCustomShader(isParticleShader, fullVertCode, fullFragCode, out string err, out this._glData))
                    {
                        l.Log(LogLevel.Error, "Could not compile custom shader!");
                        l.Log(LogLevel.Error, err);
                        this._glData = null;
                        this._glValid = false;
                    }
                    else
                    {
                        this._glValid = true;
                    }
                }
                else
                {
                    this._glData = null;
                    this._glValid = false;
                }

                this._glGen = true;
            }

            return this._glData;
        }

        public Image<Rgba32> GeneratePreviewImage(SimulationContext ctx, out NodeSimulationMatrix mat)
        {
            ShaderNode mainOut = this.Nodes.Find(n => n.TemplateID.Equals(ShaderNodeTemplate.MaterialPBR.ID));
            if (mainOut == null)
            {
                mat = null;
                return null;
            }

            return this.GetNodeImage(ctx, mainOut, 0, out mat);
        }

        public bool TryCompileShaderCode(out string code)
        {
            if (!this.ValidatePreprocess(out _, out _))
            {
                Queue<ShaderNode> nodesToRemove = new Queue<ShaderNode>();
                Dictionary<Guid, NodeInput> pairedInputs = new Dictionary<Guid, NodeInput>();
                foreach (NodeInput ni in this.AllInputsById.Values.Select(x => x.Item2))
                {
                    pairedInputs[ni.ConnectedOutput] = ni;
                }

                // Step 1: sanitize the algorithm, removing useless nodes (a node is useless if all of its outputs are unused)
                while (true)
                {
                    foreach (ShaderNode n in this.Nodes)
                    {
                        bool nRem = n.Outputs.Count > 0;
                        foreach (NodeOutput no in n.Outputs)
                        {
                            if (pairedInputs.ContainsKey(no.ID))
                            {
                                nRem = false;
                                break;
                            }
                        }

                        if (nRem)
                        {
                            nodesToRemove.Enqueue(n);
                        }
                    }

                    if (nodesToRemove.Count == 0)
                    {
                        break;
                    }
                    else
                    {
                        while (nodesToRemove.Count > 0)
                        {
                            ShaderNode sn = nodesToRemove.Dequeue();
                            foreach (NodeOutput no in sn.Outputs)
                            {
                                pairedInputs.Remove(no.ID);
                            }

                            this.RemoveNode(sn);
                        }
                    }
                }

                // Step 2: Find the material output node
                ShaderNode mainOut = this.Nodes.Find(n => n.TemplateID.Equals(ShaderNodeTemplate.MaterialPBR.ID));
                if (mainOut == null)
                {
                    code = string.Empty;
                    return false; // Can't compile without main output
                }

                // Step 3: Recursively emit code
                string mC = "";
                bool fail = false;
                List<ShaderNode> emitted = new List<ShaderNode>();
                this.RecursivelyEmitCode(mainOut, ref fail, ref mC, emitted);
                code = mC;
                return !fail;
            }

            code = string.Empty;
            return false;
        }

        private void RecursivelyEmitCode(ShaderNode nodeFrom, ref bool cascadeFail, ref string codeStr, List<ShaderNode> emitted)
        {
            if (cascadeFail)
            {
                return; // Compile nothing if cascade failure
            }

            if (emitted.Contains(nodeFrom))
            {
                return; // Already emitted code for this node, don't emit twice
            }

            foreach (NodeInput ni in nodeFrom.Inputs)
            {
                if (!Guid.Empty.Equals(ni.ConnectedOutput)) // Walk back and append that code first
                {
                    if (this.AllOutputsById.TryGetValue(ni.ConnectedOutput, out (ShaderNode, NodeOutput) val))
                    {
                        RecursivelyEmitCode(val.Item1, ref cascadeFail, ref codeStr, emitted);
                    }
                    else
                    {
                        cascadeFail = true;
                        return;
                    }
                }
            }

            if (cascadeFail) // Check again - could have failed while child iterating
            {
                return; // Compile nothing if cascade failure
            }

            if (!ShaderNodeTemplate.TemplatesByID.TryGetValue(nodeFrom.TemplateID, out ShaderNodeTemplate template))
            {
                cascadeFail = true;
                return;
            }

            string code = template.Code;
            int i = 0;
            try
            {
                while (true)
                {
                    int c = 0;
                    string cTemp = "$TEMP@" + i + "$";
                    string cOut = "$OUTPUT@" + i + "$";
                    string cIn = "$INPUT@" + i + "$";
                    if (code.Contains(cTemp, StringComparison.OrdinalIgnoreCase))
                    {
                        string tV = "temp_" + nodeFrom.NodeID.ToString("N") + "_" + i;
                        code = code.Replace(cTemp, tV);
                        c++;
                    }

                    if (code.Contains(cOut, StringComparison.OrdinalIgnoreCase))
                    {
                        NodeOutput no = nodeFrom.Outputs[i];
                        string tV = this.Prefix(no.SelfType) + " out_" + nodeFrom.Outputs[i].ID.ToString("N");
                        code = code.Replace(cOut, tV);
                        c++;
                    }

                    if (code.Contains(cIn, StringComparison.OrdinalIgnoreCase))
                    {
                        NodeInput ni = nodeFrom.Inputs[i];
                        if (!ni.ConnectedOutput.IsEmpty())
                        {
                            NodeOutput no = this.AllOutputsById[ni.ConnectedOutput].Item2;
                            string outPtr = "out_" + ni.ConnectedOutput.ToString("N");
                            code = code.Replace(cIn, this.Convert(no.SelfType, ni.SelfType, outPtr));
                        }
                        else
                        {
                            code = code.Replace(cIn, this.CreateValue(ni.SelfType, ni.CurrentValue));
                        }

                        c++;
                    }

                    if (c == 0)
                    {
                        break; // Found no inputs, outputs or temps to replace
                    }

                    i++;
                }
            }
            catch (Exception e)
            {
                Client.Instance.Logger.Log(LogLevel.Error, "Fatal exception while compiling shader! Likely the shader template backend was changed!");
                Client.Instance.Logger.Exception(LogLevel.Error, e);
                cascadeFail = true;
                return;
            }

            emitted.Add(nodeFrom);
            if (!code.EndsWith('\n'))
            {
                code = code + '\n';
            }

            codeStr = codeStr + code;
        }

        public string CreateValue(NodeValueType valType, object value)
        {
            return valType switch
            {
                NodeValueType.Bool => value is bool b ? b.ToString() : "false",
                NodeValueType.Float => value is float f ? f.ToString() : "0.0",
                NodeValueType.Int => value is int i ? i.ToString() : "0",
                NodeValueType.UInt => value is int i ? i.ToString() : value is uint ui ? ui.ToString() : "0",
                NodeValueType.Vec2 => value is Vector2 v ? $"vec2({v.X}, {v.Y})" : "vec2(0, 0)",
                NodeValueType.Vec3 => value is Vector3 v ? $"vec3({v.X}, {v.Y}, {v.Z})" : "vec3(0, 0, 0)",
                NodeValueType.Vec4 => value is Vector4 v ? $"vec4({v.X}, {v.Y}, {v.Z}, {v.W})" : "vec4(0, 0, 0, 0)",
                _ => string.Empty,
            };
        }

        public string Prefix(NodeValueType valType)
        {
            return valType switch
            {
                NodeValueType.Bool => "bool",
                NodeValueType.Int => "int",
                NodeValueType.UInt => "uint",
                NodeValueType.Float => "float",
                NodeValueType.Vec2 => "vec2",
                NodeValueType.Vec3 => "vec3",
                NodeValueType.Vec4 => "vec4",
                _ => "byte"
            };
        }

        public NodeSimulationMatrix SimulationConvert(NodeValueType valFrom, NodeValueType valTo, NodeSimulationMatrix matrixIn)
        {
            for (int i = 0; i < matrixIn.SimulationPixels.Length; ++i)
            {
                matrixIn.SimulationPixels[i] = this.SimulationConvertSingle(valFrom, valTo, matrixIn.SimulationPixels[i]);
            }

            return matrixIn;
        }

        private object SimulationConvertSingle(NodeValueType valFrom, NodeValueType valTo, object value)
        {
            switch (valFrom)
            {
                case NodeValueType.Bool:
                {
                    bool b = (bool)value;
                    switch (valTo)
                    {
                        case NodeValueType.Float:
                        {
                            return b ? 1f : 0f;
                        }

                        case NodeValueType.Int:
                        {
                            return b ? 1 : 0;
                        }

                        case NodeValueType.UInt:
                        {
                            return b ? 1u : 0u;
                        }

                        case NodeValueType.Vec2:
                        {
                            return b ? Vector2.Zero : Vector2.One;
                        }

                        case NodeValueType.Vec3:
                        {
                            return b ? Vector3.Zero : Vector3.One;
                        }

                        case NodeValueType.Vec4:
                        {
                            return b ? Vector4.Zero : Vector4.One;
                        }

                        default:
                        {
                            return value;
                        }
                    }
                }

                case NodeValueType.Int:
                {
                    int i = (int)value;
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return i > 0;
                        }

                        case NodeValueType.Float:
                        {
                            return (float)i;
                        }

                        case NodeValueType.UInt:
                        {
                            return (uint)i;
                        }

                        case NodeValueType.Vec2:
                        {
                            return new Vector2(i, i);
                        }

                        case NodeValueType.Vec3:
                        {
                            return new Vector3(i, i, i);
                        }

                        case NodeValueType.Vec4:
                        {
                            return new Vector4(i, i, i, i);
                        }

                        default:
                        {
                            return i;
                        }
                    }
                }

                case NodeValueType.UInt:
                {
                    uint ui = (uint)value;
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return ui > 0;
                        }

                        case NodeValueType.Float:
                        {
                            return (float)ui;
                        }

                        case NodeValueType.Int:
                        {
                            return (int)ui;
                        }

                        case NodeValueType.Vec2:
                        {
                            return new Vector2(ui, ui);
                        }

                        case NodeValueType.Vec3:
                        {
                            return new Vector3(ui, ui, ui);
                        }

                        case NodeValueType.Vec4:
                        {
                            return new Vector4(ui, ui, ui, ui);
                        }

                        default:
                        {
                            return ui;
                        }
                    }
                }

                case NodeValueType.Float:
                {
                    float f = (float)value;
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return f > 0;
                        }

                        case NodeValueType.Int:
                        {
                            return (int)f;
                        }

                        case NodeValueType.UInt:
                        {
                            return (uint)f;
                        }

                        case NodeValueType.Vec2:
                        {
                            return new Vector2(f, f);
                        }

                        case NodeValueType.Vec3:
                        {
                            return new Vector3(f, f, f);
                        }

                        case NodeValueType.Vec4:
                        {
                            return new Vector4(f, f, f, f);
                        }

                        default:
                        {
                            return f;
                        }
                    }
                }

                case NodeValueType.Vec2:
                {
                    Vector2 vec = (Vector2)value;
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return false;
                        }

                        case NodeValueType.Float:
                        {
                            return vec.X;
                        }

                        case NodeValueType.Int:
                        {
                            return (int)vec.X;
                        }

                        case NodeValueType.UInt:
                        {
                            return (uint)vec.X;
                        }

                        case NodeValueType.Vec3:
                        {
                            return new Vector3(vec.X, vec.Y, 0.0f);
                        }

                        case NodeValueType.Vec4:
                        {
                            return new Vector4(vec.X, vec.Y, 0.0f, 0.0f);
                        }

                        default:
                        {
                            return vec;
                        }
                    }
                }

                case NodeValueType.Vec3:
                {
                    Vector3 vec = (Vector3)value;
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return false;
                        }

                        case NodeValueType.Float:
                        {
                            return vec.X;
                        }

                        case NodeValueType.Int:
                        {
                            return (int)vec.X;
                        }

                        case NodeValueType.UInt:
                        {
                            return (uint)vec.X;
                        }

                        case NodeValueType.Vec2:
                        {
                            return vec.Xy();
                        }

                        case NodeValueType.Vec4:
                        {
                            return new Vector4(vec.X, vec.Y, vec.Z, 0.0f);
                        }

                        default:
                        {
                            return vec;
                        }
                    }
                }

                case NodeValueType.Vec4:
                {
                    Vector4 vec = (Vector4)value;
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return false;
                        }

                        case NodeValueType.Float:
                        {
                            return vec.X;
                        }

                        case NodeValueType.Int:
                        {
                            return (int)vec.X;
                        }

                        case NodeValueType.UInt:
                        {
                            return (uint)vec.X;
                        }

                        case NodeValueType.Vec2:
                        {
                            return vec.Xy();
                        }

                        case NodeValueType.Vec3:
                        {
                            return vec.Xyz();
                        }

                        default:
                        {
                            return vec;
                        }
                    }
                }

                default:
                {
                    return value;
                }
            }

        }

        public string Convert(NodeValueType valFrom, NodeValueType valTo, string ptr)
        {
            switch (valFrom)
            {
                case NodeValueType.Bool:
                {
                    switch (valTo)
                    {
                        case NodeValueType.Float:
                        {
                            return $"float({ptr})";
                        }

                        case NodeValueType.Int:
                        {
                            return $"int({ptr})";
                        }

                        case NodeValueType.UInt:
                        {
                            return $"uint({ptr})";
                        }

                        case NodeValueType.Vec2:
                        {
                            return $"vec2(float({ptr}))";
                        }

                        case NodeValueType.Vec3:
                        {
                            return $"vec3(float({ptr}))";
                        }

                        case NodeValueType.Vec4:
                        {
                            return $"vec4(float({ptr}))";
                        }

                        default:
                        {
                            return ptr;
                        }
                    }
                }

                case NodeValueType.Int:
                {
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return $"{ptr} > 0";
                        }

                        case NodeValueType.Float:
                        {
                            return $"float({ptr})";
                        }

                        case NodeValueType.UInt:
                        {
                            return $"uint({ptr})";
                        }

                        case NodeValueType.Vec2:
                        {
                            return $"vec2(float({ptr}))";
                        }

                        case NodeValueType.Vec3:
                        {
                            return $"vec3(float({ptr}))";
                        }

                        case NodeValueType.Vec4:
                        {
                            return $"vec4(float({ptr}))";
                        }

                        default:
                        {
                            return ptr;
                        }
                    }
                }

                case NodeValueType.UInt:
                {
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return $"{ptr} > 0";
                        }

                        case NodeValueType.Float:
                        {
                            return $"float({ptr})";
                        }

                        case NodeValueType.Int:
                        {
                            return $"int({ptr})";
                        }

                        case NodeValueType.Vec2:
                        {
                            return $"vec2(float({ptr}))";
                        }

                        case NodeValueType.Vec3:
                        {
                            return $"vec3(float({ptr}))";
                        }

                        case NodeValueType.Vec4:
                        {
                            return $"vec4(float({ptr}))";
                        }

                        default:
                        {
                            return ptr;
                        }
                    }
                }

                case NodeValueType.Float:
                {
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return $"{ptr} > 0";
                        }

                        case NodeValueType.Int:
                        {
                            return $"int({ptr})";
                        }

                        case NodeValueType.UInt:
                        {
                            return $"uint({ptr})";
                        }

                        case NodeValueType.Vec2:
                        {
                            return $"vec2({ptr})";
                        }

                        case NodeValueType.Vec3:
                        {
                            return $"vec3({ptr})";
                        }

                        case NodeValueType.Vec4:
                        {
                            return $"vec4({ptr})";
                        }

                        default:
                        {
                            return ptr;
                        }
                    }
                }

                case NodeValueType.Vec2:
                {
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return "false";
                        }

                        case NodeValueType.Float:
                        {
                            return $"{ptr}.x";
                        }

                        case NodeValueType.Int:
                        {
                            return $"int({ptr}.x)";
                        }

                        case NodeValueType.UInt:
                        {
                            return $"uint({ptr}.x)";
                        }

                        case NodeValueType.Vec3:
                        {
                            return $"vec3({ptr}, 0.0)";
                        }

                        case NodeValueType.Vec4:
                        {
                            return $"vec4({ptr}, 0.0, 0.0)";
                        }

                        default:
                        {
                            return ptr;
                        }
                    }
                }

                case NodeValueType.Vec3:
                {
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return "false";
                        }

                        case NodeValueType.Float:
                        {
                            return $"{ptr}.x";
                        }

                        case NodeValueType.Int:
                        {
                            return $"int({ptr}.x)";
                        }

                        case NodeValueType.UInt:
                        {
                            return $"uint({ptr}.x)";
                        }

                        case NodeValueType.Vec2:
                        {
                            return $"{ptr}.xy";
                        }

                        case NodeValueType.Vec4:
                        {
                            return $"vec4({ptr}, 0.0)";
                        }

                        default:
                        {
                            return ptr;
                        }
                    }
                }

                case NodeValueType.Vec4:
                {
                    switch (valTo)
                    {
                        case NodeValueType.Bool:
                        {
                            return "false";
                        }

                        case NodeValueType.Float:
                        {
                            return $"{ptr}.x";
                        }

                        case NodeValueType.Int:
                        {
                            return $"int({ptr}.x)";
                        }

                        case NodeValueType.UInt:
                        {
                            return $"uint({ptr}.x)";
                        }

                        case NodeValueType.Vec2:
                        {
                            return $"{ptr}.xy";
                        }

                        case NodeValueType.Vec3:
                        {
                            return $"{ptr}.xyz";
                        }

                        default:
                        {
                            return ptr;
                        }
                    }
                }

                default:
                {
                    return ptr;
                }
            }
        }

        public bool ValidatePreprocess(out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();
            bool result = false;
            if (this.CheckForRecursion())
            {
                errors.Add("shadergraph.err.recursion");
                result = true;
            }

            foreach ((ShaderNode, NodeInput) inData in this.AllInputsById.Values)
            {
                NodeInput ni = inData.Item2;
                if (!ni.ConnectedOutput.Equals(Guid.Empty) && this.AllOutputsById.TryGetValue(ni.ConnectedOutput, out (ShaderNode, NodeOutput) val))
                {
                    if (val.Item2.SelfType != ni.SelfType)
                    {
                        warnings.Add($"shadergraph.warn.automatic_type_conversion@{val.Item1.Name}->{inData.Item1.Name}(output {val.Item2.Name} of type {val.Item2.SelfType} -> input {ni.Name} of type {ni.SelfType})");
                    }
                }
            }

            return result;
        }

        public bool CheckForRecursion()
        {
            List<ShaderNode> traversed = new List<ShaderNode>();
            Stack<ShaderNode> toTraverse = new Stack<ShaderNode>(this.Nodes);
            while (toTraverse.Count > 0)
            {
                ShaderNode starting = toTraverse.Pop();
                traversed.Clear();
                if (TraverseAllInputsRecursive(starting, traversed, 0))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TraverseAllInputsRecursive(ShaderNode nodeToTravers, List<ShaderNode> traversed, int depth)
        {
            if (depth >= 256) // Too deep, recursion may not be present but chain too long either way
            {
                return true;
            }

            traversed.Add(nodeToTravers);
            foreach (NodeInput ni in nodeToTravers.Inputs)
            {
                if (!ni.ConnectedOutput.Equals(Guid.Empty) && this.AllOutputsById.TryGetValue(ni.ConnectedOutput, out (ShaderNode, NodeOutput) result))
                {
                    ShaderNode localNode = result.Item1;
                    if (this.TraverseAllInputsRecursive(localNode, traversed, depth + 1))
                    {
                        return true;
                    }
                }
            }

            traversed.Remove(nodeToTravers);
            return false;
        }

        public ShaderGraph FullCopy()
        {
            ShaderGraph ret = new ShaderGraph();
            foreach (ShaderNode n in this.Nodes)
            {
                ret.AddNode(n.FullCopy());
            }

            ret.ExtraTextures = this.ExtraTextures.FullCopy();
            ret.IsLoaded = true;
            return ret;
        }

        public void Deserialize(DataElement e)
        {
            this.IsLoaded = false;
            lock (this.Lock)
            {
                this.Nodes.Clear();
                ShaderNode[] nodes = e.GetArray("Nodes", (n, c) =>
                {
                    ShaderNode ret = new ShaderNode();
                    ret.Deserialize(c.GetMap(n));
                    return ret;
                }, Array.Empty<ShaderNode>());

                AllOutputsById.Clear();
                AllInputsById.Clear();
                foreach (ShaderNode n in nodes)
                {
                    this.AddNode(n);
                }

                this.ExtraTextures.DeserializeCompatibility(e);
                this.IsLoaded = true;
            }
        }

        public void RemoveNode(ShaderNode node)
        {
            lock (this.Lock)
            {
                foreach (NodeInput ni in node.Inputs)
                {
                    this.AllInputsById.Remove(ni.ID);
                }

                foreach (NodeOutput no in node.Outputs)
                {
                    foreach (NodeInput ni in this.AllInputsById.Values.Select(x => x.Item2))
                    {
                        if (ni.ConnectedOutput.Equals(no.ID))
                        {
                            ni.ConnectedOutput = Guid.Empty;
                        }
                    }

                    this.AllOutputsById.Remove(no.ID);
                }

                this.Nodes.Remove(node);
            }
        }

        public void AddNode(ShaderNode node)
        {
            lock (this.Lock)
            {
                this.Nodes.Add(node);
                foreach (NodeInput ni in node.Inputs)
                {
                    this.AllInputsById[ni.ID] = (node, ni);
                }

                foreach (NodeOutput no in node.Outputs)
                {
                    this.AllOutputsById[no.ID] = (node, no);
                }
            }
        }

        public void FillDefaultParticleLayout()
        {
            ShaderNode materialData = ShaderNodeTemplate.MaterialData.CreateNode();
            ShaderNode particleData = ShaderNodeTemplate.ParticleInfo.CreateNode();
            ShaderNode mulAlbedo = ShaderNodeTemplate.Vec3Multiply.CreateNode();
            ShaderNode matOut = ShaderNodeTemplate.MaterialPBR.CreateNode();

            mulAlbedo.ConnectInput(0, materialData, 0);
            mulAlbedo.ConnectInput(1, particleData, 2);

            matOut.ConnectInput(0, mulAlbedo, 0);
            matOut.ConnectInput(1, materialData, 2);
            matOut.ConnectInput(2, materialData, 3);
            matOut.ConnectInput(3, materialData, 1);
            matOut.ConnectInput(4, materialData, 4);
            matOut.ConnectInput(5, materialData, 5);
            matOut.ConnectInput(6, materialData, 6);

            int wLayer1 = 240;
            int wLayer2 = 480;

            materialData.Location = new Vector2(0, 0);
            particleData.Location = new Vector2(0, 300);
            mulAlbedo.Location = new Vector2(wLayer1, 0);
            matOut.Location = new Vector2(wLayer2 + 40, 0);

            this.Nodes.Add(particleData);
            this.Nodes.Add(materialData);
            this.Nodes.Add(mulAlbedo);
            this.Nodes.Add(matOut);
        }

        public void FillDefaultObjectLayout()
        {
            ShaderNode materialData = ShaderNodeTemplate.MaterialData.CreateNode();
            ShaderNode uniformAlpha = ShaderNodeTemplate.MaterialAlpha.CreateNode();
            ShaderNode uniformTint = ShaderNodeTemplate.MaterialTintColor.CreateNode();
            ShaderNode mulAlbedo0 = ShaderNodeTemplate.Vec3Multiply.CreateNode();
            ShaderNode mulAlbedo1 = ShaderNodeTemplate.Vec3Multiply.CreateNode();
            ShaderNode mulA0 = ShaderNodeTemplate.FloatMultiply.CreateNode();
            ShaderNode mulA1 = ShaderNodeTemplate.FloatMultiply.CreateNode();
            ShaderNode matOut = ShaderNodeTemplate.MaterialPBR.CreateNode();

            mulA0.ConnectInput(0, uniformAlpha, 0);
            mulA0.ConnectInput(1, uniformTint, 1);
            mulA1.ConnectInput(1, mulA0, 0);
            mulA1.ConnectInput(0, materialData, 1);
            mulAlbedo0.ConnectInput(0, materialData, 0);
            mulAlbedo0.ConnectInput(1, materialData, 7);
            mulAlbedo1.ConnectInput(0, mulAlbedo0, 0);
            mulAlbedo1.ConnectInput(1, uniformTint, 0);

            matOut.ConnectInput(0, mulAlbedo1, 0);
            matOut.ConnectInput(1, materialData, 2);
            matOut.ConnectInput(2, materialData, 3);
            matOut.ConnectInput(3, mulA1, 0);
            matOut.ConnectInput(4, materialData, 4);
            matOut.ConnectInput(5, materialData, 5);
            matOut.ConnectInput(6, materialData, 6);

            int wLayer1 = 240;
            int wLayer2 = 480;

            uniformAlpha.Location = new Vector2(0, 300);
            uniformTint.Location = new Vector2(0, 400);

            mulAlbedo0.Location = new Vector2(wLayer1, 0);
            mulAlbedo1.Location = new Vector2(wLayer2, 0);
            mulA0.Location = new Vector2(wLayer1, 400);
            mulA1.Location = new Vector2(wLayer2, 400);

            matOut.Location = new Vector2(wLayer2 + mulA1.Size.X + 40, 0);

            this.Nodes.Add(materialData);
            this.Nodes.Add(uniformAlpha);
            this.Nodes.Add(uniformTint);
            this.Nodes.Add(mulAlbedo0);
            this.Nodes.Add(mulAlbedo1);
            this.Nodes.Add(mulA0);
            this.Nodes.Add(mulA1);
            this.Nodes.Add(matOut);
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetArray("Nodes", this.Nodes.ToArray(), (n, c, v) =>
            {
                DataElement d = v.Serialize();
                c.SetMap(n, d);
            });

            this.ExtraTextures.SerializeCompatibility(ret);
            return ret;
        }

        public void Free()
        {
            lock (this.Lock)
            {
                if (this._glData != null && this._glGen)
                {
                    this._glValid = false;
                    this._glData.Program.Dispose();
                    this._glData = null;
                }
            }
        }
    }
}
