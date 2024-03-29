﻿#version 330 core
in vec3 f_world_pos;

uniform vec3 camera_position;
uniform vec4 g_color;
uniform float g_alpha;
uniform float g_size;
uniform vec3 cursor_position;

out vec4 f_color;
const float cutoff = 0.8;

void main()
{
	float cameraDistanceFactor = length(camera_position - f_world_pos);
	float m = cameraDistanceFactor - 32.0;
	float cutoffFactor = 1.0 + (cameraDistanceFactor / 32.0) + max(0, pow(m / 32.0, 2) * sign(m));
	float m_cutoff = cutoff + 0.19 / cutoffFactor;

	float grid_math_scale = g_size;
	vec3 grid_modulo = abs(mod(f_world_pos - (0.5 * grid_math_scale), vec3(grid_math_scale)));
	
	vec3 g_d = ceil(max(vec3(0.0), abs(grid_math_scale - grid_modulo) - m_cutoff * grid_math_scale));
	float gmx = max(g_d.x, g_d.y);

	float world_to_cursor_x = 0.5 * (min(1.5, abs(f_world_pos.x - cursor_position.x)) / 1.5);
	float world_to_cursor_y = 0.5 * (min(1.5, abs(f_world_pos.y - cursor_position.y)) / 1.5);
	float world_to_cursor_sphere_factor = 0.5 * (min(1.5, length(f_world_pos - cursor_position)) / 1.5);

	float world_distance_to_cursor_effect = max(0, max(world_to_cursor_x + world_to_cursor_y - 2 * world_to_cursor_x * world_to_cursor_y, world_to_cursor_sphere_factor));

	f_color = vec4(g_color.xyz, max(0, (g_color.a * gmx * g_alpha) - world_distance_to_cursor_effect));
	if (f_color.a < 0.001)
	{
		discard;
	}
}