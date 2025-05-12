namespace VTT.Asset.Shader.NodeGraph
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
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
            this.NodeID = e.GetGuidLegacy("ID");
            this.Name = e.GetString("Name");
            this.Location = e.GetVec2Legacy("Location");
            this.Size = e.GetVec2Legacy("Size");
            this.Deletable = e.GetBool("Deletable");
            this.TemplateID = e.GetGuidLegacy("Template");
            this.Inputs = new List<NodeInput>(e.GetArray("Ins", (n, c) => { NodeInput i = new NodeInput(); i.Deserialize(c.GetMap(n)); return i; }, Array.Empty<NodeInput>()));
            this.Outputs = new List<NodeOutput>(e.GetArray("Outs", (n, c) => { NodeOutput i = new NodeOutput(); i.Deserialize(c.GetMap(n)); return i; }, Array.Empty<NodeOutput>()));
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("ID", this.NodeID);
            ret.SetString("Name", this.Name);
            ret.SetVec2("Location", this.Location);
            ret.SetVec2("Size", this.Size);
            ret.SetBool("Deletable", this.Deletable);
            ret.SetGuid("Template", this.TemplateID);
            ret.SetArray("Ins", this.Inputs.ToArray(), (n, c, v) => c.SetMap(n, v.Serialize()));
            ret.SetArray("Outs", this.Outputs.ToArray(), (n, c, v) => c.SetMap(n, v.Serialize()));
            return ret;
        }

        public NodeSimulationMatrix SimulateProcess(SimulationContext ctx, ShaderGraph container, int index, List<ShaderNode> recursionProtection)
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
                NodeSimulationMatrix o;
                if (!ni.ConnectedOutput.Equals(Guid.Empty) && container.AllOutputsById.TryGetValue(ni.ConnectedOutput, out (ShaderNode, NodeOutput) data))
                {
                    o = data.Item1.SimulateProcess(ctx, container, data.Item1.Outputs.IndexOf(data.Item2), recursionProtection);
                    o = container.SimulationConvert(data.Item2.SelfType, ni.SelfType, o);
                }
                else
                {
                    o = container.SimulationConvert(ni.SelfType, ni.SelfType, new NodeSimulationMatrix(ctx, ni.CurrentValue));
                }

                ins[i] = o;
            }

            recursionProtection.Remove(this);
            return ShaderNodeTemplate.TemplatesByID.TryGetValue(this.TemplateID, out ShaderNodeTemplate val)
                ? val.Simulator(ctx, ins, index)
                : throw new Exception("Invalid shader node template!");
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
        public int Width { get; }
        public int Height { get; }

        public NodeSimulationMatrix(SimulationContext ctx)
        {
            this.Width = ctx.Width;
            this.Height = ctx.Height;
            this.SimulationPixels = new object[this.Width * this.Height];
        }

        public NodeSimulationMatrix(SimulationContext ctx, NodeSimulationMatrix val) : this(ctx)
        {
            for (int i = 0; i < this.Width * this.Height; ++i)
            {
                this.SimulationPixels[i] = val.SimulationPixels[i];
            }
        }

        public NodeSimulationMatrix(SimulationContext ctx, object o) : this(ctx)
        {
            for (int i = 0; i < this.Width * this.Height; ++i)
            {
                this.SimulationPixels[i] = o;
            }
        }

        public NodeSimulationMatrix Cast<T1, T>(Func<T1, T> processor)
        {
            for (int i = 0; i < this.Width * this.Height; ++i)
            {
                this.SimulationPixels[i] = processor((T1)this.SimulationPixels[i]);
            }

            return this;
        }

        public NodeSimulationMatrix CastParallel<T1, T>(Func<T1, T> processor)
        {
            System.Threading.Tasks.Parallel.For(0, this.Width * this.Height, i => this.SimulationPixels[i] = processor((T1)this.SimulationPixels[i]));
            return this;
        }

        public NodeSimulationMatrix(SimulationContext ctx, Image<Rgba32> image) : this(ctx)
        {
            for (int i = 0; i < this.Width * this.Height; ++i)
            {
                int x = i % this.Width;
                int y = i / this.Height;
                this.SimulationPixels[i] = this.GetPixel(image, x, y);
            }
        }

        private Vector4 GetPixel(Image<Rgba32> img, int x, int y)
        {
            if (img.Width != this.Width)
            {
                float factor = (float)img.Width / this.Width;
                x = (int)MathF.Floor(x * factor);
            }

            if (img.Height != this.Height)
            {
                float factor = (float)img.Height / this.Height;
                y = (int)MathF.Floor(y * factor);
            }

            return img[x, y].ToScaledVector4();
        }

        public NodeSimulationMatrix(SimulationContext ctx, Image<Rgba32> image, Vector2 coordinateOffsets) : this(ctx)
        {
            for (int i = 0; i < this.Width * this.Height; ++i)
            {
                int x = i % this.Width;
                int y = i / this.Height;
                x += (int)(coordinateOffsets.X * this.Width);
                y += (int)(coordinateOffsets.Y * this.Height);
                if (x < 0)
                {
                    x = Math.Abs(x);
                }

                if (y < 0)
                {
                    y = Math.Abs(y);
                }

                x %= this.Width;
                y %= this.Height;
                this.SimulationPixels[i] = this.GetPixel(image, x, y);
            }
        }

        public NodeSimulationMatrix(SimulationContext ctx, Image<Rgba32> image, NodeSimulationMatrix offsetsMatrix) : this(ctx)
        {
            for (int i = 0; i < this.Width * this.Height; ++i)
            {
                int x = 0;
                int y = 0;
                Vector2 coordinateOffsets = (Vector2)offsetsMatrix.SimulationPixels[i];
                x += (int)(coordinateOffsets.X * this.Width);
                y += (int)(coordinateOffsets.Y * this.Height);
                if (x < 0)
                {
                    x = Math.Abs(x);
                }

                if (y < 0)
                {
                    y = Math.Abs(y);
                }

                x %= this.Width;
                y %= this.Height;
                this.SimulationPixels[i] = this.GetPixel(image, x, y);
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

        public static NodeSimulationMatrix Parallel(SimulationContext ctx, NodeSimulationMatrix[] inputs, Func<object[], object> processor)
        {
            NodeSimulationMatrix ret = new NodeSimulationMatrix(ctx);
            object[][] raw = new object[ctx.Width * ctx.Height][];

            System.Threading.Tasks.Parallel.For(0, ctx.Width * ctx.Height, i =>
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

        public static NodeSimulationMatrix SimulateScreenPositionMatrix(SimulationContext ctx)
        {
            NodeSimulationMatrix ret = new NodeSimulationMatrix(ctx);
            for (int i = 0; i < ctx.Width * ctx.Height; ++i)
            {
                int x = i % ctx.Width;
                int y = i / ctx.Height;
                ret.SimulationPixels[i] = new Vector3(x, y, 0);
            }

            return ret;
        }

        public static NodeSimulationMatrix SimulateUVMatrix(SimulationContext ctx)
        {
            NodeSimulationMatrix ret = new NodeSimulationMatrix(ctx);
            for (int i = 0; i < ctx.Width * ctx.Height; ++i)
            {
                int x = i % ctx.Width;
                int y = i / ctx.Height;
                ret.SimulationPixels[i] = new Vector2((float)x / ctx.Width, 1.0f - ((float)y / ctx.Height));
            }

            return ret;
        }
    }

    public class SimulationContext
    {
        public int Width { get; }
        public int Height { get; }

        public Image<Rgba32> Albedo { get; }
        public Image<Rgba32> Normal { get; }
        public Image<Rgba32> AOMR { get; }

        public SimulationContext(int width, int height, Image<Rgba32> albedo, Image<Rgba32> normal, Image<Rgba32> aOMR)
        {
            this.Width = width;
            this.Height = height;
            this.Albedo = albedo;
            this.Normal = normal;
            this.AOMR = aOMR;
        }

        public NodeSimulationMatrix Parallel(NodeSimulationMatrix[] inputs, Func<object[], object> processor) => NodeSimulationMatrix.Parallel(this, inputs, processor);
        public NodeSimulationMatrix CreateMatrix() => new NodeSimulationMatrix(this);
        public NodeSimulationMatrix CreateMatrix(NodeSimulationMatrix from) => new NodeSimulationMatrix(this, from);
        public NodeSimulationMatrix CreateMatrix(Image<Rgba32> img) => new NodeSimulationMatrix(this, img);
        public NodeSimulationMatrix CreateMatrix(float f) => new NodeSimulationMatrix(this, f);
        public NodeSimulationMatrix CreateMatrix(Vector2 f) => new NodeSimulationMatrix(this, f);
        public NodeSimulationMatrix CreateMatrix(Vector3 f) => new NodeSimulationMatrix(this, f);
        public NodeSimulationMatrix CreateMatrix(Vector4 f) => new NodeSimulationMatrix(this, f);
        public NodeSimulationMatrix CreateMatrix(uint ui) => new NodeSimulationMatrix(this, ui);
        public NodeSimulationMatrix CreateMatrix(int i) => new NodeSimulationMatrix(this, i);
        public NodeSimulationMatrix CreateAlbedo() => new NodeSimulationMatrix(this, this.Albedo);
        public NodeSimulationMatrix CreateAlbedo(Vector2 offset) => new NodeSimulationMatrix(this, this.Albedo, offset);
        public NodeSimulationMatrix CreateAlbedo(NodeSimulationMatrix offset) => new NodeSimulationMatrix(this, this.Albedo, offset);
        public NodeSimulationMatrix CreateNormal() => new NodeSimulationMatrix(this, this.Normal);
        public NodeSimulationMatrix CreateNormal(Vector2 offset) => new NodeSimulationMatrix(this, this.Normal, offset);
        public NodeSimulationMatrix CreateNormal(NodeSimulationMatrix offset) => new NodeSimulationMatrix(this, this.Normal, offset);
        public NodeSimulationMatrix CreateAOMR() => new NodeSimulationMatrix(this, this.AOMR);
        public NodeSimulationMatrix CreateAOMR(Vector2 offset) => new NodeSimulationMatrix(this, this.AOMR, offset);
        public NodeSimulationMatrix CreateAOMR(NodeSimulationMatrix offset) => new NodeSimulationMatrix(this, this.AOMR, offset);
        public NodeSimulationMatrix CreateCustomImage(Image<Rgba32> img) => new NodeSimulationMatrix(this, img);
        public NodeSimulationMatrix CreateCustomImage(Image<Rgba32> img, Vector2 offset) => new NodeSimulationMatrix(this, img, offset);
        public NodeSimulationMatrix CreateCustomImage(Image<Rgba32> img, NodeSimulationMatrix offset) => new NodeSimulationMatrix(this, img, offset);
        public NodeSimulationMatrix CreateScreenPositions() => NodeSimulationMatrix.SimulateScreenPositionMatrix(this);
        public NodeSimulationMatrix CreateViewportSize() => this.CreateMatrix(new Vector2(this.Width, this.Height));
        public NodeSimulationMatrix CreateUVs() => NodeSimulationMatrix.SimulateUVMatrix(this);
        public NodeSimulationMatrix Cast<T1, T>(NodeSimulationMatrix matrix, Func<T1, T> converter) => matrix.Cast(converter);
        public NodeSimulationMatrix ParallelCast<T1, T>(NodeSimulationMatrix matrix, Func<T1, T> converter) => matrix.CastParallel(converter);

        public void Free()
        {
            this.Albedo.Dispose();
            this.Normal.Dispose();
            this.AOMR.Dispose();
        }
    }
}
