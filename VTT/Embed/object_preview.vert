#version 330 core

layout (location = 0) in vec3 v_position;
layout (location = 1) in vec2 v_texture;
layout (location = 2) in vec3 v_normal;
layout (location = 3) in vec3 v_tangent;
layout (location = 4) in vec3 v_bitangent;
layout (location = 5) in vec4 v_color;
layout (location = 6) in vec4 v_weights;
layout (location = 7) in vec2 v_bones;

layout (std140) uniform BoneData {
	mat4 bones[256];
};

// Camera
uniform mat4 view;
uniform mat4 projection;
uniform mat4 model;

// Scene data
uniform uint frame;
uniform uint update;
uniform bool is_animated;

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
	}

	f_texture = v_texture;
	gl_Position = projection * view * model * vec4(t_pos, 1.0);
}