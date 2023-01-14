#version 330 core

in vec4 inst_color;
in vec4 f_color;
in vec2 f_texture;
flat in int f_frame;

uniform uint frame;
uniform uint update;

uniform vec4 m_diffuse_color;
uniform sampler2D m_texture_diffuse;
uniform vec4 m_diffuse_frame;
uniform float gamma_factor;

out vec4 g_color;

vec4 sampleMap()
{
	ivec2 twh = textureSize(m_texture_diffuse, 0);														// 32768 x 512
	ivec2 fwh = ivec2(int(m_diffuse_frame.z * twh.x), int(m_diffuse_frame.w * twh.y));					// 256 x 256
	vec2 fxy = vec2(float((f_frame * fwh.x) % twh.x), float(((f_frame * fwh.x) / twh.x) * fwh.y));	    // (256 * frame) % 32768 x [0~256]
	vec2 uv = (fxy / vec2(twh)) + (f_texture * m_diffuse_frame.zw);								
	return texture(m_texture_diffuse, uv);
}

void main()
{
	g_color = sampleMap() * f_color * inst_color;
	g_color.rgb = pow(g_color.rgb, vec3(1.0/gamma_factor));
}