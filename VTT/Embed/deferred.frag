#version 330 core

#undef NODEGRAPH
#undef VTTGLSLIMPL
#define VTT_PKIND_DEFERRED

#define GRID_TYPE_SQUARE 0u
#define GRID_TYPE_HHEX 1u
#define GRID_TYPE_VHEX 2u

in mat3 f_tbn;
in vec3 f_world_position;
in vec4 f_color;
in vec2 f_texture;

layout (std140) uniform FrameData {
    mat4 view; // due to the particle shader demanding view separately, the view and projection matrices can't be united together here
    mat4 projection;
    mat4 sun_matrix;
    vec4 camera_position_sundir; // camera_position = xyz, sundir = unpackNorm101010(w)
    vec4 camera_direction_sunclr; // camera_direction = xyz, sunclr = unpackRgba(w)
    vec4 al_sky_colors_viewportsz; // al_color = unpackRgba(x), sky_color = unpackRgba(y), viewportsz = zw
    vec4 cursor_position_gridclr; // cursor_position = xyz, grid_color = unpackRgba(w)
    vec4 frame_update_updatedt_gridsz; // frame = reinterpretcast<uint>(x), update = reinterpretcast<uint>(y), updatedt = z, gridsz = w
    vec4 skybox_colors_blend_pl_num; // color_day = unpackRgba(x), color_night = unpackRgba(y), blend = z, pl_num = reinterpretcast<uint>(w)
    vec4 skybox_animation_day; // full frame data
    vec4 skybox_animation_night; // full frame data
    mat4 sunCascadeMatrices[5];
    vec4 cascadePlaneDistances;
    vec4 pl_positions_color[16]; // pl_position = [i].xyz, pl_color = unpackRgba([i].w)
    vec4 pl_cutouts[4]; // pl_cutout = abs([i >> 2][i & 3]), pl_casts_shadows = sign(pl_cutout) > epsilon
    vec4 fow_scale_mod; // fow_scale = vec2(x), fow_offset = vec2(0.5) + floor(fow_scale * 0.5), fow_mod = y, zw unused
};

layout (std140) uniform Material
{
    vec4 albedo_metal_roughness_alpha_cutoff; // diffuse_color = unpackRgba(x), metalness = y, roughness = z, alpha_cutoff = w
    vec4 m_diffuse_frame;
    vec4 m_normal_frame;
    vec4 m_emissive_frame;
    vec4 m_aomr_frame;
    vec4 m_index_padding; // index = reinterpretcast<uint>(x), yzw padded
};

// Unfortunately can't pack samplers into uniforms (could with ARB_bindless_texture, but support is iffy)
uniform sampler2D m_texture_diffuse;
uniform sampler2D m_texture_normal;
uniform sampler2D m_texture_emissive;
uniform sampler2D m_texture_aomr;

uniform vec4 tint_color;

uniform float grid_alpha;
uniform uint grid_type;

layout (location = 0) out vec4 g_color; // no writing here occurs, needed for consistency
layout (location = 1) out vec4 g_position;
layout (location = 2) out vec4 g_normal;
layout (location = 3) out vec4 g_albedo;
layout (location = 4) out vec4 g_aomrg;
layout (location = 5) out vec4 g_emission;

const float oneOver255 = 1.0 / 255.0;
vec4 unpackRgba(float packedRgbaVal)
{
    uint packed_ui = floatBitsToUint(packedRgbaVal);
    return vec4(
        float((packed_ui >> 24) & 0xffu),
        float((packed_ui >> 16) & 0xffu),
        float((packed_ui >> 8) & 0xffu),
        float(packed_ui & 0xffu)
    ) * oneOver255;
}

const float unorm101010unpackfact = 1.0 / 511.5;
const vec3 unormtonormconversionadder = vec3(-1.0, -1.0, -1.0);
vec3 unpackNorm101010(float packedNorm101010)
{
    uint packed_ui = floatBitsToUint(packedNorm101010);
    return normalize(vec3(
        float((packed_ui >> 20) & 0x3ffu),
        float((packed_ui >> 10) & 0x3ffu),
        float(packed_ui & 0x3ffu)
    ) * unorm101010unpackfact + unormtonormconversionadder);
}

vec2 unpackvec216x2(float packedv2)
{
    uint packed_ui = floatBitsToUint(packedv2);
    return vec2(
        uintBitsToFloat((packed_ui >> 16) & 0xffffu),
        uintBitsToFloat((packed_ui) & 0xffffu)
    );
}

vec4 sampleMapCustom(sampler2D sampler, vec2 uvs, vec4 frameData)
{
    return texture(sampler, uvs * frameData.zw + frameData.xy);
}

vec4 sampleMap(sampler2D sampler, vec4 frameData)
{
    return sampleMapCustom(sampler, f_texture, frameData);
}

vec3 getNormalFromMap()
{
    vec3 tangentNormal = sampleMap(m_texture_normal, m_normal_frame).xyz * 2.0 - 1.0;
    return normalize(f_tbn * tangentNormal);
}

vec3 getNormalFromMapCustom(vec2 uvs)
{
    vec3 tangentNormal = sampleMapCustom(m_texture_normal, uvs, m_normal_frame).xyz * 2.0 - 1.0;
    return normalize(f_tbn * tangentNormal);
}

