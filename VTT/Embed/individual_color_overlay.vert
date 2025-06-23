#version 330 core
layout (location = 0) in vec4 v_pos_color;

uniform mat4 view;
uniform mat4 projection;
uniform mat4 model;

out vec4 f_color;

vec4 unpackColor()
{
	uint color_ui = floatBitsToUint(v_pos_color.w); // CPU - ARGB format, here we see it as BGRA (endianness moment)
	return vec4(
		float((color_ui & 0x00ff0000u) >> 16u) / 255.0,
		float((color_ui & 0x0000ff00u) >> 8u) / 255.0,
		float(color_ui & 0x000000ffu) / 255.0,
		float((color_ui & 0xff000000u) >> 24u) / 255.0
	);
}

void main()
{	
	f_color = unpackColor();
	gl_Position = projection * view * model * vec4(v_pos_color.xyz, 1.0);
}