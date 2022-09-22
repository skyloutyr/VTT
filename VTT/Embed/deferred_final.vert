#version 330 core

layout (location = 0) in vec2 v_position;

out vec2 f_texture;

uniform sampler2D g_positions;

void main()
{
	f_texture = v_position * 0.5f + 0.5f;
	gl_Position = vec4(v_position, 0.0f, 1.0f);
}