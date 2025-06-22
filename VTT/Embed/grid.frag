#version 330 core

#define VTT_PKIND_GRID

#define GRID_TYPE_SQUARE 0u
#define GRID_TYPE_HHEX 1u
#define GRID_TYPE_VHEX 2u

in vec3 f_world_position;

uniform vec3 camera_position;
uniform vec4 grid_color;
uniform float grid_alpha;
uniform float grid_size;
uniform vec3 cursor_position;
uniform uint grid_type;

out vec4 f_color;

const float grid_cutoff = 0.8;
const float grid_camera_cutoff_threshold = 32.0;
const float grid_side_lerp_threshold = 0.03125;
const float grid_side_length = 0.19;

float getSquareGrid()
{
    float cameraDistanceFactor = length(camera_position - f_world_position);
	float m = cameraDistanceFactor - grid_camera_cutoff_threshold;
	float cutoffFactor = (cameraDistanceFactor * grid_side_lerp_threshold) + 1.0 + max(0, pow(m * grid_side_lerp_threshold, 2) * sign(m));
	float m_cutoff = grid_cutoff + grid_side_length / cutoffFactor;
	vec3 grid_modulo = abs(mod(f_world_position - (0.5 * grid_size), vec3(grid_size)));
	vec3 g_d = ceil(max(vec3(0.0), abs(grid_size - grid_modulo) - m_cutoff * grid_size));
	float gmx = max(g_d.x, g_d.y);
	return gmx;
}

// https://www.shadertoy.com/view/wtdSzX
float getHexGrid(bool horizontal)
{
	// Note that the hex grid doesn't do the m_cutoff factor for camera position relative to grid.

	vec2 s = horizontal ? vec2(1.7320508, 1) : vec2(1, 1.7320508); // Hexagon factor
	vec2 p = (f_world_position.xy - vec2(0.02, 0.02)) / grid_size; // 0.02 is offset for cutoff on eDist to 'center' the hexagons
	vec4 hC = floor(vec4(p, p - (horizontal ? vec2(1, 0.5) : vec2(0.5, 1))) / s.xyxy) + 0.5; // Create 2 square grids
	vec4 h = vec4(p - hC.xy * s, p - (hC.zw + 0.5) * s); // Transform them to 'hexagon space', as in make them have the correct fractions and overlap
	vec4 hex = abs(dot(h.xy, h.xy) < dot(h.zw, h.zw) ? vec4(h.xy, hC.xy) : vec4(h.zw, hC.zw + 0.5)); // Simply pick the closest center square - naturally returns in a hexagon - more info here https://inspirnathan.com/posts/174-interactive-hexagon-grid-tutorial-part-5
	float eDist = max(dot(hex.xy, s * 0.5), horizontal ? hex.y : hex.x); // Get distance to edge and pick the correct term for horizontal/vertical
	return smoothstep(0.0, 0.03, eDist - 0.5 + 0.04); // Smoothstep to hexagonal edges, 0.5 is the offset, 0.04 is the side length
}

vec4 getGrid()
{
#ifndef VTT_PKIND_GRID
	vec3 normal = f_tbn[2];
    float d = dot(normal, unitZ);
    d = float(abs(d) >= 0.45);
#else
	float d = 1.0;
#endif
	float gmx = 0;
	switch (grid_type)
	{
		case GRID_TYPE_SQUARE:
		{
			gmx = getSquareGrid();
			break;
		}

		case GRID_TYPE_HHEX:
		{
			gmx = getHexGrid(true);
			break;
		}

		case GRID_TYPE_VHEX:
		{
			gmx = getHexGrid(false);
			break;
		}

		default:
		{
			gmx = getSquareGrid();
			break;
		}
	}

	float world_to_cursor_x = 0.5 * (min(1.5, abs(f_world_position.x - cursor_position.x)) * 0.666666);
	float world_to_cursor_y = 0.5 * (min(1.5, abs(f_world_position.y - cursor_position.y)) * 0.666666);
	float world_to_cursor_sphere_factor = 0.5 * (min(1.5, length(f_world_position - cursor_position)) * 0.666666);

	float world_distance_to_cursor_effect = max(0, max(world_to_cursor_x + world_to_cursor_y - 2 * world_to_cursor_x * world_to_cursor_y, world_to_cursor_sphere_factor));

	return vec4(grid_color.xyz, max(0, (grid_color.a * gmx * grid_alpha * d) - world_distance_to_cursor_effect));
}

void main()
{
	f_color = getGrid();
	if (f_color.a < 0.001)
	{
		discard;
	}
}