#version 330 core
layout (location = 0) in vec3 v_pos;

out vec3 f_tex;

uniform mat4 view;
uniform mat4 projection;

void main()
{
	f_tex = v_pos;
	vec4 pos = projection * view * vec4(v_pos, 1.0);
	gl_Position = pos.xyww;
}