#version 330 core
layout (location = 0) in vec3 v_position;
layout (location = 1) in vec2 v_texture;
layout (location = 2) in vec3 v_normal;
layout (location = 3) in vec3 v_tangent;
layout (location = 4) in vec3 v_bitangent;
layout (location = 5) in vec4 v_color;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 model;

uniform uint frame;
uniform uint update;

uniform samplerBuffer dataBuffer;

// FOW
uniform usampler2D fow_texture;
uniform vec2 fow_offset;
uniform vec2 fow_scale;
uniform float fow_mod;
uniform bool billboard;

out vec4 f_color;
out vec2 f_texture;
out vec4 inst_color;
flat out int f_frame;

vec4 decodeMColor(float cData)
{
	uint ui = floatBitsToUint(cData);
	float r = float((ui & 0xff000000u) >> 24) / 255.0;
	float g = float((ui & 0x00ff0000u) >> 16) / 255.0;
	float b = float((ui & 0x0000ff00u) >> 8) / 255.0;
	float a = float((ui & 0x000000ffu)) / 255.0;
	return vec4(r, g, b, a);
}

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

void main()
{
	int idx = gl_InstanceID * 2;
	vec3 v0 = texelFetch(dataBuffer, idx + 0).xyz;
	vec3 v1 = texelFetch(dataBuffer, idx + 1).xyz;
	float inst_x = v0.x;
	float inst_y = v0.y;
	float inst_z = v0.z;
	float inst_w = v1.x;
	float inst_clr = v1.y;
	float inst_frame = v1.z;
	inst_color = decodeMColor(inst_clr);
	vec4 viewPos = view * model * (billboard ? vec4(inst_x, inst_y, inst_z, 1.0) : vec4((v_position * inst_w) + vec3(inst_x, inst_y, inst_z), 1.0));
	f_color = v_color;
	f_texture = v_texture;
	f_frame = int(floatBitsToUint(inst_frame));
	float fow_mul = mix(getFowMultiplier(vec3(inst_x, inst_y, inst_z)), 1.0, 1.0 - fow_mod);
	gl_Position = (inst_w < 0.001 || fow_mul <= 0.001) ? vec4(0.0, 0.0, 0.0, -1.0) : projection * (viewPos + vec4((billboard ? v_position * inst_w : vec3(0.0, 0.0, 0.0)), 0.0));
}