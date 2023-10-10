#version 330 core

in vec2 f_tex;

uniform sampler2D g_color;
uniform sampler2D g_depth;

out layout (location = 0) vec4 o_color;

void main()
{
	o_color = texture(g_color, f_tex);
	if (o_color.a < 0.0039215686274509803921568627451)
	{
		discard;
	}

	gl_FragDepth = texture(g_depth, f_tex).r;
}