vec3 rgb2hsv(vec3 c)
{
    vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c)
{
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

const float cutoff = 0.8;
const vec3 unitZ = vec3(0.0, 0.0, 1.0);
const float eff_epsilon = 0.0001;
const float grid_cutoff = 0.8;
const float grid_camera_cutoff_threshold = 32.0;
const float grid_side_lerp_threshold = 0.03125;
const float grid_side_length = 0.19;

float getSquareGrid()
{
    float grid_size = frame_update_updatedt_gridsz.w;
    vec2 grid_v = fract((f_world_position.xy + vec2(0.5)) / grid_size); // Offset by 0.5 to match the old square grid impl, scale down by the size factor and clamp to a [0-1] factor
    grid_v = abs((grid_v * 2.0) + vec2(-1.0, -1.0)); // fma to move from [0-1] to a [-1-1] range, and then turn it to a [1-1] range (distance to edge)
    return max(grid_v.x, grid_v.y) > 0.96 ? 1.0 : 0.0; // return 1 if we are 0.04 units away from the edge, 0 otherwise
}

// https://www.shadertoy.com/view/wtdSzX
float getHexGrid(bool horizontal)
{
	vec2 s = horizontal ? vec2(1.7320508, 1) : vec2(1, 1.7320508); // Hexagon factor
	vec2 p = (f_world_position.xy - vec2(0.02, 0.02)) / frame_update_updatedt_gridsz.w; // 0.02 is offset for cutoff on eDist to 'center' the hexagons
	vec4 hC = floor(vec4(p, p - (horizontal ? vec2(1, 0.5) : vec2(0.5, 1))) / s.xyxy) + 0.5; // Create 2 square grids
	vec4 h = vec4(p - hC.xy * s, p - (hC.zw + 0.5) * s); // Transform them to 'hexagon space', as in make them have the correct fractions and overlap
	vec4 hex = abs(dot(h.xy, h.xy) < dot(h.zw, h.zw) ? vec4(h.xy, hC.xy) : vec4(h.zw, hC.zw + 0.5)); // Simply pick the closest center square - naturally returns in a hexagon - more info here https://inspirnathan.com/posts/174-interactive-hexagon-grid-tutorial-part-5
	float eDist = max(dot(hex.xy, s * 0.5), horizontal ? hex.y : hex.x); // Get distance to edge and pick the correct term for horizontal/vertical
	return smoothstep(0.0, 0.03, eDist - 0.5 + 0.04); // Smoothstep to hexagonal edges, 0.5 is the offset, 0.04 is the side length
}

float getGrid()
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

	float world_to_cursor_x = 0.5 * (min(1.5, abs(f_world_position.x - cursor_position_gridclr.x)) * 0.666666);
	float world_to_cursor_y = 0.5 * (min(1.5, abs(f_world_position.y - cursor_position_gridclr.y)) * 0.666666);
	float world_to_cursor_sphere_factor = 0.5 * (min(1.5, length(f_world_position - cursor_position_gridclr.xyz)) * 0.666666);

	float world_distance_to_cursor_effect = max(0, max(world_to_cursor_x + world_to_cursor_y - 2 * world_to_cursor_x * world_to_cursor_y, world_to_cursor_sphere_factor));

	return max(0, (gmx * grid_alpha * d) - world_distance_to_cursor_effect);
}

#ifdef VTTGLSLIMPL
#pragma ANCHOR_GLSL
#endif

void shaderGraph(out vec3 albedo, out vec3 normal, out vec3 emissive, out float ao, out float m, out float r, out float a)
{
#ifndef NODEGRAPH
    vec4 d_clr = unpackRgba(albedo_metal_roughness_alpha_cutoff.x);
    vec4 albedo_tex = sampleMap(m_texture_diffuse, m_diffuse_frame);
    albedo = albedo_tex.rgb * d_clr.rgb * tint_color.rgb;
    normal = getNormalFromMap();
    vec3 aomr = sampleMap(m_texture_aomr, m_aomr_frame).rgb;
    emissive = sampleMap(m_texture_emissive, m_emissive_frame).rgb;
    ao = aomr.r;
    m = aomr.g;
    r = aomr.b;
    a = tint_color.a * albedo_tex.a * d_clr.a;
#else
#pragma ENTRY_NODEGRAPH
#endif
}

void main()
{
    vec3 world_to_camera = normalize(camera_position_sundir.xyz - f_world_position);
    vec3 albedo = vec3(0.0, 0.0, 0.0);
    vec3 normal = vec3(0.0, 0.0, 0.0);
    vec3 emissive = vec3(0.0, 0.0, 0.0);
    float ao = 0.0;
    float m = 0.0;
    float r = 0.0;
    float l_a = 0.0;
    shaderGraph(albedo, normal, emissive, ao, m, r, l_a);
    float g = 0;
    if (grid_alpha > eff_epsilon)
    {
        g = getGrid();
    }

    g_position = vec4(f_world_position, 1.0);
    g_normal = vec4(normal, 1.0);
    g_albedo = vec4(albedo, 1.0);
    g_aomrg = vec4(ao, m, r, g);
    g_emission = vec4(emissive, 1.0);
}