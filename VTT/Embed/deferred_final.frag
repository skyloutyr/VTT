﻿#version 330 core

#define SHADOW_BIAS_MAX 0.01
#define SHADOW_BIAS_MIN 0.0005

#define HAS_POINT_SHADOWS
#define HAS_DIRECTIONAL_SHADOWS
#define BRANCHING

in vec2 f_texture;

uniform uint frame;
uniform uint update;

uniform vec3 camera_position;

uniform sampler2D g_positions;
uniform sampler2D g_normals;
uniform sampler2D g_albedo;
uniform sampler2D g_aomrg;
uniform sampler2D g_emission;

// Directional light
uniform vec3 dl_direction;
uniform vec3 dl_color;
uniform vec3 al_color;
uniform sampler2DShadow dl_shadow_map;
uniform mat4 sun_view;
uniform mat4 sun_projection;

// Point lights
uniform vec3 pl_position[16];
uniform vec3 pl_color[16];
uniform vec2 pl_cutout[16];
uniform int pl_index[16];
uniform int pl_num;
uniform sampler2DArrayShadow pl_shadow_maps;

// fow
uniform usampler2D fow_texture;
uniform vec2 fow_offset;
uniform vec2 fow_scale;
uniform float fow_mod;
uniform vec3 sky_color;

// grid
uniform vec4 grid_color;

// darkvision
uniform vec4 dv_data;

out vec4 g_color;

const vec3 surface_reflection_for_dielectrics = vec3(0.04f);
const float PI = 3.14159265359f;
const float eff_epsilon = 0.00001f;

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0f - F0) * pow(clamp(1.0f - cosTheta, 0.0f, 1.0f), 5.0f);
}

float ndfGGX(float cosLh, float roughness)
{
	float alpha = roughness * roughness;
	float alphaSq = alpha * alpha;
	float denom = (cosLh * cosLh) * (alphaSq - 1.0) + 1.0;
	return alphaSq / (PI * denom * denom);
}

float distributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness*roughness;
    float a2 = a*a;
    float NdotH = max(dot(N, H), 0.0f);
    float NdotH2 = NdotH*NdotH;
    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0f) + 1.0f);
    denom = PI * denom * denom;
    return num / denom;
}

float geometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0f);
    float k = (r*r) * 0.125f;
    float num = NdotV;
    float denom = NdotV * (1.0f - k) + k;
    return num / denom;
}

float geometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0f);
    float NdotL = max(dot(N, L), 0.0f);
    float ggx2 = geometrySchlickGGX(NdotV, roughness);
    float ggx1 = geometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}


// Single term for separable Schlick-GGX below.
float gaSchlickG1(float cosTheta, float k)
{
	return cosTheta / (cosTheta * (1.0f - k) + k);
}

// Schlick-GGX approximation of geometric attenuation function using Smith's method.
float gaSchlickGGX(float cosLi, float cosLo, float roughness)
{
	float r = roughness + 1.0f;
	float k = (r * r) / 8.0f; // Epic suggests using this roughness remapping for analytic lights.
	return gaSchlickG1(cosLi, k) * gaSchlickG1(cosLo, k);
}

// Shlick's approximation of the Fresnel factor.
vec3 fresnelSchlick(vec3 F0, float cosTheta)
{
	return F0 + (vec3(1.0f) - F0) * pow(1.0f - cosTheta, 5.0f);
}

#ifndef BRANCHING
vec2 project2CubemapTexture(vec3 vec, int side)
{
	float x = vec.x;
	float y = vec.y;
	float z = vec.z;
	float ma = (max(0, min(1, 2 - side)) * x) + (max(0, min(1, 4 - side) * min(1, max(0, side - 1))) * y) + (max(0, min(1, max(0, side - 3))) * z);
	float sc = (max(0, 1 - side) * -z) + (((1 - (1 - side)) * max(0, 2 - side)) * z) + (max(0, min(1, 5 - side) * min(1, max(0, side - 1))) * x) + (max(0, min(1, side - 4)) * -x);
	float tc = (((max(0, min(1, 2 - side))) | (min(1, max(0, side - 3)))) * -y) + (max(0, min(1, 3 - side) * min(1, max(0, side - 1))) * z) + (max(0, min(1, 4 - side) * min(1, max(0, side - 2))) * -z);
	float s = ((sc / abs(ma)) + 1) * 0.5f;
	float t = ((tc / abs(ma)) + 1) * 0.5f;
	return vec2(s, t);
}

float computeShadowForSide(int light, vec3 light2frag, vec3 norm, int side, int offset)
{
	vec2 coords = project2CubemapTexture(light2frag, side);
	float current_depth = length(light2frag) / pl_cutout[light].x;
	float bias = 0f;  
	float shadow_depth = texture(pl_shadow_maps, vec4(coords.x, coords.y, offset + side, current_depth - bias));
	return clamp(shadow_depth * pl_cutout[light].y, 0, 1);
}

