#version 330 core

in vec3 f_world_pos;

uniform vec4 u_color;

out vec4 o_color;

void main()
{
	o_color = u_color;
}