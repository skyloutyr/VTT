namespace VTT.Asset.Shader.NodeGraph
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Asset.Glb;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class ShaderGraph : ISerializable
    {
        public List<ShaderNode> Nodes { get; set; } = new List<ShaderNode>();
        public object Lock = new object();
        public bool IsLoaded { get; set; } = false;

        public Dictionary<Guid, (ShaderNode, NodeOutput)> AllOutputsById { get; } = new Dictionary<Guid, (ShaderNode, NodeOutput)>();
        public Dictionary<Guid, (ShaderNode, NodeInput)> AllInputsById { get; } = new Dictionary<Guid, (ShaderNode, NodeInput)>();

        public List<Guid> ExtraTexturesAttachments { get; } = new List<Guid>();
        public Texture CombinedExtraTextures { get; set; }
        public Vector2[] CombinedExtraTexturesData { get; set; }
        public TextureAnimation[] CombinedExtraTexturesAnimations { get; set; }
        public bool HasExtraTexture { get; set; }

        private ShaderProgram _glData { get; set; }
        private bool _glValid { get; set; }
        private bool _glGen { get; set; }

        private void RemoveDefine(ref string lines, string define)
        {
            string r = "#define " + define;
            int idx = lines.IndexOf(r);
            if (idx != -1)
            {
                lines = lines.Remove(idx, lines.IndexOf('\n', idx) - idx - 1);
            }
        }

        public ShaderProgram GetGLShader(bool isParticleShader)
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
                if (this.TryCompileShaderCode(out string code))
                {
                    string fullFragCode = IOVTT.ResourceToString(isParticleShader ? "VTT.Embed.particle.frag" : "VTT.Embed.object.frag");
                    string fullVertCode = IOVTT.ResourceToString(isParticleShader ? "VTT.Embed.particle.vert" : "VTT.Embed.object.vert");
                    if (fullFragCode.Contains("#pragma ENTRY_NODEGRAPH")) // have nodegraph entry point
                    {
                        fullFragCode = fullFragCode.Replace("#undef NODEGRAPH", "#define NODEGRAPH"); // Mark shader as nodegraph
                        fullFragCode = fullFragCode.Replace("#pragma ENTRY_NODEGRAPH", code); // Inject compiled code
                        if (!isParticleShader) // Particles aren't affected by shadows, no need to mess w/ defines
                        {
                            bool dirShadows = Client.Instance.Settings.EnableSunShadows;
                            bool pointShadows = Client.Instance.Settings.EnableDirectionalShadows;
                            bool noBranches = Client.Instance.Settings.DisableShaderBranching;
                            if (!dirShadows)
                            {
                                RemoveDefine(ref fullVertCode, "HAS_DIRECTIONAL_SHADOWS");
                                RemoveDefine(ref fullFragCode, "HAS_DIRECTIONAL_SHADOWS");
                            }

                            if (!pointShadows)
                            {
                                RemoveDefine(ref fullVertCode, "HAS_POINT_SHADOWS");
                                RemoveDefine(ref fullFragCode, "HAS_POINT_SHADOWS");
                            }

                            if (noBranches)
                            {
                                RemoveDefine(ref fullVertCode, "BRANCHING");
                                RemoveDefine(ref fullFragCode, "BRANCHING");
                            }

                            fullFragCode = fullFragCode.Replace("#define PCF_ITERATIONS 2", $"#define PCF_ITERATIONS {Client.Instance.Settings.ShadowsPCF}");
                        }

                        if (!ShaderProgram.TryCompile(out ShaderProgram sp, fullVertCode, string.Empty, fullFragCode, out string err))
                        {
                            l.Log(LogLevel.Error, "Could not compile custom shader!");
                            l.Log(LogLevel.Error, err);
                            this._glData = null;
                            this._glValid = false;
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
                                sp["unifiedTexture"].Set(12);
                            }
                            else
                            {
                                sp.BindUniformBlock("FrameData", 1); // Bind uniform block to slot 1
                                sp["m_texture_diffuse"].Set(0); // Setup texture locations
                                sp["m_texture_normal"].Set(1);
                                sp["m_texture_emissive"].Set(2);
                                sp["m_texture_aomr"].Set(3);
                                sp["unifiedTexture"].Set(12);
                                sp["pl_shadow_maps"].Set(13);
                                sp["dl_shadow_map"].Set(14);
                            }

                            this._glData = sp;
                            this._glValid = true;
                        }
                    }
                    else
                    {
                        this._glData = null;
                        this._glValid = false;
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
                    if (code.IndexOf(cTemp) != -1)
                    {
                        string tV = "temp_" + nodeFrom.NodeID.ToString("N") + "_" + i;
                        code = code.Replace(cTemp, tV);
                        c++;
                    }

                    if (code.IndexOf(cOut) != -1)
                    {
                        NodeOutput no = nodeFrom.Outputs[i];
                        string tV = this.Prefix(no.SelfType) + " out_" + nodeFrom.Outputs[i].ID.ToString("N");
                        code = code.Replace(cOut, tV);
                        c++;
                    }

                    if (code.IndexOf(cIn) != -1)
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
                            return $"vec2(float{ptr})";
                        }

                        case NodeValueType.Vec3:
                        {
                            return $"vec3(float{ptr})";
                        }

                        case NodeValueType.Vec4:
                        {
                            return $"vec4(float{ptr})";
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
                            return $"vec2(float{ptr})";
                        }

                        case NodeValueType.Vec3:
                        {
                            return $"vec3(float{ptr})";
                        }

                        case NodeValueType.Vec4:
                        {
                            return $"vec4(float{ptr})";
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
                            return $"vec2(float{ptr})";
                        }

                        case NodeValueType.Vec3:
                        {
                            return $"vec3(float{ptr})";
                        }

                        case NodeValueType.Vec4:
                        {
                            return $"vec4(float{ptr})";
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

            ret.ExtraTexturesAttachments.AddRange(this.ExtraTexturesAttachments);
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
                    ret.Deserialize(c.Get<DataElement>(n));
                    return ret;
                }, Array.Empty<ShaderNode>());

                AllOutputsById.Clear();
                AllInputsById.Clear();
                foreach (ShaderNode n in nodes)
                {
                    this.AddNode(n);
                }

                this.ExtraTexturesAttachments.Clear();
                this.ExtraTexturesAttachments.AddRange(e.GetArray("Textures", (n, c) => c.GetGuid(n), Array.Empty<Guid>()));
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
            ShaderNode mulAlbedo = ShaderNodeTemplate.Vec3Multiply.CreateNode();
            ShaderNode mulA0 = ShaderNodeTemplate.FloatMultiply.CreateNode();
            ShaderNode mulA1 = ShaderNodeTemplate.FloatMultiply.CreateNode();
            ShaderNode matOut = ShaderNodeTemplate.MaterialPBR.CreateNode();

            mulA0.ConnectInput(0, uniformAlpha, 0);
            mulA0.ConnectInput(1, uniformTint, 1);
            mulA1.ConnectInput(1, mulA0, 0);
            mulA1.ConnectInput(0, materialData, 1);
            mulAlbedo.ConnectInput(0, materialData, 0);
            mulAlbedo.ConnectInput(1, uniformTint, 0);

            matOut.ConnectInput(0, mulAlbedo, 0);
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

            mulAlbedo.Location = new Vector2(wLayer1, 0);
            mulA0.Location = new Vector2(wLayer1, 400);
            mulA1.Location = new Vector2(wLayer2, 400);

            matOut.Location = new Vector2(wLayer2 + mulA1.Size.X + 40, 0);

            this.Nodes.Add(materialData);
            this.Nodes.Add(uniformAlpha);
            this.Nodes.Add(uniformTint);
            this.Nodes.Add(mulAlbedo);
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
                c.Set(n, d);
            });

            ret.SetArray("Textures", this.ExtraTexturesAttachments.ToArray(), (n, c, v) => c.SetGuid(n, v));

            return ret;
        }

        public AssetStatus GetExtraTexture(out Texture t, out Vector2[] sizes, out TextureAnimation[] cachedAnimData)
        {
            if (this.HasExtraTexture)
            {
                t = this.CombinedExtraTextures;
                sizes = this.CombinedExtraTexturesData;
                cachedAnimData = this.CombinedExtraTexturesAnimations;
                return AssetStatus.Return;
            }

            foreach (Guid id in this.ExtraTexturesAttachments)
            {
                AssetStatus ast;
                if ((ast = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(id, AssetType.Texture, out Asset tAss)) != AssetStatus.Return || tAss == null || tAss.Texture == null || !tAss.Texture.glReady)
                {
                    t = null;
                    sizes = Array.Empty<Vector2>();
                    cachedAnimData = Array.Empty<TextureAnimation>();
                    return ast == AssetStatus.Return ? AssetStatus.Await : ast; // May return that asset is present but async data is not present yet
                }
            }

            // If we are here then all textures are ready
            this.GenerateUnifiedExtraTexture();
            t = this.CombinedExtraTextures;
            sizes = this.CombinedExtraTexturesData;
            cachedAnimData = this.CombinedExtraTexturesAnimations;
            return AssetStatus.Return;
        }

        public unsafe void GenerateUnifiedExtraTexture() // Ensure that all asset data was transmitted
        {
            if (this.ExtraTexturesAttachments.Count == 0)
            {
                this.HasExtraTexture = true;
                return;
            }

            this.HasExtraTexture = false;
            int maxW = 0;
            int maxH = 0;
            bool s = true;
            if (this.CombinedExtraTexturesData == null || this.CombinedExtraTexturesData.Length != this.ExtraTexturesAttachments.Count)
            {
                this.CombinedExtraTexturesData = new Vector2[this.ExtraTexturesAttachments.Count];
                this.CombinedExtraTexturesAnimations = new TextureAnimation[this.ExtraTexturesAttachments.Count];
            }

            Image<Rgba32>[] imgs = new Image<Rgba32>[this.ExtraTexturesAttachments.Count];
            int i = 0;
            foreach (Guid id in this.ExtraTexturesAttachments) // Ensure data loaded
            {
                if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(id, AssetType.Texture, out Asset tAss) == AssetStatus.Return && tAss != null && tAss.Texture != null && tAss.Texture.glReady)
                {
                    imgs[i++] = tAss.Texture.CompoundImage();
                    maxW = Math.Max(maxW, imgs[i - 1].Width);
                    maxH = Math.Max(maxH, imgs[i - 1].Height);
                    this.CombinedExtraTexturesAnimations[i - 1] = tAss.Texture.CachedAnimation;
                }
                else
                {
                    s = false;
                }
            }

            if (s)
            {
                this.CombinedExtraTextures?.Dispose();
                this.CombinedExtraTextures = new Texture(TextureTarget.Texture2DArray);
                this.CombinedExtraTextures.Bind();
                this.CombinedExtraTextures.SetWrapParameters(WrapParam.Repeat, WrapParam.Repeat, WrapParam.Repeat);
                this.CombinedExtraTextures.SetFilterParameters(FilterParam.Linear, FilterParam.Linear);
                GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba, maxW, maxH, this.ExtraTexturesAttachments.Count, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                for (i = 0; i < this.ExtraTexturesAttachments.Count; ++i)
                {
                    Image<Rgba32> img = imgs[i];
                    this.CombinedExtraTexturesData[i] = new Vector2(img.Width, img.Height);
                    img.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem);
                    System.Buffers.MemoryHandle hnd = mem.Pin();
                    GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, i, img.Width, img.Height, 1, PixelFormat.Rgba, PixelType.UnsignedByte, new IntPtr(hnd.Pointer));
                    hnd.Dispose();
                    img.Dispose();
                }

                this.HasExtraTexture = true;
            }
        }
    }
}
