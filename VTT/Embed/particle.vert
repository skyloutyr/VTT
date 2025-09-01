#version 330 core

#ifdef USE_VTX_COMPRESSION
layout (location = 0) in vec4 v_position_padding;
layout (location = 1) in vec4 v_texture_normal_tangent;
layout (location = 2) in vec4 v_bones_weights_color;
#else
layout (location = 0) in vec3 v_position; // Has to be a vec3f, can't do compression
layout (location = 1) in vec2 v_texture; // Could be compressed to 2x16bit component vector, but if we want to support texture ranges outside of 0-1, then must be a vec2f
layout (location = 2) in vec3 v_normal; // Could be compressed to a int_14_14_4, if a normalize() is an acceptable instruction
layout (location = 3) in vec3 v_tangent; // Could be compressed to a int_14_14_4, if a normalize() is an acceptable instruction
layout (location = 4) in vec3 v_bitangent; // Can be entirely omitted if normalize(cross(normal, tangent)) is acceptable
layout (location = 5) in vec4 v_color; // Can be compressed to a int8_8_8_8
layout (location = 6) in vec4 v_weights; // Depending on the precision desired can be compressed to a int10_10_10_2?
layout (location = 7) in vec2 v_bones; // Already compressed
#endif

layout (std140) uniform FrameData {
    mat4 view; // due to the particle shader demanding view separately, the view and projection matrices can't be united together here
    mat4 projection;
    mat4 sun_matrix;
    vec4 camera_position_sundir; // camera_position = xyz, sundir = unpackNorm101010(w)
    vec4 camera_direction_sunclr; // camera_direction = xyz, sunclr = unpackRgba(w)
    vec4 al_sky_colors_viewportsz; // al_color = unpackRgba(x), sky_color = unpackRgba(y), viewportsz = zw
    vec4 cursor_position_gridclr; // cursor_position = xyz, grid_color = unpackRgba(w)
    vec4 frame_update_updatedt_gridsz; // frame = reinterpretcast<uint>(x), update = reinterpretcast<uint>(y), updatedt = z, gridsz = w
    vec4 skybox_colors_blend_pl_num; // color_day = unpackRgba(x), color_night = unpackRgba(y), blend = z, pl_num = reinterpretcast<uint>(w)
    vec4 skybox_animation_day; // full frame data
    vec4 skybox_animation_night; // full frame data
    mat4 sunCascadeMatrices[5];
    vec4 cascadePlaneDistances;
    vec4 pl_positions_color[16]; // pl_position = [i].xyz, pl_color = unpackRgba([i].w)
    vec4 pl_cutouts[4]; // pl_cutout = abs([i >> 2][i & 3]), pl_casts_shadows = sign(pl_cutout) > epsilon
    vec4 fow_scale_mod; // fow_scale = vec2(x), fow_offset = vec2(0.5) + floor(fow_scale * 0.5), fow_mod = y, zw unused
};

uniform mat4 model;

uniform uint frame;
uniform uint update;

uniform samplerBuffer dataBuffer;

// FOW
uniform usampler2D fow_texture;
uniform bool billboard;
uniform bool do_fow;

uniform bool is_sprite_sheet;
uniform vec2 sprite_sheet_data;

out vec4 f_color;
out vec2 f_texture;
out vec4 inst_color;
out vec3 f_world_position;
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
    vec2 fow_scale = vec2(fow_scale_mod.x);
    vec2 fow_offset = vec2(0.5) + floor(fow_scale * 0.5);
    vec2 uv_fow_world = (f_world_position.xy + fow_offset) / fow_scale;
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
#ifdef USE_VTX_COMPRESSION
	vec2 v = vec2(
		stepX * float(iX) + stepX * v_texture_normal_tangent.x, 
		stepY * float(iY) + stepY * v_texture_normal_tangent.y
	);
#else
	vec2 v = vec2(
		stepX * float(iX) + stepX * v_texture.x, 
		stepY * float(iY) + stepY * v_texture.y
	);
#endif

	return v;
}

void main()
{
#ifdef USE_VTX_COMPRESSION
	vec3 v_position = v_position_padding.xyz;
	vec4 v_color = decodeMColor(v_bones_weights_color.w);
	vec2 v_texture = v_texture_normal_tangent.xy;
#endif
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
    float fow_mod = fow_scale_mod.y;
	float fow_mul = do_fow ? 1.0 : mix(getFowMultiplier(vec3(inst_x, inst_y, inst_z)), 1.0, 1.0 - fow_mod);
	gl_Position = (inst_w < 0.001 || fow_mul <= 0.001) ? vec4(0.0, 0.0, 0.0, -1.0) : projection * (viewPos + vec4((billboard ? v_position * inst_w : vec3(0.0, 0.0, 0.0)), 0.0));
}