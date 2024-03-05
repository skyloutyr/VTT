namespace VTT.Asset.Shader.NodeGraph
{
    using System;
    using System.Numerics;
    using VTT.Util;

    public class NodeInput : ISerializable
    {
        public Guid ID { get; set; }
        public string Name { get; set; }
        public NodeValueType SelfType { get; set; }
        public Guid ConnectedOutput { get; set; }
        public object CurrentValue { get; set; }

        public NodeInput ValidateValue()
        {
            switch (this.SelfType)
            {
                case NodeValueType.Bool:
                {
                    if (this.CurrentValue is not bool)
                    {
                        this.CurrentValue = false;
                    }

                    break;
                }

                case NodeValueType.Int:
                {
                    if (this.CurrentValue is not int)
                    {
                        this.CurrentValue = 0;
                    }

                    break;
                }

                case NodeValueType.UInt:
                {
                    if (this.CurrentValue is not uint)
                    {
                        this.CurrentValue = 0u;
                    }

                    break;
                }

                case NodeValueType.Float:
                {
                    if (this.CurrentValue is not float)
                    {
                        this.CurrentValue = 0f;
                    }

                    break;
                }

                case NodeValueType.Vec2:
                {
                    if (this.CurrentValue is not Vector2)
                    {
                        this.CurrentValue = Vector2.Zero;
                    }

                    break;
                }

                case NodeValueType.Vec3:
                {
                    if (this.CurrentValue is not Vector3)
                    {
                        this.CurrentValue = Vector3.Zero;
                    }

                    break;
                }

                case NodeValueType.Vec4:
                {
                    if (this.CurrentValue is not Vector4)
                    {
                        this.CurrentValue = Vector4.Zero;
                    }

                    break;
                }
            }

            return this;
        }

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
}
