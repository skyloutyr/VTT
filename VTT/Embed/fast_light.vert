#version 330 core

layout (location = 0) in vec3 v_pos;

uniform vec4 model;
uniform mat4 view;
uniform mat4 projection;

out vec4 world_frag;
flat out vec4 light_position;

void main()
{
	world_frag = vec4((v_pos * model.w) + model.xyz, 1.0);
	light_position = vec4(model.xyz, 1.0);

	gl_Position = projection * view * world_frag;
}