#version 330 core
#define BRANCHING

in mat3 f_tbn;
in vec3 f_position;
in vec3 f_normal;
in vec3 f_tangent;
in vec3 f_bitangent;
in vec3 f_world_position;
in vec4 f_color;
in vec2 f_texture;

uniform uint frame;
uniform uint update;

uniform vec3 camera_position;

uniform vec4 m_diffuse_color;
uniform float m_metal_factor;
uniform float m_roughness_factor;
uniform float m_alpha_cutoff;
uniform sampler2D m_texture_diffuse;
uniform sampler2D m_texture_normal;
uniform sampler2D m_texture_emissive;
uniform sampler2D m_texture_aomr;
uniform vec4 m_diffuse_frame;
uniform vec4 m_normal_frame;
uniform vec4 m_emissive_frame;
uniform vec4 m_aomr_frame;

uniform vec4 tint_color;

uniform float grid_alpha;
uniform float grid_size;
uniform vec3 cursor_position;

out vec4 g_position;
out vec4 g_normal;
out vec4 g_albedo;
out vec4 g_aomrg;
out vec4 g_emission;

vec4 sampleMap(sampler2D sampler, vec4 frameData)
{
    return texture(sampler, f_texture * frameData.zw + frameData.xy);
}

vec3 getNormalFromMap()
{
    vec3 tangentNormal = sampleMap(m_texture_normal, m_normal_frame).xyz * 2.0 - 1.0;
    return normalize(f_tbn * tangentNormal);
}

const float cutoff = 0.8;
const vec3 unitZ = vec3(0.0, 0.0, 1.0);
const float eff_epsilon = 0.0001;
float getGrid()
{
    vec3 normal = f_tbn[2];
    float d = dot(normal, unitZ);
#ifndef BRANCHING
    d = min(1, floor(0.45 + abs(d)));
#else
    d = float(abs(d) >= 0.45);
#endif

    float cameraDistanceFactor = length(camera_position - f_world_position);
	float m = cameraDistanceFactor - 32.0;
	float cutoffFactor = (cameraDistanceFactor * 0.03125) + 1.0 + max(0, pow(m * 0.03125, 2) * sign(m));
	float m_cutoff = cutoff + 0.19 / cutoffFactor;

	float grid_math_scale = grid_size;
	vec3 grid_modulo = abs(mod(f_world_position - (0.5 * grid_math_scale), vec3(grid_math_scale)));
	
	vec3 g_d = ceil(max(vec3(0.0), abs(grid_math_scale - grid_modulo) - m_cutoff * grid_math_scale));
	float gmx = max(g_d.x, g_d.y);

	float world_to_cursor_x = 0.5 * (min(1.5, abs(f_world_position.x - cursor_position.x)) * 0.666666);
	float world_to_cursor_y = 0.5 * (min(1.5, abs(f_world_position.y - cursor_position.y)) * 0.666666);
	float world_to_cursor_sphere_factor = 0.5 * (min(1.5, length(f_world_position - cursor_position)) * 0.666666);

	float world_distance_to_cursor_effect = max(0, max(world_to_cursor_x + world_to_cursor_y - 2 * world_to_cursor_x * world_to_cursor_y, world_to_cursor_sphere_factor));

	return max(0, (gmx * grid_alpha * d) - world_distance_to_cursor_effect);
}

void main()
{
    vec3 world_to_camera = normalize(camera_position - f_world_position);
    vec3 albedo = sampleMap(m_texture_diffuse, m_diffuse_frame).rgb * tint_color.rgb;
    vec3 normal = getNormalFromMap();
    vec3 aomr = sampleMap(m_texture_aomr, m_aomr_frame).rgb;
    vec3 emissive = sampleMap(m_texture_emissive, m_emissive_frame).rgb;
    float ao = aomr.r;
    float m = aomr.g;
    float r = aomr.b;
    float g = 0;

#ifndef BRANCHING
	g = getGrid();
#else
    if (grid_alpha > eff_epsilon)
    {
        g = getGrid();
    }
#endif

    g_position = vec4(f_world_position, 1.0);
    g_normal = vec4(normal, 1.0);
    g_albedo = vec4(albedo, 1.0);
    g_aomrg = vec4(ao, m, r, g);
    g_emission = sampleMap(m_texture_emissive, m_emissive_frame);
}