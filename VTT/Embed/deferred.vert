#version 330 core
layout (location = 0) in vec3 v_position;
layout (location = 1) in vec2 v_texture;
layout (location = 2) in vec3 v_normal;
layout (location = 3) in vec3 v_tangent;
layout (location = 4) in vec3 v_bitangent;
layout (location = 5) in vec4 v_color;
layout (location = 6) in vec4 v_weights;
layout (location = 7) in vec2 v_bones;

layout (std140) uniform FrameData {
	mat4 view;
	mat4 projection;
	mat4 sun_view;
	mat4 sun_projection;
	vec3 camera_position;
	vec3 camera_direction;
	vec3 dl_direction;
	vec3 dl_color;
	vec3 al_color;
	vec3 sky_color;
	vec3 cursor_position;
	vec4 grid_color;
	vec4 dv_data;
	uint frame;
	uint update;
	float grid_size;
    float frame_delta;
};

layout (std140) uniform BoneData {
	mat4 bones[256];
};

uniform mat4 model;
uniform mat4 mvp;
uniform bool is_animated;

out mat3 f_tbn;
out vec3 f_position;
out vec3 f_normal;
out vec3 f_tangent;
out vec3 f_bitangent;
out vec3 f_world_position;
out vec4 f_color;
out vec2 f_texture;

vec3 boneTransformPos(vec4 vec, uint i1, uint i2, uint i3, uint i4)
{
	ivec4 indices = ivec4(i1, i2, i3, i4);
	vec3 result = vec3(0.0);
	for (int i = 0; i < 4; ++i)
	{
		mat4 boneMat = bones[indices[i]];
		result += (v_weights[i] * boneMat * vec).xyz;
	}

	return result;
}

void main()
{
	vec3 t_tan = v_tangent;
	vec3 t_bitan = v_bitangent;
	vec3 t_normal = v_normal;
	vec3 t_pos = v_position;

	if (is_animated)
	{
		mat4 fullBoneMat = mat4(1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0);
		uint indices12 = floatBitsToUint(v_bones.x);
		uint indices34 = floatBitsToUint(v_bones.y);

		uint index1 = indices12 >> 16;
		uint index2 = indices12 & 0xffffu;
		uint index3 = indices34 >> 16;
		uint index4 = indices34 & 0xffffu;

		t_pos = boneTransformPos(vec4(t_pos, 1.0), index1, index2, index3, index4);
		t_tan = normalize(boneTransformPos(vec4(t_pos, 1.0), index1, index2, index3, index4));
		t_bitan = normalize(boneTransformPos(vec4(t_pos, 1.0), index1, index2, index3, index4));
		t_normal = normalize(boneTransformPos(vec4(t_pos, 1.0), index1, index2, index3, index4));
	}

	vec3 world_tan = normalize(vec3(model * vec4(t_tan, 0.0)));
	vec3 world_bitan = normalize(vec3(model * vec4(t_bitan, 0.0)));
	vec3 world_normal = normalize(vec3(model * vec4(t_normal, 0.0)));
	f_tbn = mat3(world_tan, world_bitan, world_normal);
	f_position = t_pos;
	f_world_position = vec3(model * vec4(t_pos, 1.0));
	f_color = v_color;
	f_texture = v_texture;
	f_normal = t_normal;
	f_tangent = t_tan;
	f_bitangent = t_bitan;
	gl_Position = mvp * vec4(t_pos, 1.0);
}