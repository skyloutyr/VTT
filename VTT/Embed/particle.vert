#version 330 core
layout (location = 0) in vec3 v_position;
layout (location = 1) in vec2 v_texture;
layout (location = 2) in vec3 v_normal;
layout (location = 3) in vec3 v_tangent;
layout (location = 4) in vec3 v_bitangent;
layout (location = 5) in vec4 v_color;
layout (location = 6) in vec4 v_weights;
layout (location = 7) in vec2 v_bones;

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
uniform bool do_fow;
uniform bool is_sprite_sheet;
uniform vec2 sprite_sheet_data;

out vec4 f_color;
out vec2 f_texture;
out vec4 inst_color;
out vec3 f_world_position;
out vec3 f_position;
flat out int f_frame;
flat out int inst_id;
flat out float inst_lifespan;

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

vec2 decodeSpriteSheetCoordinates(float f)
{
	int ssIndex = floatBitsToInt(f);
	int iX = ssIndex % int(sprite_sheet_data.x);
	int iY = ssIndex / int(sprite_sheet_data.x);
	float stepX = 1.0 / sprite_sheet_data.x;
	float stepY = 1.0 / sprite_sheet_data.y;
	vec2 v = vec2(
		stepX * float(iX) + stepX * v_texture.x, 
		stepY * float(iY) + stepY * v_texture.y
	);

	return v;
}

void main()
{
	inst_id = gl_InstanceID;
	int idx = gl_InstanceID * 2;
	vec4 v0 = texelFetch(dataBuffer, idx + 0);
	vec4 v1 = texelFetch(dataBuffer, idx + 1);
	float inst_x = v0.x;
	float inst_y = v0.y;
	float inst_z = v0.z;
	float inst_w = v0.w;
	float inst_clr = v1.x;
	float inst_frame = v1.y;
	inst_lifespan = v1.w;
	inst_color = decodeMColor(inst_clr);
	vec4 worldPos = model * (billboard ? vec4(inst_x, inst_y, inst_z, 1.0) : vec4((v_position * inst_w) + vec3(inst_x, inst_y, inst_z), 1.0));
	vec4 viewPos = view * worldPos;
	f_world_position = (worldPos + vec4(billboard ? v_position * inst_w : vec3(0.0, 0.0, 0.0), 0.0)).xyz;
	f_color = v_color;
	f_texture = is_sprite_sheet ? decodeSpriteSheetCoordinates(v1.z) : v_texture;
	f_frame = int(floatBitsToUint(inst_frame));
	f_position = v_position;
	float fow_mul = do_fow ? 1.0 : mix(getFowMultiplier(vec3(inst_x, inst_y, inst_z)), 1.0, 1.0 - fow_mod);
	gl_Position = (inst_w < 0.001 || fow_mul <= 0.001) ? vec4(0.0, 0.0, 0.0, -1.0) : projection * (viewPos + vec4((billboard ? v_position * inst_w : vec3(0.0, 0.0, 0.0)), 0.0));
}