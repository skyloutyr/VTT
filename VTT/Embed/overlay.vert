#version 330 core

layout (location = 0) in vec3 v_pos;
layout (location = 1) in vec2 v_texture;

out vec3 f_world_pos;
out vec2 f_texture;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 model;

void main()
{
	f_world_pos = (model * vec4(v_pos, 1.0f)).xyz;
	f_texture = v_texture;
	gl_Position = projection * view * model * vec4(v_pos, 1.0f);
}