float computeShadow(int light, vec3 light2frag, vec3 norm)
{
	vec3 norm_l2f = normalize(light2frag);
	int mxy = int(max(0, ceil(abs(norm_l2f.x) - abs(norm_l2f.y)) + (1 - ceil(abs(abs(norm_l2f.x) - abs(norm_l2f.y))))));
	int mxz = int(max(0, ceil(abs(norm_l2f.x) - abs(norm_l2f.z)) + (1 - ceil(abs(abs(norm_l2f.x) - abs(norm_l2f.z))))));
	int myz = int(max(0, ceil(abs(norm_l2f.y) - abs(norm_l2f.z)) + (1 - ceil(abs(abs(norm_l2f.y) - abs(norm_l2f.z))))));
	int xC = mxy * mxz;
	int yC = (1 - mxy) * myz;
	int zC = (1 - mxz) * (1 - myz);
	int index = pl_index[light] * 6;
	return max(0, computeShadowForSide(light, light2frag, norm, 
		int(
			 (xC * -sign(norm_l2f.x) + 1 * xC) / 2 + 
			((yC * -sign(norm_l2f.y) + 1 * yC) / 2 + 2 * yC) + 
			((zC * -sign(norm_l2f.z) + 1 * zC) / 2 + 4 * zC)), 
	index));
}
#else
vec3 cubemap(vec3 r) 
{
    vec3 uvw;
    vec3 absr = abs(r);
    bool bx = absr.x > absr.y && absr.x > absr.z;
    bool by = absr.y > absr.z && !bx;
    float modX = float(bx);
    float modY = float(by);
    float modZ = float(!by);
    float negx = step(r.x, 0.0f);
    float negy = step(r.y, 0.0f);
    float negz = step(r.z, 0.0f);

    uvw = 
        bx ? vec3(r.zy, absr.x) * vec3(mix(-1.0f, 1.0f, negx), -1.0f, 1.0f) : 
        by ? vec3(r.xz, absr.y) * vec3(1.0f, mix(1.0f, -1.0f, negy), 1.0f) : 
             vec3(r.xy, absr.z) * vec3(mix(1.0f, -1.0f, negz), -1.0f, 1.0f);

    return vec3(vec2(uvw.xy / uvw.z + 1.0f) * 0.5f, bx ? negx : by ? negy + 2.0f : negz + 4.0f);
}

float texCubemap(vec3 uvw, float offset, float currentDepth) 
{
    vec3 st = cubemap(uvw);
    st.z += offset;
    return texture(pl_shadow_maps, vec4(st, currentDepth));
}

float computeShadow(int light, vec3 light2frag, vec3 norm)
{
	vec3 norm_l2f = normalize(light2frag);
    float current_depth = length(light2frag) / pl_cutout[light].x;
    const float bias = 0f;
    return clamp(texCubemap(norm_l2f, pl_index[light] * 6, current_depth - bias), 0, 1);
}
#endif

vec3 calcLight(vec3 world_to_light, vec3 radiance, vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness)
{
/*
  
	vec3 half_vector = normalize(world_to_camera + world_to_light);

	vec3 f0 = mix(surface_reflection_for_dielectrics, albedo, metallic);
	vec3 f = fresnelSchlick(max(dot(half_vector, world_to_camera), 0.0f), f0);

    float NDF = distributionGGX(normal, half_vector, roughness);
    float G = geometrySmith(normal, world_to_camera, world_to_light, roughness);

    vec3 numerator = NDF * G * f;
    float denominator = 4.0f * max(dot(normal, world_to_camera), 0.0) * max(dot(normal, world_to_light), 0.0f) + eff_epsilon;
    vec3 specular = numerator / denominator; 

    vec3 kS = f;
    vec3 kD = vec3(1.0f) - kS;
  
    kD *= 1.0f - metallic;	

    float NdotL = max(dot(normal, world_to_light), 0.0); 

	return (kD * albedo / PI + specular) * radiance * NdotL;
*/

    vec3 F0 = mix(surface_reflection_for_dielectrics, albedo, metallic);
    vec3 Lh = normalize(world_to_camera + world_to_light);
    float cosLi = max(0.0f, dot(normal, world_to_light));
	float cosLh = max(0.0f, dot(normal, Lh));
    float cosLo = max(0.0f, dot(normal, world_to_camera));
    vec3 F = fresnelSchlick(max(0.0f, dot(Lh, world_to_camera)), F0);
	float D = ndfGGX(cosLh, roughness);
	float G = gaSchlickGGX(cosLi, cosLo, roughness);
    vec3 kd = mix(vec3(1.0f) - F, vec3(0.0f), metallic);
    vec3 diffuseBRDF = kd * albedo;
    vec3 specularBRDF = (F * D * G) / max(eff_epsilon, 4.0f * cosLi * cosLo);
    return (diffuseBRDF + specularBRDF) * radiance * cosLi;

}


