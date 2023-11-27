#version 330 core

layout (location = 0) in vec2 v_pos;
layout (location = 1) in vec2 v_texture;

out vec2 f_texture;

uniform mat4 view;
uniform mat4 projection;
uniform vec4 position;
uniform vec2 screenSize;

void main()
{
	vec3 vpWorld = position.xyz;
	gl_Position = projection * view * vec4(vpWorld, 1.0);
	gl_Position /= gl_Position.w;
	gl_Position.xy += v_pos.xy * screenSize * position.w;
	f_texture = v_texture;
}