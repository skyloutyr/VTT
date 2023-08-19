namespace VTT.Asset.Shader.NodeGraph
{
    using OpenTK.Mathematics;
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
}