vec3 calcPointLight(int light_index, vec3 f_world_position, vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness)
{
    vec3 world_to_light = normalize(pl_position[light_index] - f_world_position);
	float light_distance = length(pl_position[light_index] - f_world_position);

    float attenuation = pl_cutout[light_index].x / (light_distance * light_distance * PI * PI * 4);
    vec3 radiance = pl_color[light_index] * attenuation;
#ifdef HAS_POINT_SHADOWS
#ifndef BRANCHING
    float shadow = computeShadow(light_index, f_world_position - pl_position[light_index], normal);
#else
    float shadow = pl_cutout[light_index].y < eff_epsilon ? 1.0f : computeShadow(light_index, f_world_position - pl_position[light_index], normal);
#endif
#else
    float shadow = 1.0f;
#endif
    return calcLight(world_to_light, radiance, world_to_camera, albedo, normal, metallic, roughness) * shadow;
}

float calcDirectionalShadows(vec3 surface_normal, vec3 world_position)
{
#ifdef HAS_DIRECTIONAL_SHADOWS
    vec4 sun_coord = sun_projection * sun_view * vec4(world_position.xyz, 1.0f);
    float zMod = max(0, floor(sun_coord.z - 1.0f));
    vec3 proj_coords = sun_coord.xyz / sun_coord.w;
    proj_coords = proj_coords * 0.5f + 0.5f; 
    float currentDepth = proj_coords.z;
    float cosTheta = dot(surface_normal, -dl_direction);
    float bias = clamp(0.003f * tan(acos(cosTheta)), 0.0f, 0.01f);
    float result = texture(dl_shadow_map, vec3(proj_coords.xy, currentDepth - bias));
    return result;
#else
    return 1.0f;
#endif
}

vec3 calcDirectionalLight(vec3 world_position, vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness, float ao)
{
    return calcLight(-dl_direction, dl_color, world_to_camera, albedo, normal, metallic, roughness) * calcDirectionalShadows(normal, world_position);
}

vec3 getNormalFromMap()
{
    return texture(g_normals, f_texture).xyz;
}

vec4 getGrid(float g)
{
	return vec4(grid_color.xyz, g);
}

float getFowMultiplier(vec3 f_world_position)
{   
    vec2 uv_fow_world = (f_world_position.xy + fow_offset) * fow_scale;
    vec2 fow_world = (f_world_position.xy + fow_offset);

    uvec4 data = texture(fow_texture, uv_fow_world);
    float yIdx = fract(fow_world.y);

    float mulR = float(yIdx <= 0.25f);
    float mulG = float(yIdx <= 0.5f && yIdx > 0.25f);
    float mulB = float(yIdx <= 0.75f && yIdx > 0.5f);
    float mulA = float(yIdx > 0.75f);

    uint bitOffsetY = 8u * uint(round(mod(yIdx * 4, 1)));
    uint bitOffsetX = uint(fract(fow_world.x) * 8);

    uint mask = (1u << bitOffsetY) << bitOffsetX;

    float r = min(1, float(data.r & mask) * mulR);
    float g = min(1, float(data.g & mask) * mulG);
    float b = min(1, float(data.b & mask) * mulB);
    float a = min(1, float(data.a & mask) * mulA);

    return r + g + b + a;
}

void main()
{
    vec4 world_position = texture(g_positions, f_texture);
    #ifdef BRANCHING
    if (world_position.a <= eff_epsilon)
    {
        discard;
    }

    #endif

    vec3 world_to_camera = normalize(camera_position - world_position.rgb);
    vec4 aomrg = texture(g_aomrg, f_texture);
    vec3 albedo = texture(g_albedo, f_texture).rgb;
    vec3 normal = getNormalFromMap();
    vec3 emissive = vec3(0.0f);
    float ao = aomrg.r;
    float m = aomrg.g;
    float r = aomrg.b;
    float g = aomrg.a;

    vec3 light_colors = vec3(0.0f, 0.0f, 0.0f);
    for (int i = 0; i < pl_num; ++i)
    {
        light_colors += calcPointLight(i, world_position.rgb, world_to_camera, albedo, normal, m, r);
    }

    vec3 ambient = al_color * albedo * ao;
	vec3 color = ambient + light_colors + calcDirectionalLight(world_position.rgb, world_to_camera, albedo, normal, m, r, ao) + emissive;
	vec4 grid = getGrid(g);
    color = mix(color, grid.rgb, grid.a);
    color = color + texture(g_emission, f_texture).rgb;

#ifndef BRANCHING
    float fowVal = getFowMultiplier(world_position.rgb) * fow_mod + (1.0f * (1.0f - fow_mod));
    g_color = vec4(mix(sky_color, color, fowVal), world_position.a);
#else
    if (fow_mod > eff_epsilon)
    {
        float fowVal = getFowMultiplier(world_position.rgb) * fow_mod + (1.0f * (1.0f - fow_mod));
        g_color = vec4(mix(sky_color, color, fowVal), world_position.a);
    }
    else
    {
        g_color = vec4(color, world_position.a);
    }
#endif
}