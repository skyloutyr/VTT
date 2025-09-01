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

layout (std140) uniform BoneData {
	mat4 bones[256];
};

uniform mat4 model;
uniform mat4 mvp;
uniform bool is_animated;

out mat3 f_tbn;
out vec3 f_world_position;
out vec4 f_color;
out vec2 f_texture;

vec4 unpackRgba(float packedRgbaVal)
{
	const float oneOver255 = 1.0 / 255.0;
    uint packed_ui = floatBitsToUint(packedRgbaVal);
    return vec4(
        float((packed_ui >> 24) & 0xffu),
        float((packed_ui >> 16) & 0xffu),
        float((packed_ui >> 8) & 0xffu),
        float(packed_ui & 0xffu)
    ) * oneOver255;
}

// For decoding normals/tangents in attributes
vec4 unpackVec14144Norm(float num)
{
	const float oneOver16383 = 1.0 / 16383.0;
	uint val = floatBitsToUint(num);
	float x = (float((val >> 18u) & 0x3fffu) * oneOver16383) * ((val & 8u) == 8u ? -1.0 : 1.0);
	float y = (float((val >> 4u) & 0x3fffu) * oneOver16383) * ((val & 4u) == 4u ? -1.0 : 1.0);
	vec3 v3 = normalize(vec3(x, y, sqrt(max(0, 1.0 - (x * x + y * y))) * ((val & 2u) == 2u ? -1.0 : 1.0)));
	return vec4(v3, (val & 1u) == 1u ? -1.0 : 1.0);
}

vec4 unpackVec101010Weights(float num)
{
	const float oneOver1023 = 1.0 / 1023.0;
	uint val = floatBitsToUint(num);
	float v1 = float((val >> 22u) & 0x3ffu) * oneOver1023;
	float v2 = float((val >> 12u) & 0x3ffu) * oneOver1023;
	float v3 = float((val >> 2u) & 0x3ffu) * oneOver1023;
	return vec4(v1, v2, v3, 1.0 - (v1 + v2 + v3));
}

void main()
{
#ifdef USE_VTX_COMPRESSION
	vec3 t_pos = v_position_padding.xyz;
	vec2 v_texture = v_texture_normal_tangent.xy;
	vec2 v_bones = v_bones_weights_color.xy;
	vec4 v_weights = unpackVec101010Weights(v_bones_weights_color.z);
	vec3 t_normal = unpackVec14144Norm(v_texture_normal_tangent.z).xyz;
	vec4 tbhand = unpackVec14144Norm(v_texture_normal_tangent.w);
	vec3 t_tan = tbhand.xyz;
	vec3 t_bitan = cross(t_normal, t_tan) * tbhand.w;
	vec4 v_color = unpackRgba(v_bones_weights_color.w);
#else
	vec3 t_tan = v_tangent;
	vec3 t_bitan = v_bitangent;
	vec3 t_normal = v_normal;
	vec3 t_pos = v_position;
#endif

	if (is_animated)
	{
		mat4 fullBoneMat = mat4(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
		uint indices12 = floatBitsToUint(v_bones.x);
		uint indices34 = floatBitsToUint(v_bones.y);
		uint index1 = indices12 >> 16;
		uint index2 = indices12 & 0xffffu;
		uint index3 = indices34 >> 16;
		uint index4 = indices34 & 0xffffu;
		ivec4 indices = ivec4(index1, index2, index3, index4);
		for (int i = 0; i < 4; ++i)
		{
			fullBoneMat += (v_weights[i] * bones[indices[i]]);
		}

		mat4 tInvNMat = transpose(inverse(fullBoneMat));
		t_pos = (fullBoneMat * vec4(t_pos, 1.0)).xyz;
		t_tan = (tInvNMat * vec4(t_tan, 1.0)).xyz;
		t_bitan = (tInvNMat * vec4(t_bitan, 1.0)).xyz;
		t_normal = (tInvNMat * vec4(t_normal, 1.0)).xyz;
	}

	vec3 world_tan = normalize(vec3(model * vec4(t_tan, 0.0)));
	vec3 world_bitan = normalize(vec3(model * vec4(t_bitan, 0.0)));
	vec3 world_normal = normalize(vec3(model * vec4(t_normal, 0.0)));
	f_tbn = mat3(world_tan, world_bitan, world_normal);
	f_world_position = vec3(model * vec4(t_pos, 1.0));
	f_color = v_color;
	f_texture = v_texture;
	gl_Position = mvp * vec4(t_pos, 1.0);
}