#version 330 core
layout (location = 0) in vec3 v_pos;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 model;

out vec4 f_world_position;

void main()
{	
	f_world_position = model * vec4(v_pos, 1.0);
	gl_Position = projection * view * f_world_position;
}