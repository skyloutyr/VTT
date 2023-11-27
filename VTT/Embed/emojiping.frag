#version 330 core

in vec2 f_texture;

uniform vec4 u_color;
uniform sampler2D texture_image;

out vec4 o_color;

void main()
{
	o_color = texture(texture_image, f_texture) * u_color;
}