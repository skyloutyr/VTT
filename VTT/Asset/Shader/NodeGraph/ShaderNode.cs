namespace VTT.Asset.Shader.NodeGraph
{
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Util;

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

        public NodeSimulationMatrix SimulateProcess(ShaderGraph container, int index, List<ShaderNode> recursionProtection)
        {
            if (recursionProtection.Contains(this))
            {
                throw new StackOverflowException();
            }

            recursionProtection.Add(this);
            NodeSimulationMatrix[] ins = new NodeSimulationMatrix[this.Inputs.Count];
            for (int i = 0; i < this.Inputs.Count; i++)
            {
                NodeInput ni = this.Inputs[i];
                NodeSimulationMatrix o = ins[i];
                if (!ni.ConnectedOutput.Equals(Guid.Empty) && container.AllOutputsById.TryGetValue(ni.ConnectedOutput, out (ShaderNode, NodeOutput) data))
                {
                    o = data.Item1.SimulateProcess(container, data.Item1.Outputs.IndexOf(data.Item2), recursionProtection);
                    o = container.SimulationConvert(data.Item2.SelfType, ni.SelfType, o);
                }
                else
                {
                    o = container.SimulationConvert(ni.SelfType, ni.SelfType, new NodeSimulationMatrix(ni.CurrentValue));
                }

                ins[i] = o;
            }

            recursionProtection.Remove(this);
            if (ShaderNodeTemplate.TemplatesByID.TryGetValue(this.TemplateID, out ShaderNodeTemplate val))
            {
                return val.Simulator(ins, index);
            }
            else
            {
                throw new Exception("Invalid shader node template!");
            }
        }

        public void ConnectInput(int inputIndex, ShaderNode provider, int providerOutputIndex) => this.Inputs[inputIndex].ConnectedOutput = provider.Outputs[providerOutputIndex].ID;
        public void ConnectOutput(int outputIndex, ShaderNode to, int toInputIndex) => to.ConnectInput(toInputIndex, this, outputIndex);

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

    public class NodeSimulationMatrix
    {
        public object[] SimulationPixels { get; }

        public NodeSimulationMatrix()
        {
            this.SimulationPixels = new object[32 * 32];
        }

        public NodeSimulationMatrix(NodeSimulationMatrix val) : this()
        {
            for (int i = 0; i < 32 * 32; ++i)
            {
                this.SimulationPixels[i] = val.SimulationPixels[i];
            }
        }

        public NodeSimulationMatrix(object o) : this()
        {
            for (int i = 0; i < 32 * 32; ++i)
            {
                this.SimulationPixels[i] = o;
            }
        }

        public NodeSimulationMatrix Cast<T1, T>(Func<T1, T> processor)
        {
            for (int i = 0; i < 32 * 32; ++i)
            {
                this.SimulationPixels[i] = processor((T1)this.SimulationPixels[i]);
            }

            return this;
        }

        public NodeSimulationMatrix(Image<Rgba32> image) : this()
        {
            for (int i = 0; i < 32 * 32; ++i)
            {
                int x = i % 32;
                int y = i / 32;
                this.SimulationPixels[i] = image[x, y].ToScaledVector4().GLVector();
            }
        }

        public NodeSimulationMatrix(Image<Rgba32> image, Vector2 coordinateOffsets) : this()
        {
            for (int i = 0; i < 32 * 32; ++i)
            {
                int x = i % 32;
                int y = i / 32;
                x += (int)(coordinateOffsets.X * 32);
                y += (int)(coordinateOffsets.Y * 32);
                if (x < 0)
                {
                    x = Math.Abs(x);
                }

                if (y < 0)
                {
                    y = Math.Abs(y);
                }

                x %= 32;
                y %= 32;
                this.SimulationPixels[i] = image[x, y].ToScaledVector4().GLVector();
            }
        }

        public NodeSimulationMatrix(Image<Rgba32> image, NodeSimulationMatrix offsetsMatrix)
        {
            for (int i = 0; i < 32 * 32; ++i)
            {
                int x = i % 32;
                int y = i / 32;
                Vector2 coordinateOffsets = (Vector2)offsetsMatrix.SimulationPixels[i];
                x += (int)(coordinateOffsets.X * 32);
                y += (int)(coordinateOffsets.Y * 32);
                if (x < 0)
                {
                    x = Math.Abs(x);
                }

                if (y < 0)
                {
                    y = Math.Abs(y);
                }

                x %= 32;
                y %= 32;
                this.SimulationPixels[i] = image[x, y].ToScaledVector4().GLVector();
            }
        }

        public string GetAverageValueAsString()
        {
            switch (this.SimulationPixels[0])
            {
                case bool:
                {
                    bool b = (bool)this.SimulationPixels[0];
                    for (int i = 0; i < 32 * 32; ++i)
                    {
                        bool b1 = (bool)this.SimulationPixels[i];
                        if (b1 != b)
                        {
                            return "varies";
                        }
                    }

                    return b.ToString();
                }

                case int:
                {
                    int accum = 0;
                    for (int i = 0; i < 32 * 32; ++i)
                    {
                        accum += (int)this.SimulationPixels[i];
                    }

                    return (accum / 1024f).ToString("0.000");
                }

                case uint:
                {
                    uint accum = 0;
                    for (int i = 0; i < 32 * 32; ++i)
                    {
                        accum += (uint)this.SimulationPixels[i];
                    }

                    return (accum / 1024f).ToString("0.000");
                }

                case float:
                {
                    float accum = 0;
                    for (int i = 0; i < 32 * 32; ++i)
                    {
                        accum += (float)this.SimulationPixels[i];
                    }

                    return (accum / 1024f).ToString("0.000");
                }

                case Vector2:
                {
                    Vector2 accum = Vector2.Zero;
                    for (int i = 0; i < 32 * 32; ++i)
                    {
                        accum += (Vector2)this.SimulationPixels[i];
                    }

                    accum /= 1024f;
                    return $"({accum.X:0.000}, {accum.Y:0.000})";
                }

                case Vector3:
                {
                    Vector3 accum = Vector3.Zero;
                    for (int i = 0; i < 32 * 32; ++i)
                    {
                        accum += (Vector3)this.SimulationPixels[i];
                    }

                    accum /= 1024f;
                    return $"({accum.X:0.000}, {accum.Y:0.000}, {accum.Z:0.000})";
                }

                case Vector4:
                {
                    Vector4 accum = Vector4.Zero;
                    for (int i = 0; i < 32 * 32; ++i)
                    {
                        accum += (Vector4)this.SimulationPixels[i];
                    }

                    accum /= 1024f;
                    return $"({accum.X:0.000}, {accum.Y:0.000}, {accum.Z:0.000}, {accum.W:0.000})";
                }

                default:
                {
                    return "unknown";
                }
            }
        }

        public static NodeSimulationMatrix Parallel(NodeSimulationMatrix[] inputs, Func<object[], object> processor)
        {
            NodeSimulationMatrix ret = new NodeSimulationMatrix();
            object[][] raw = new object[32 * 32][];

            System.Threading.Tasks.Parallel.For(0, 32 * 32, i =>
            {
                raw[i] = new object[inputs.Length];
                for (int j = 0; j < inputs.Length; ++j)
                {
                    raw[i][j] = inputs[j].SimulationPixels[i];
                }

                ret.SimulationPixels[i] = processor(raw[i]);
            });

            return ret;
        }

        public static NodeSimulationMatrix SimulateScreenPositionMatrix()
        {
            NodeSimulationMatrix ret = new NodeSimulationMatrix();
            for (int i = 0; i < 32 * 32; ++i)
            {
                int x = i % 32;
                int y = i / 32;
                ret.SimulationPixels[i] = new Vector2(x, y);
            }

            return ret;
        }

        public static NodeSimulationMatrix SimulateUVMatrix()
        {
            NodeSimulationMatrix ret = new NodeSimulationMatrix();
            for (int i = 0; i < 32 * 32; ++i)
            {
                int x = i % 32;
                int y = i / 32;
                ret.SimulationPixels[i] = new Vector2(x / 32f, 1.0f - (y / 32f));
            }

            return ret;
        }
    }
}
