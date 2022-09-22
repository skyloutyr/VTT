#version 330 core
layout (location = 0) in vec3 v_position;
layout (location = 1) in vec2 v_texture;
layout (location = 2) in vec3 v_normal;
layout (location = 3) in vec3 v_tangent;
layout (location = 4) in vec3 v_bitangent;
layout (location = 5) in vec4 v_color;

uniform mat4 model;

void main()
{
	gl_Position = model * vec4(v_position, 1.0); 
}