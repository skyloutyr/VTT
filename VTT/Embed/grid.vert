#version 330 core

layout (location = 0) in vec3 v_pos;

out vec3 f_world_position;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 model;
uniform vec4 g_color;
uniform float g_alpha;
uniform float g_size;

void main()
{
	f_world_position = (model * vec4(v_pos, 1.0)).xyz;
	vec4 v_p = projection * view * model * vec4(v_pos, 1.0);
	gl_Position = v_p.xyww;
}