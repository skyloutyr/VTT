﻿#version 330 core

#define SHADOW_BIAS_MAX 0.01
#define SHADOW_BIAS_MIN 0.0005

#ifndef GL_SPIRV
#define PCF_ITERATIONS 2
#define HAS_POINT_SHADOWS
#define HAS_DIRECTIONAL_SHADOWS
#else
layout(constant_id = 0) const int has_dir_shadows = 1;
layout(constant_id = 1) const int has_point_shadows = 1;
layout(constant_id = 2) const int has_branches = 1;
layout(constant_id = 3) const int PCF_ITERATIONS = 1;
#endif

#define BRANCHING

in vec2 f_texture;

layout (std140) uniform FrameData {
	mat4 view;
	mat4 projection;
	mat4 sun_view;
	mat4 sun_projection;
	vec3 camera_position;
	vec3 camera_direction;
	vec3 dl_direction;
	vec3 dl_color;
	vec3 al_color;
	vec3 sky_color;
	vec3 cursor_position;
	vec4 grid_color;
	vec4 dv_data;
	uint frame;
	uint update;
	float grid_size;
    float frame_delta;
};

uniform sampler2D g_positions;
uniform sampler2D g_normals;
uniform sampler2D g_albedo;
uniform sampler2D g_aomrg;
uniform sampler2D g_emission;
uniform sampler2D g_depth;

// Directional light
uniform sampler2D dl_shadow_map;

// Point lights
uniform vec3 pl_position[16];
uniform vec3 pl_color[16];
uniform vec2 pl_cutout[16];
uniform int pl_index[16];
uniform int pl_num;
uniform sampler2DArray pl_shadow_maps;

// fow
uniform usampler2D fow_texture;
uniform vec2 fow_offset;
uniform vec2 fow_scale;
uniform float fow_mod;
uniform float gamma_factor;

out vec4 g_color;

const vec3 surface_reflection_for_dielectrics = vec3(0.04);
const float PI = 3.14159265359;
const float eff_epsilon = 0.00001;

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
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
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;
    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    return num / denom;
}

float geometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) * 0.125;
    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    return num / denom;
}

float geometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = geometrySchlickGGX(NdotV, roughness);
    float ggx1 = geometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}


// Single term for separable Schlick-GGX below.
float gaSchlickG1(float cosTheta, float k)
{
	return cosTheta / (cosTheta * (1.0 - k) + k);
}

// Schlick-GGX approximation of geometric attenuation function using Smith's method.
float gaSchlickGGX(float cosLi, float cosLo, float roughness)
{
	float r = roughness + 1.0;
	float k = (r * r) / 8.0; // Epic suggests using this roughness remapping for analytic lights.
	return gaSchlickG1(cosLi, k) * gaSchlickG1(cosLo, k);
}

// Shlick's approximation of the Fresnel factor.
vec3 fresnelSchlick(vec3 F0, float cosTheta)
{
	return F0 + (vec3(1.0) - F0) * pow(1.0 - cosTheta, 5.0);
}

float getShadowDepth2DArray(sampler2DArray sampler, vec3 coords, float depth)
{
    float pcf_itr_con = 0;
    float shadow = 0.0;
    vec2 mOffset = vec2(1.0, 1.0) / textureSize(sampler, 0).xy;
    for (int y = -PCF_ITERATIONS; y <= PCF_ITERATIONS; ++y)
    {
        for (int x = -PCF_ITERATIONS; x <= PCF_ITERATIONS; ++x)
        {
            vec2 offset = vec2(x, y) * mOffset;
            shadow += texture(sampler, coords + vec3(offset, 0.0)).r < depth ? 0.0 : 1.0;
            ++pcf_itr_con;
        }
    }

    return shadow / pcf_itr_con;
}

