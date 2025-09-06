#version 330 core

in vec4 f_color;
in vec2 f_texture;

layout (std140) uniform Material
{
    vec4 albedo_metal_roughness_alpha_cutoff; // diffuse_color = unpackRgba(x), metalness = y, roughness = z, alpha_cutoff = w
    vec4 m_diffuse_frame;
    vec4 m_normal_frame;
    vec4 m_emissive_frame;
    vec4 m_aomr_frame;
    vec4 m_index_padding; // index = reinterpretcast<uint>(x), yzw padded
};

uniform vec4 u_color;
uniform sampler2D m_texture_diffuse; // binding 0

layout (location = 0) out vec4 o_color;

const float oneOver255 = 1.0 / 255.0;
vec4 unpackRgba(float packedRgbaVal)
{
    uint packed_ui = floatBitsToUint(packedRgbaVal);
    return vec4(
        float((packed_ui >> 24) & 0xffu),
        float((packed_ui >> 16) & 0xffu),
        float((packed_ui >> 8) & 0xffu),
        float(packed_ui & 0xffu)
    ) * oneOver255;
}

vec4 sampleMapCustom(sampler2D sampler, vec2 uvs, vec4 frameData)
{
    return texture(sampler, uvs * frameData.zw + frameData.xy);
}

vec4 sampleMap(sampler2D sampler, vec4 frameData)
{
    return sampleMapCustom(sampler, f_texture, frameData);
}

void main()
{
	o_color = sampleMap(m_texture_diffuse, m_diffuse_frame) * unpackRgba(albedo_metal_roughness_alpha_cutoff.x) * u_color;
}