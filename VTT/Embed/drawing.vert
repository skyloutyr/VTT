#version 330 core

layout (location = 0) in vec3 v_pos;
layout (location = 1) in vec3 v_offset;

uniform mat4 projection;
uniform mat4 view;

void main()
{
	gl_Position = projection * view * vec4(v_pos + v_offset, 1.0);
}