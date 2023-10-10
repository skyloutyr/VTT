#version 330 core

#define SHADOW_BIAS_MAX 0.01
#define SHADOW_BIAS_MIN 0.0005

#define PCF_ITERATIONS 2
#define HAS_POINT_SHADOWS
#define HAS_DIRECTIONAL_SHADOWS

#undef NODEGRAPH

in mat3 f_tbn;
in vec3 f_normal;
in vec3 f_tangent;
in vec3 f_bitangent;
in vec3 f_world_position;
in vec3 f_position;
in vec4 f_color;
in vec2 f_texture;
in vec4 f_sun_coord;

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

uniform float alpha;

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
uniform uint material_index;

uniform vec4 tint_color;

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

// grid
uniform float grid_alpha;

// extra textures for custom shaders
uniform sampler2DArray unifiedTexture;
uniform vec2 unifiedTextureData[64];
uniform vec4 unifiedTextureFrames[64];

layout (location = 0) out vec4 g_color; // no writing here occurs, needed for consistency
layout (location = 1) out vec4 g_position;
layout (location = 2) out vec4 g_normal;
layout (location = 3) out vec4 g_albedo;
layout (location = 4) out vec4 g_aomrg;
layout (location = 5) out vec4 g_emission;

const vec3 surface_reflection_for_dielectrics = vec3(0.04);
const float PI = 3.14159265359;
const float eff_epsilon = 0.00001;

vec4 sampleMapCustom(sampler2D sampler, vec2 uvs, vec4 frameData)
{
    return texture(sampler, uvs * frameData.zw + frameData.xy);
}

vec4 sampleMap(sampler2D sampler, vec4 frameData)
{
    return sampleMapCustom(sampler, f_texture, frameData);
}

vec4 sampleExtraTexture(int layer, vec2 uvs)
{
    ivec3 ts = textureSize(unifiedTexture, 0);
    vec2 f_uvs = uvs * unifiedTextureData[layer] / vec2(ts.x, ts.y);
    vec4 frameData = unifiedTextureFrames[layer];
    f_uvs = f_uvs * frameData.zw + frameData.xy;
    return texture(unifiedTexture, vec3(f_uvs, layer));
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
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

float ndfGGX(float cosLh, float roughness)
{
	float alpha = roughness * roughness;
	float alphaSq = alpha * alpha;
	float denom = (cosLh * cosLh) * (alphaSq - 1.0) + 1.0;
	return alphaSq / (PI * denom * denom);
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
    float bias = 0;
    return texCubemap(norm_l2f, pl_index[light] * 6, current_depth - bias);
}

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


vec3 calcPointLight(int light_index, vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness)
{
    vec3 world_to_light = normalize(pl_position[light_index] - f_world_position);
	float light_distance = length(pl_position[light_index] - f_world_position);

    float attenuation = pl_cutout[light_index].x / (light_distance * light_distance * PI * PI * 4);
    vec3 radiance = pl_color[light_index] * attenuation;
#ifdef HAS_POINT_SHADOWS
    float shadow = pl_cutout[light_index].y < eff_epsilon ? 1.0 : computeShadow(light_index, f_world_position - pl_position[light_index], normal);
#else
    float shadow = 1.0;
#endif
    return calcLight(world_to_light, radiance, world_to_camera, albedo, normal, metallic, roughness) * shadow;
}

float calcDirectionalShadows(vec3 surface_normal)
{
#ifdef HAS_DIRECTIONAL_SHADOWS
    float zMod = max(0, floor(f_sun_coord.z - 1.0));
    vec3 proj_coords = f_sun_coord.xyz / f_sun_coord.w;
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
}

vec3 calcDirectionalLight(vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness, float ao)
{
    return calcLight(-dl_direction, dl_color, world_to_camera, albedo, normal, metallic, roughness) * calcDirectionalShadows(normal);
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

const float cutoff = 0.8;
const vec3 unitZ = vec3(0.0, 0.0, 1.0);
vec4 getGrid()
{
    vec3 normal = f_tbn[2];
    float d = dot(normal, unitZ);
    d = float(abs(d) >= 0.45);

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

	return vec4(grid_color.xyz, max(0, (grid_color.a * gmx * grid_alpha * d) - world_distance_to_cursor_effect));
}

float getFowMultiplier()
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

void shaderGraph(out vec3 albedo, out vec3 normal, out vec3 emissive, out float ao, out float m, out float r, out float a)
{
#ifndef NODEGRAPH
    vec4 albedo_tex = sampleMap(m_texture_diffuse, m_diffuse_frame);
    albedo = albedo_tex.rgb * tint_color.rgb;
    normal = getNormalFromMap();
    vec3 aomr = sampleMap(m_texture_aomr, m_aomr_frame).rgb;
    emissive = sampleMap(m_texture_emissive, m_emissive_frame).rgb;
    ao = aomr.r;
    m = aomr.g;
    r = aomr.b;
    a = alpha * tint_color.a * albedo_tex.a;
#else
#pragma ENTRY_NODEGRAPH
#endif
}

void main()
{
    vec3 world_to_camera = normalize(camera_position - f_world_position);
    vec3 albedo = vec3(0.0, 0.0, 0.0);
    vec3 normal = vec3(0.0, 0.0, 0.0);
    vec3 emissive = vec3(0.0, 0.0, 0.0);
    float ao = 0.0;
    float m = 0.0;
    float r = 0.0;
    float l_a = 0.0;
    shaderGraph(albedo, normal, emissive, ao, m, r, l_a);
    vec4 grid = vec4(0.0, 0.0, 0.0, 0.0);
    if (grid_alpha > eff_epsilon)
    {
        grid = getGrid();
    }

    g_position = vec4(f_world_position, 1.0);
    g_normal = vec4(normal, 1.0);
    g_albedo = vec4(albedo, 1.0);
    g_aomrg = vec4(ao, m, r, grid.a);
    g_emission = vec4(emissive, 1.0);

    vec3 light_colors = vec3(0.0, 0.0, 0.0);
    for (int i = 0; i < pl_num; ++i)
    {
        light_colors += calcPointLight(i, world_to_camera, albedo, normal, m, r);
    }

    vec3 ambient = al_color * albedo * ao;
	vec3 color = ambient + light_colors + calcDirectionalLight(world_to_camera, albedo, normal, m, r, ao) + emissive;
    color = mix(color, grid.rgb, grid.a);

    color.rgb = pow(color.rgb, vec3(1.0/gamma_factor));

    float a = l_a;
    if (a <= eff_epsilon)
    {
        discard;
    }

    if (fow_mod > eff_epsilon)
    {
        float fowVal = getFowMultiplier() * fow_mod + (1.0 * (1.0 - fow_mod));
        g_color = vec4(mix(sky_color, color, fowVal), a);
    }
    else
    {
        g_color = vec4(color, a);
    }
}