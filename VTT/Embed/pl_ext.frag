#version 330 core
in vec4 frag_pos;

uniform vec3 light_pos;
uniform float far_plane;

void main()
{
	float dist = length(frag_pos.xyz - light_pos) * 1.06;
	dist = dist / far_plane;
	gl_FragDepth = dist;
}