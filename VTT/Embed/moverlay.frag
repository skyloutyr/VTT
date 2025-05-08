#version 330 core

in vec4 f_world_position;

uniform vec4 u_color;

out vec4 o_color;

// FOW
uniform usampler2D fow_texture;
uniform vec2 fow_offset;
uniform vec2 fow_scale;
uniform float fow_mod;
uniform bool do_fow;
uniform vec3 sky_color;

float getFowMultiplier(vec3 world_position)
{   
    vec2 uv_fow_world = (world_position.xy + fow_offset) * fow_scale;
    vec2 fow_world = (world_position.xy + fow_offset);

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

void main()
{
	o_color = u_color;
    if (do_fow)
    {
        float fowVal = getFowMultiplier(f_world_position.xyz) * fow_mod + (1.0 - fow_mod);
        o_color = mix(vec4(sky_color, o_color.a), o_color, fowVal);
    }
}