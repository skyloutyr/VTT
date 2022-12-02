namespace VTT.Asset.Shader.NodeGraph
{
    using OpenTK.Mathematics;
    using System;

    public class Node
    {
        public NodeProcessor Proc { get; }

        public Guid NodeID { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Scale { get; set; }
        public string Name { get; set; }
        public bool Editable { get; set; }

        public NodeInput[] ConnectedIns { get; set; }
        public NodeOutput[] ConnectedOuts { get; set; }
    }

    public readonly struct NodeTemplate
    {
        public const string CategoryInputs = "ui.shadergraph.inputs";
        public const string CategoryMaterial = "ui.shadergraph.material";

        public string Name { get; }
        public string Category { get; }
        public NodeProcessor Proc { get; }
        public ParameterType[] Ins { get; }
        public ParameterType[] Outs { get; }

        public NodeTemplate(string name, string category, NodeProcessor proc, ParameterType[] ins, ParameterType[] outs)
        {
            this.Name = name;
            this.Category = category;
            this.Proc = proc;
            this.Ins = ins;
            this.Outs = outs;
        }
    }

    public static class NodeTemplates
    {
        public static NodeTemplate Input_Tangent { get; } = new NodeTemplate("ui.shadergraph.node.in_tangent", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return f_tangent;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec3 });
        public static NodeTemplate Input_Bitangent { get; } = new NodeTemplate("ui.shadergraph.node.in_bitangent", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return f_bitangent;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec3 });
        public static NodeTemplate Input_NormalVec { get; } = new NodeTemplate("ui.shadergraph.node.in_normalvec", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return f_normal;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec3 });
        public static NodeTemplate Input_ModelPosition { get; } = new NodeTemplate("ui.shadergraph.node.in_modelpos", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return f_position;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec3 });
        public static NodeTemplate Input_WorldPosition { get; } = new NodeTemplate("ui.shadergraph.node.in_worldpos", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return f_world_position;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec3 });
        public static NodeTemplate Input_ModelColor { get; } = new NodeTemplate("ui.shadergraph.node.in_modelclr", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return f_color;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec4 });
        public static NodeTemplate Input_UVs { get; } = new NodeTemplate("ui.shadergraph.node.in_uvs", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return f_texture;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec2 });
        public static NodeTemplate Input_Frame { get; } = new NodeTemplate("ui.shadergraph.node.in_frame", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return frame;" }, new ParameterType[0], new ParameterType[] { ParameterType.Integer });
        public static NodeTemplate Input_Update { get; } = new NodeTemplate("ui.shadergraph.node.in_update", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return update;" }, new ParameterType[0], new ParameterType[] { ParameterType.Integer });
        public static NodeTemplate Input_Alpha { get; } = new NodeTemplate("ui.shadergraph.node.in_opacity", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return alpha;" }, new ParameterType[0], new ParameterType[] { ParameterType.Float });
        public static NodeTemplate Input_CamPos { get; } = new NodeTemplate("ui.shadergraph.node.in_camerapos", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return camera_position;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec3 });
        public static NodeTemplate Input_TintColor { get; } = new NodeTemplate("ui.shadergraph.node.in_tintclr", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return tint_color;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec4 });
        public static NodeTemplate Input_Normal { get; } = new NodeTemplate("ui.shadergraph.node.in_worldnormal", NodeTemplate.CategoryInputs, new NodeProcessor() { CodeConverter = "return getNormalFromMap();" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec4 });

        public static NodeTemplate Material_DiffuseColor { get; } = new NodeTemplate("ui.shadergraph.node.mat_diffusefac", NodeTemplate.CategoryMaterial, new NodeProcessor() { CodeConverter = "return m_diffuse_color;" }, new ParameterType[0], new ParameterType[] { ParameterType.Vec4 });
        public static NodeTemplate Material_MetalFactor { get; } = new NodeTemplate("ui.shadergraph.node.mat_metalfac", NodeTemplate.CategoryMaterial, new NodeProcessor() { CodeConverter = "return m_metal_factor;" }, new ParameterType[0], new ParameterType[] { ParameterType.Float });
        public static NodeTemplate Material_RoughnessFactor { get; } = new NodeTemplate("ui.shadergraph.node.mat_roughfac", NodeTemplate.CategoryMaterial, new NodeProcessor() { CodeConverter = "return m_roughness_factor;" }, new ParameterType[0], new ParameterType[] { ParameterType.Float });
        public static NodeTemplate Material_AlbedoTexture { get; } = new NodeTemplate("ui.shadergraph.node.mat_albedotex", NodeTemplate.CategoryMaterial, new NodeProcessor() { CodeConverter = "return sampleMapCustom(m_texture_diffuse, ARG0, m_diffuse_frame);" }, new ParameterType[] { ParameterType.Vec2 }, new ParameterType[] { ParameterType.Vec4 });
        public static NodeTemplate Material_NormalTexture { get; } = new NodeTemplate("ui.shadergraph.node.mat_normaltex", NodeTemplate.CategoryMaterial, new NodeProcessor() { CodeConverter = "return sampleMapCustom(m_texture_normal, ARG0, m_normal_frame);" }, new ParameterType[] { ParameterType.Vec2 }, new ParameterType[] { ParameterType.Vec4 });
        public static NodeTemplate Material_EmissiveTexture { get; } = new NodeTemplate("ui.shadergraph.node.mat_emissiontex", NodeTemplate.CategoryMaterial, new NodeProcessor() { CodeConverter = "return sampleMapCustom(m_texture_emissive, ARG0, m_emissive_frame);" }, new ParameterType[] { ParameterType.Vec2 }, new ParameterType[] { ParameterType.Vec4 });
        public static NodeTemplate Material_AoTexture { get; } = new NodeTemplate("ui.shadergraph.node.mat_aotex", NodeTemplate.CategoryMaterial, new NodeProcessor() { CodeConverter = "return sampleMapCustom(m_texture_aomr, ARG0, m_aomr_frame).r;" }, new ParameterType[] { ParameterType.Vec2 }, new ParameterType[] { ParameterType.Float });
        public static NodeTemplate Material_MetallicTexture { get; } = new NodeTemplate("ui.shadergraph.node.mat_mtex", NodeTemplate.CategoryMaterial, new NodeProcessor() { CodeConverter = "return sampleMapCustom(m_texture_aomr, ARG0, m_aomr_frame).g;" }, new ParameterType[] { ParameterType.Vec2 }, new ParameterType[] { ParameterType.Float });
        public static NodeTemplate Material_RoughnessTexture { get; } = new NodeTemplate("ui.shadergraph.node.mat_rtex", NodeTemplate.CategoryMaterial, new NodeProcessor() { CodeConverter = "return sampleMapCustom(m_texture_aomr, ARG0, m_aomr_frame).b;" }, new ParameterType[] { ParameterType.Vec2 }, new ParameterType[] { ParameterType.Float });
    }

    public class NodeProcessor
    {
        public string CodeConverter { get; set; }
    }

    public class NodeInput
    {
        public ParameterType ParameterType { get; }
        public object UnconnectedValue { get; set; }
        public Guid ConnectedID { get; set; }
        public int ConnectedOutputIndex { get; set; }
    }

    public class NodeOutput
    {
        public ParameterType ParameterType { get; }
    }

    public static class ConversionHelper
    {
        public static string Convert(ParameterType from, ParameterType to)
        {
            switch (from)
            {
                case ParameterType.Integer:
                {
                    return to switch
                    {
                        ParameterType.Float => "float({0})",
                        ParameterType.Vec2 => "vec2({0}, {0})",
                        ParameterType.Vec3 => "vec3({0}, {0}, {0})",
                        ParameterType.Vec4 => "vec4({0}, {0}, {0}, {0})",
                        ParameterType.Boolean => "{0} != 0",
                        _ => "{0}"
                    };
                }

                case ParameterType.Float:
                {
                    return to switch
                    {
                        ParameterType.Integer => "int({0})",
                        ParameterType.Vec2 => "vec2({0}, {0})",
                        ParameterType.Vec3 => "vec3({0}, {0}, {0})",
                        ParameterType.Vec4 => "vec4({0}, {0}, {0}, {0})",
                        ParameterType.Boolean => "int({0}) != 0",
                        _ => "{0}"
                    };
                }

                case ParameterType.Vec2:
                {
                    return to switch
                    {
                        ParameterType.Integer => "int({0}.x)",
                        ParameterType.Float => "{0}.x",
                        ParameterType.Vec3 => "vec3({0}.x, {0}.y, 0)",
                        ParameterType.Vec4 => "vec4({0}.x, {0}.y, 0, 0)",
                        ParameterType.Boolean => "int({0}.x) != 0",
                        _ => "{0}"
                    };
                }

                case ParameterType.Vec3:
                {
                    return to switch
                    {
                        ParameterType.Integer => "int({0}.x)",
                        ParameterType.Float => "{0}.x",
                        ParameterType.Vec2 => "{0}.xy",
                        ParameterType.Vec4 => "vec4({0}.x, {0}.y, {0}.z, 0)",
                        ParameterType.Boolean => "int({0}.x) != 0",
                        _ => "{0}"
                    };
                }

                case ParameterType.Vec4:
                {
                    return to switch
                    {
                        ParameterType.Integer => "int({0}.x)",
                        ParameterType.Float => "{0}.x",
                        ParameterType.Vec2 => "{0}.xy",
                        ParameterType.Vec3 => "{0}.xyz",
                        ParameterType.Boolean => "int({0}.x) != 0",
                        _ => "{0}"
                    };
                }

                default:
                {
                    return "{0}";
                }
            }
        }
    }

    public enum ParameterType
    {
        Integer,
        Float,
        Vec2,
        Vec3,
        Vec4,
        Boolean
    }
}
