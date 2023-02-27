namespace VTT.Asset.Shader.NodeGraph
{
    using System;
    using VTT.Util;

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
