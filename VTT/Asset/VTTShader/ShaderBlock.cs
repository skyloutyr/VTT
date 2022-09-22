namespace VTT.Asset.VTTShader
{
    using System;

    public class ShaderBlock
    {
        public Guid BlockID { get; set; }
    }

    public struct ShaderBlockTemplate
    {
        public string LangKey { get; }
        public AttachmentPoint[] Inputs { get; }
        public AttachmentPoint[] Outputs { get; }
        public string Code { get; }

        public ShaderBlockTemplate(string langKey, AttachmentPoint[] inputs, AttachmentPoint[] outputs, string code)
        {
            this.LangKey = langKey;
            this.Inputs = inputs;
            this.Outputs = outputs;
            this.Code = code;
        }

        public enum Category
        {
            Inputs,
            ImageProcessing,
            Math,
            VectorMath,
        }
    }

    public readonly struct AttachmentPoint
    {
        public string VarName { get; }
        public ShaderFieldType FieldType { get; }
        public object DefaultValue { get; }

        public AttachmentPoint(string varName, ShaderFieldType fieldType, object val)
        {
            this.VarName = varName;
            this.FieldType = fieldType;
            this.DefaultValue = val;
        }

        public string Convert(ShaderFieldType from, ShaderFieldType to, string vNameOrValue)
        {
            string n = Enum.GetName(to).ToLower();
            return $"{n} {this.VarName} = {ValueConverter.GetFor(from, to).Convert(vNameOrValue)}";
        }
    }
}
