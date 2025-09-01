#version 330 core

in vec2 f_texture;

layout (location = 0) out vec4 f_color;

layout (std140) uniform Material
{
    vec4 albedo_metal_roughness_alpha_cutoff; // diffuse_color = unpackRgba(x), metalness = y, roughness = z, alpha_cutoff = w
    vec4 m_diffuse_frame;
    vec4 m_normal_frame;
    vec4 m_emissive_frame;
    vec4 m_aomr_frame;
    vec4 m_index_padding; // index = reinterpretcast<uint>(x), yzw padded
};

uniform sampler2D m_texture_diffuse;

uniform float gamma_factor;

const vec3 grayscale_factors = vec3(0.299, 0.587, 0.114);
const vec3 royal_blue = vec3(65.0 / 255.0, 105.0 / 255.0, 225.0 / 255.0);
const float stripe_interval = 16.0;

vec4 sampleMap(sampler2D sampler, vec4 frameData)
{
	return texture(sampler, f_texture * frameData.zw + frameData.xy);
}

void main()
{
	vec4 clr_orig = sampleMap(m_texture_diffuse, m_diffuse_frame);
	float g = dot(clr_orig.rgb, grayscale_factors);
	float scanline_contribution = step(mod(-gl_FragCoord.x + gl_FragCoord.y, stripe_interval) / (stripe_interval - 1.0), 0.5);
	f_color = vec4(vec3(g) * royal_blue, clr_orig.a * (0.5 + (0.25 * scanline_contribution)));
    f_color.rgb = pow(f_color.rgb, vec3(1.0/gamma_factor));
}