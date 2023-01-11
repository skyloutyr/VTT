namespace VTT.Asset.Shader.NodeGraph
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Util;

    public class ShaderGraph : ISerializable
    {
        public List<ShaderNode> Nodes { get; set; } = new List<ShaderNode>();
        public object Lock = new object();
        public bool IsLoaded { get; set; } = false;

        public Dictionary<Guid, (ShaderNode, NodeOutput)> AllOutputsById { get; } = new Dictionary<Guid, (ShaderNode, NodeOutput)>();
        public Dictionary<Guid, (ShaderNode, NodeInput)> AllInputsById { get; } = new Dictionary<Guid, (ShaderNode, NodeInput)>();

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
                    NodeOutput no = this.AllOutputsById[ni.ConnectedOutput].Item2;
                    string outPtr = "out_" + ni.ConnectedOutput.ToString("N");
                    code = code.Replace(cIn, this.Convert(no.SelfType, ni.SelfType, outPtr));
                    c++;
                }

                if (c == 0)
                {
                    break; // Found no inputs, outputs or temps to replace
                }

                i++;
            }

            emitted.Add(nodeFrom);
            codeStr = codeStr + code;
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

            foreach (NodeInput ni in this.AllInputsById.Select(x => x.Value.Item2))
            {
                if (!ni.ConnectedOutput.Equals(Guid.Empty) && this.AllOutputsById.TryGetValue(ni.ConnectedOutput, out (ShaderNode, NodeOutput) val))
                { 
                    if (val.Item2.SelfType != ni.SelfType)
                    {
                        warnings.Add("shadergraph.warn.automatic_type_conversion@" + val.Item1.Name);
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
            if (depth >= 32) // Too deep, recursion may not be present but chain too long either way
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

        public void FillDefaultLayout()
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

            int wLayer1 = (int)(new[] { materialData, uniformAlpha, uniformTint }).Select(x => x.Size.X).Max() + 40;
            int wLayer2 = wLayer1 + (int)(new[] { mulAlbedo, mulA0 }).Select(x => x.Size.X).Max() + 40;

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
            ret.SetArray("Nodes", this.Nodes.ToArray(), (n, c, v) => {
                DataElement d = v.Serialize();
                c.Set(n, d);
            });

            return ret;
        }
    }

    public class ShaderNode : ISerializable
    {
        public Guid NodeID { get; set; }
        public Guid TemplateID { get; set; }
        public string Name { get; set; }
        public Vector2 Location { get; set; }
        public Vector2 Size { get; set; }
        public bool IsDeleted { get; set; }
        public bool Deletable { get; set; }

        public List<NodeInput> Inputs { get; set; } = new List<NodeInput>();
        public List<NodeOutput> Outputs { get; set; } = new List<NodeOutput>();

        public void Deserialize(DataElement e)
        {
            this.NodeID = e.GetGuid("ID");
            this.Name = e.Get<string>("Name");
            this.Location = e.GetVec2("Location");
            this.Size = e.GetVec2("Size");
            this.Deletable = e.Get<bool>("Deletable");
            this.TemplateID = e.GetGuid("Template");
            this.Inputs = new List<NodeInput>(e.GetArray("Ins", (n, c) => { NodeInput i = new NodeInput(); i.Deserialize(c.Get<DataElement>(n)); return i; }, Array.Empty<NodeInput>()));
            this.Outputs = new List<NodeOutput>(e.GetArray("Outs", (n, c) => { NodeOutput i = new NodeOutput(); i.Deserialize(c.Get<DataElement>(n)); return i; }, Array.Empty<NodeOutput>()));
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("ID", this.NodeID);
            ret.Set("Name", this.Name);
            ret.SetVec2("Location", this.Location);
            ret.SetVec2("Size", this.Size);
            ret.Set("Deletable", this.Deletable);
            ret.SetGuid("Template", this.TemplateID);
            ret.SetArray("Ins", this.Inputs.ToArray(), (n, c, v) => c.Set(n, v.Serialize()));
            ret.SetArray("Outs", this.Outputs.ToArray(), (n, c, v) => c.Set(n, v.Serialize()));
            return ret;
        }

        public void ConnectInput(int inputIndex, ShaderNode provider, int providerOutputIndex)
        {
            this.Inputs[inputIndex].ConnectedOutput = provider.Outputs[providerOutputIndex].ID;
        }

        public void ConnectOutput(int outputIndex, ShaderNode to, int toInputIndex)
        {
            to.ConnectInput(toInputIndex, this, outputIndex);
        }

        public ShaderNode FullCopy()
        {
            return new ShaderNode()
            {
                NodeID = this.NodeID,
                TemplateID = this.TemplateID,
                Name = this.Name,
                Location = this.Location,
                Size = this.Size,
                IsDeleted = this.IsDeleted,
                Deletable = this.Deletable,
                Inputs = new List<NodeInput>(this.Inputs.Select(x => x.FullCopy())),
                Outputs = new List<NodeOutput>(this.Outputs.Select(x => x.FullCopy()))
            };
        }
    }

    public enum NodeValueType
    {
        Bool,
        Int,
        UInt,
        Float,
        Vec2,
        Vec3,
        Vec4
    }

    public class NodeInput : ISerializable
    {
        public Guid ID { get; set; }
        public string Name { get; set; }
        public NodeValueType SelfType { get; set; }
        public Guid ConnectedOutput { get; set; }
        public object CurrentValue { get; set; }

        public NodeInput Copy()
        {
            return new NodeInput()
            {
                ID = Guid.NewGuid(),
                Name = this.Name,
                SelfType = this.SelfType,
                CurrentValue = this.CurrentValue
            };
        }

        public NodeInput FullCopy()
        {
            return new NodeInput()
            {
                ID = this.ID,
                Name = this.Name,
                SelfType = this.SelfType,
                ConnectedOutput = this.ConnectedOutput,
                CurrentValue = this.CurrentValue
            };
        }

        public void Deserialize(DataElement e)
        {
            this.ID = e.GetGuid("ID");
            this.Name = e.Get<string>("Name");
            this.SelfType = e.GetEnum<NodeValueType>("Type");
            this.ConnectedOutput = e.GetGuid("Connected");
            switch (this.SelfType)
            {
                case NodeValueType.Int:
                {
                    this.CurrentValue = e.Get<int>("Value");
                    break;
                }

                case NodeValueType.UInt:
                {
                    this.CurrentValue = e.Get<uint>("Value");
                    break;
                }

                case NodeValueType.Float:
                {
                    this.CurrentValue = e.Get<float>("Value");
                    break;
                }

                case NodeValueType.Bool:
                {
                    this.CurrentValue = e.Get<bool>("Value");
                    break;
                }

                case NodeValueType.Vec2:
                {
                    this.CurrentValue = e.GetVec2("Value");
                    break;
                }

                case NodeValueType.Vec3:
                {
                    this.CurrentValue = e.GetVec3("Value");
                    break;
                }

                case NodeValueType.Vec4:
                {
                    this.CurrentValue = e.GetVec4("Value");
                    break;
                }
            }
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("ID", this.ID);
            ret.Set("Name", this.Name);
            ret.SetEnum("Type", this.SelfType);
            ret.SetGuid("Connected", this.ConnectedOutput);
            switch (this.SelfType) 
            {
                case NodeValueType.Int:
                {
                    ret.Set("Value", (int)this.CurrentValue);
                    break;
                }

                case NodeValueType.UInt:
                {
                    ret.Set("Value", (uint)this.CurrentValue);
                    break;
                }

                case NodeValueType.Float:
                {
                    ret.Set("Value", (float)this.CurrentValue);
                    break;
                }

                case NodeValueType.Bool:
                {
                    ret.Set("Value", (bool)this.CurrentValue);
                    break;
                }

                case NodeValueType.Vec2:
                {
                    ret.SetVec2("Value", (Vector2)this.CurrentValue);
                    break;
                }

                case NodeValueType.Vec3:
                {
                    ret.SetVec3("Value", (Vector3)this.CurrentValue);
                    break;
                }

                case NodeValueType.Vec4:
                {
                    ret.SetVec4("Value", (Vector4)this.CurrentValue);
                    break;
                }
            }

            return ret;
        }
    }

    public class NodeOutput : ISerializable
    {
        public Guid ID { get; set; }
        public string Name { get; set; }
        public NodeValueType SelfType { get; set; }

        public NodeOutput Copy()
        {
            return new NodeOutput() {
                ID = Guid.NewGuid(),
                Name = this.Name,
                SelfType = this.SelfType
            };
        }

        public NodeOutput FullCopy()
        {
            return new NodeOutput()
            {
                ID = this.ID,
                Name = this.Name,
                SelfType = this.SelfType
            };
        }

        public void Deserialize(DataElement e)
        {
            this.ID = e.GetGuid("ID");
            this.Name = e.Get<string>("Name");
            this.SelfType = e.GetEnum<NodeValueType>("Type");
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("ID", this.ID);
            ret.Set("Name", this.Name);
            ret.SetEnum("Type", this.SelfType);
            return ret;
        }
    }
}
