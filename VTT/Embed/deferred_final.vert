#version 330 core

layout (location = 0) in vec2 v_position;
layout (location = 1) in vec2 v_texture;

out vec2 f_texture;

uniform sampler2D g_positions;

void main()
{
	f_texture = v_texture;
	gl_Position = vec4(v_position, 0.0, 1.0);
}