float getShadowDepth2D(sampler2D sampler, vec2 coords, float depth)
{
    float pcf_itr_con = 0;
    float shadow = 0.0;
    vec2 mOffset = vec2(1.0, 1.0) / textureSize(sampler, 0).xy;
    for (int y = -PCF_ITERATIONS; y <= PCF_ITERATIONS; ++y)
    {
        for (int x = -PCF_ITERATIONS; x <= PCF_ITERATIONS; ++x)
        {
            vec2 offset = vec2(x, y) * mOffset;
            shadow += texture(sampler, coords + offset).r < depth ? 0.0 : 1.0;
            ++pcf_itr_con;
        }
    }

    return shadow / pcf_itr_con;
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
	float s = ((sc / abs(ma)) + 1) * 0.5;
	float t = ((tc / abs(ma)) + 1) * 0.5;
	return vec2(s, t);
}

float computeShadowForSide(int light, vec3 light2frag, vec3 norm, int side, int offset)
{
	vec2 coords = project2CubemapTexture(light2frag, side);
	float current_depth = length(light2frag) / pl_cutout[light].x;
	float bias = 0.0;  
	//float shadow_depth = texture(pl_shadow_maps, vec4(coords.x, coords.y, offset + side, current_depth - bias));
	float shadow_depth = getShadowDepth2DArray(pl_shadow_maps, vec3(coords.x, coords.y, offset + side), current_depth - bias);
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
    float negx = step(r.x, 0.0);
    float negy = step(r.y, 0.0);
    float negz = step(r.z, 0.0);

    uvw = 
        bx ? vec3(r.zy, absr.x) * vec3(mix(-1.0, 1.0, negx), -1.0, 1.0) : 
        by ? vec3(r.xz, absr.y) * vec3(1.0, mix(1.0, -1.0, negy), 1.0) : 
             vec3(r.xy, absr.z) * vec3(mix(1.0, -1.0, negz), -1.0, 1.0);

    return vec3(vec2(uvw.xy / uvw.z + 1.0) * 0.5, bx ? negx : by ? negy + 2.0 : negz + 4.0);
}

float texCubemap(vec3 uvw, float offset, float currentDepth) 
{
    vec3 st = cubemap(uvw);
    st.z += offset;
    //return texture(pl_shadow_maps, vec4(st, currentDepth));
    return getShadowDepth2DArray(pl_shadow_maps, st, currentDepth);
}

float computeShadow(int light, vec3 light2frag, vec3 norm)
{
	vec3 norm_l2f = normalize(light2frag);
    float current_depth = length(light2frag) / pl_cutout[light].x;
    float bias = 0.0;
    return clamp(texCubemap(norm_l2f, pl_index[light] * 6, current_depth - bias), 0, 1);
}
#endif

vec3 calcLight(vec3 world_to_light, vec3 radiance, vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness)
{
    vec3 F0 = mix(surface_reflection_for_dielectrics, albedo, metallic);
    vec3 Lh = normalize(world_to_camera + world_to_light);
    float cosLi = max(0.0, dot(normal, world_to_light));
	float cosLh = max(0.0, dot(normal, Lh));
    float cosLo = max(0.0, dot(normal, world_to_camera));
    vec3 F = fresnelSchlick(max(0.0, dot(Lh, world_to_camera)), F0);
	float D = ndfGGX(cosLh, roughness);
	float G = gaSchlickGGX(cosLi, cosLo, roughness);
    vec3 kd = mix(vec3(1.0) - F, vec3(0.0), metallic);
    vec3 diffuseBRDF = kd * albedo;
    vec3 specularBRDF = (F * D * G) / max(eff_epsilon, 4.0 * cosLi * cosLo);
    return (diffuseBRDF + specularBRDF) * radiance * cosLi;

}


vec3 calcPointLight(int light_index, vec3 f_world_position, vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness)
{
    vec3 world_to_light = normalize(pl_position[light_index] - f_world_position);
	float light_distance = length(pl_position[light_index] - f_world_position);

    float attenuation = pl_cutout[light_index].x / (light_distance * light_distance * PI * PI * 4);
    vec3 radiance = pl_color[light_index] * attenuation;
#ifndef GL_SPIRV
#ifdef HAS_POINT_SHADOWS
#ifndef BRANCHING
    float shadow = computeShadow(light_index, f_world_position - pl_position[light_index], normal);
#else
    float shadow = pl_cutout[light_index].y < eff_epsilon ? 1.0 : computeShadow(light_index, f_world_position - pl_position[light_index], normal);
#endif
#else
    float shadow = 1.0;
#endif
#else
    float shadow = has_point_shadows == 1 ? pl_cutout[light_index].y < eff_epsilon ? 1.0 : computeShadow(light_index, f_world_position - pl_position[light_index], normal) : 1.0;
#endif
    return calcLight(world_to_light, radiance, world_to_camera, albedo, normal, metallic, roughness) * shadow;
}

