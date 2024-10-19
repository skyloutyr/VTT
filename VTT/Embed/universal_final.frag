#version 330 core

in vec2 f_tex;

uniform sampler2D g_color;
uniform sampler2D g_depth;
uniform sampler2D g_fast_light;
uniform sampler2D g_shadows2d;
uniform float gamma;

out layout (location = 0) vec4 o_color;

void main()
{
	o_color = texture(g_color, f_tex);
	if (o_color.a < 0.0039215686274509803921568627451)
	{
		discard;
	}

	o_color.rgb += texture(g_fast_light, f_tex).rgb;
	o_color.rgb = pow(o_color.rgb, vec3(1.0 / gamma));
	o_color.rgb *= texture(g_shadows2d, f_tex).r;

	gl_FragDepth = texture(g_depth, f_tex).r;
}