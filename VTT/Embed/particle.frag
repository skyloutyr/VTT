#version 330 core

in vec4 inst_color;
in vec4 f_color;
in vec2 f_texture;
in vec3 inst_pos;
flat in int f_frame;

uniform uint frame;
uniform uint update;

uniform vec4 m_diffuse_color;
uniform sampler2D m_texture_diffuse;
uniform vec4 m_diffuse_frame;
uniform float gamma_factor;

// FOW
uniform usampler2D fow_texture;
uniform vec2 fow_offset;
uniform vec2 fow_scale;
uniform float fow_mod;
uniform bool do_fow;
uniform vec3 sky_color;

out vec4 g_color;

float getFowMultiplier(vec3 f_world_position)
{   
    vec2 uv_fow_world = (f_world_position.xy + fow_offset) * fow_scale;
    vec2 fow_world = (f_world_position.xy + fow_offset);

    uvec4 data = texture(fow_texture, uv_fow_world);
    float yIdx = fract(fow_world.y);

    float mulR = float(yIdx <= 0.25);
    float mulG = float(yIdx <= 0.5 && yIdx > 0.25);
    float mulB = float(yIdx <= 0.75 && yIdx > 0.5);
    float mulA = float(yIdx > 0.75);

    uint bitOffsetY = 8u * uint(round(mod(yIdx * 4, 1)));
    uint bitOffsetX = uint(fract(fow_world.x) * 8);

    uint mask = (1u << bitOffsetY) << bitOffsetX;

    float r = min(1, float(data.r & mask) * mulR);
    float g = min(1, float(data.g & mask) * mulG);
    float b = min(1, float(data.b & mask) * mulB);
    float a = min(1, float(data.a & mask) * mulA);

    return r + g + b + a;
}

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
    if (do_fow)
    {
        float fowVal = getFowMultiplier(inst_pos) * fow_mod + (1.0 - fow_mod);
        g_color = mix(vec4(sky_color, g_color.a), g_color, fowVal);
    }
	
}