float calcDirectionalShadows(vec3 surface_normal, vec3 world_position)
{
#ifndef GL_SPIRV
#ifdef HAS_DIRECTIONAL_SHADOWS
    vec4 sun_coord = sun_projection * sun_view * vec4(world_position.xyz, 1.0);
    float zMod = max(0, floor(sun_coord.z - 1.0));
    vec3 proj_coords = sun_coord.xyz / sun_coord.w;
    proj_coords = proj_coords * 0.5 + 0.5; 
    float currentDepth = proj_coords.z;
    float cosTheta = dot(surface_normal, -dl_direction);
    float bias = clamp(0.003 * tan(acos(cosTheta)), 0.0, 0.01);
    //float result = texture(dl_shadow_map, vec3(proj_coords.xy, currentDepth - bias));
    float result = getShadowDepth2D(dl_shadow_map, proj_coords.xy, currentDepth - bias);
    return result;
#else
    return 1.0;
#endif
#else
    if (has_dir_shadows == 1)
    {
        vec4 sun_coord = sun_projection * sun_view * vec4(world_position.xyz, 1.0);
        float zMod = max(0, floor(sun_coord.z - 1.0));
        vec3 proj_coords = sun_coord.xyz / sun_coord.w;
        proj_coords = proj_coords * 0.5 + 0.5; 
        float currentDepth = proj_coords.z;
        float cosTheta = dot(surface_normal, -dl_direction);
        float bias = clamp(0.003 * tan(acos(cosTheta)), 0.0, 0.01);
        float result = getShadowDepth2D(dl_shadow_map, proj_coords.xy, currentDepth - bias);
        return result;
    }
    else
    {
        return 1.0;
    }
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

    float mulR = float(yIdx <= 0.25);
    float mulG = float(yIdx <= 0.5 && yIdx > 0.25);
    float mulB = float(yIdx <= 0.75 && yIdx > 0.5);
    float mulA = float(yIdx > 0.75);

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
    
    gl_FragDepth = texture(g_depth, f_texture).r;
    #endif
    vec3 world_to_camera = normalize(camera_position - world_position.rgb);
    vec4 aomrg = texture(g_aomrg, f_texture);
    vec3 albedo = texture(g_albedo, f_texture).rgb;
    vec3 normal = getNormalFromMap();
    vec3 emissive = vec3(0.0);
    float ao = aomrg.r;
    float m = aomrg.g;
    float r = aomrg.b;
    float g = aomrg.a;

    vec3 light_colors = vec3(0.0, 0.0, 0.0);
    for (int i = 0; i < pl_num; ++i)
    {
        light_colors += calcPointLight(i, world_position.rgb, world_to_camera, albedo, normal, m, r);
    }

    vec3 ambient = al_color * albedo * ao;
	vec3 color = ambient + light_colors + calcDirectionalLight(world_position.rgb, world_to_camera, albedo, normal, m, r, ao) + emissive;
	vec4 grid = getGrid(g);
    color = mix(color, grid.rgb, grid.a);
    color = color + texture(g_emission, f_texture).rgb;
    color.rgb = pow(color.rgb, vec3(1.0/gamma_factor));

#ifndef BRANCHING
    float fowVal = getFowMultiplier(world_position.rgb) * fow_mod + (1.0 * (1.0 - fow_mod));
    g_color = vec4(mix(sky_color, color, fowVal), world_position.a);
#else
    if (fow_mod > eff_epsilon)
    {
        float fowVal = getFowMultiplier(world_position.rgb) * fow_mod + (1.0 * (1.0 - fow_mod));
        g_color = vec4(mix(sky_color, color, fowVal), world_position.a);
    }
    else
    {
        g_color = vec4(color, world_position.a);
    }
#endif
}