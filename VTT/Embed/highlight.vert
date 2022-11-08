#version 330 core

layout (location = 0) in vec3 v_pos;
layout (location = 1) in vec3 v_pos_offset_mul;

out vec3 f_world_pos;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 model;
uniform vec3 bounds;

void main()
{
	vec3 bounds_modified_pos = v_pos + ((vec3(1.0) - bounds) / 2.0 * v_pos_offset_mul);
	f_world_pos = (model * vec4(bounds_modified_pos, 1.0)).xyz;
	gl_Position = projection * view * model * vec4(bounds_modified_pos, 1.0);
}