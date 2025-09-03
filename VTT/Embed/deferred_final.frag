#version 330 core

#define SHADOW_BIAS_MAX 0.01
#define SHADOW_BIAS_MIN 0.0005

#define PCF_ITERATIONS 2
#define HAS_POINT_SHADOWS
#define HAS_DIRECTIONAL_SHADOWS

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

uniform sampler2D g_positions;
uniform sampler2D g_normals;
uniform sampler2D g_albedo;
uniform sampler2D g_aomrg;
uniform sampler2D g_emission;

// binding 4 is empty
// binding 5 is empty
// Skybox
uniform sampler2DArray tex_skybox; // binding 6
// bindings [7..11] are empty
// Custom shader
uniform sampler2DArray unifiedTexture; // binding 12
// Point lights
uniform sampler2DArray pl_shadow_maps; // binding 13
// Directional light
uniform sampler2DArrayShadow dl_shadow_map; // binding 14
// Fog of war
uniform usampler2D fow_texture; // binding 15

uniform bool gamma_correct;
uniform float gamma_factor;

out layout (location = 0) vec4 g_color;

const vec3 surface_reflection_for_dielectrics = vec3(0.04);
const float PI = 3.14159265359;
const float eff_epsilon = 0.00001;

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

float getSunShadowDepth(vec3 f_position, vec3 normal, vec3 lightDir)
{
    float pcf_itr_con = 0;
    vec4 fragPosViewSpace = view * vec4(f_position, 1.0);
	float depthValue = abs(fragPosViewSpace.z);
	vec4 res = step(vec4(cascadePlaneDistances[0], cascadePlaneDistances[1], cascadePlaneDistances[2], cascadePlaneDistances[3]), vec4(depthValue));
	int layer = depthValue < cascadePlaneDistances[3] ? int(res.x + res.y + res.z + res.w) : 4;
	vec4 fragPosLightSpace = sunCascadeMatrices[layer] * vec4(f_position, 1.0);
	vec3 proj_coords = fragPosLightSpace.xyz / fragPosLightSpace.w;
	proj_coords = proj_coords * 0.5 + 0.5;
	float bias = max(0.0004 * (1.0 - dot(normal, lightDir)), 0.0001);
	float currentDepth = proj_coords.z - bias;
	float ret = 0.0;
	vec2 texelSize = 1.0 / textureSize(dl_shadow_map, 0).xy;
	for (float y = -PCF_ITERATIONS; y <= PCF_ITERATIONS; ++y)
	{
		for (float x = -PCF_ITERATIONS; x <= PCF_ITERATIONS; ++x)
		{
			float pcfDepth = texture(dl_shadow_map, vec4(proj_coords.xy + vec2(x, y) * texelSize, layer, currentDepth));
			ret += pcfDepth;
            ++pcf_itr_con;
		}
	}

	ret /= pcf_itr_con;
	return currentDepth > 1.0 ? 1.0 : ret;
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
    return getShadowDepth2DArray(pl_shadow_maps, st, currentDepth);
}

const vec2 cubemap_offsets[6] = vec2[6](
    vec2(0.0, 1.0 / 3.0),   
    vec2(0.5, 1.0 / 3.0),   
    vec2(0.75, 1.0 / 3.0),  
    vec2(0.25, 1.0 / 3.0),   
    vec2(0.25, 0),          // OK
    vec2(0.25, 2.0 / 3.0)   // OK
);

const vec2 cubemap_factor = vec2(0.25, 1.0 / 3.0);

vec4 sampleSkybox(vec3 v, vec4 frameData, int index)
{
    int i = int(v.z);
    vec2 t_base = vec2(0, 0);
    switch (i)
    {
        case 0:
            t_base = vec2(v.y, v.x);
            break;

        case 1:
            t_base = vec2(1.0 - v.y, 1.0 - v.x);
            break;

        case 2:
            t_base = vec2(v.x, 1.0 - v.y);
            break;

        case 3:
            t_base = vec2(1.0 - v.x, v.y);
            break;

        case 4:
            t_base = vec2(1.0 - v.x, v.y);
            break;

        case 5:
            t_base = vec2(v.x, 1.0 - v.y);
            break;

        default:
            break;
    }

    vec2 position_base = clamp(t_base, vec2(0.001, 0.001), vec2(0.999, 0.999)) * cubemap_factor + cubemap_offsets[i];
	position_base = position_base * frameData.zw + frameData.xy;
    return texture(tex_skybox, vec3(position_base, float(index)));
    //return vec4(t_base, 0.0, 1.0);
}

vec3 computeSkyboxColor(vec3 v)
{
    v = cubemap(v);
	vec4 day = sampleSkybox(v, skybox_animation_day, 0) * vec4(unpackRgba(skybox_colors_blend_pl_num.x).rgb, 1.0);
    vec4 night = sampleSkybox(v, skybox_animation_night, 1) * vec4(unpackRgba(skybox_colors_blend_pl_num.y).rgb, 1.0);
    return mix(day, night, skybox_colors_blend_pl_num.z).rgb;
}

float computeShadow(int light, float cutout, vec3 light2frag, vec3 norm)
{
    vec3 norm_l2f = normalize(light2frag);
    float current_depth = length(light2frag) / cutout;
    float bias = 0.0;
    return texCubemap(norm_l2f, 0, current_depth - bias);
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
    vec3 kd = mix(vec3(1.0) - F, computeSkyboxColor(reflect(-world_to_camera, normal)), metallic);
    vec3 diffuseBRDF = kd * albedo;
    vec3 specularBRDF = (F * D * G) / max(eff_epsilon, 4.0 * cosLi * cosLo);
    return (diffuseBRDF + specularBRDF) * radiance * cosLi;
}

vec3 calcPointLight(int light_index, vec3 f_world_position, vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness)
{
    vec4 light_data = pl_positions_color[light_index];
    vec3 pl_position = light_data.xyz;
    vec4 pl_color = unpackRgba(light_data.w);
    vec3 world_to_light = normalize(pl_position - f_world_position);
    float light_distance = length(pl_position - f_world_position);
    float pl_cutout = pl_cutouts[light_index >> 2][light_index & 3];
    float attenuation = abs(pl_cutout) / (light_distance * light_distance * PI * PI * 4);
    vec3 radiance = pl_color.xyz * attenuation;
#ifdef HAS_POINT_SHADOWS
    float shadow = sign(pl_cutout) < eff_epsilon ? 1.0 : computeShadow(light_index, abs(pl_cutout), f_world_position - pl_position, normal);
#else
    float shadow = 1.0;
#endif
    return calcLight(world_to_light, radiance, world_to_camera, albedo, normal, metallic, roughness) * shadow;
}

float calcDirectionalShadows(vec3 world_position, vec3 normal, vec3 lightDir)
{
#ifdef HAS_DIRECTIONAL_SHADOWS
    return getSunShadowDepth(world_position, normal, lightDir);
#else
    return 1.0;
#endif
}

vec3 calcDirectionalLight(vec3 world_position, vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness, float ao)
{
    vec3 dl_direction = unpackNorm101010(camera_position_sundir.w);
    return calcLight(-dl_direction, unpackRgba(camera_direction_sunclr.w).rgb, world_to_camera, albedo, normal, metallic, roughness) * calcDirectionalShadows(world_position, normal, -dl_direction);
}

vec3 getNormalFromMap()
{
    return texture(g_normals, f_texture).xyz;
}

vec4 getGrid(float g)
{
	return vec4(unpackRgba(cursor_position_gridclr.w).rgb, g);
}

float getFowMultiplier(vec3 f_world_position)
{   
    vec2 fow_scale = vec2(fow_scale_mod.x);
    vec2 fow_offset = vec2(0.5) + floor(fow_scale * 0.5);
    vec2 uv_fow_world = (f_world_position.xy + fow_offset) / fow_scale;
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
    if (gl_FragDepth >= 1.0 - eff_epsilon)
    {
        discard;
    }
    
    vec3 world_to_camera = normalize(camera_position_sundir.xyz - world_position.rgb);
    vec4 aomrg = texture(g_aomrg, f_texture);
    vec3 albedo = texture(g_albedo, f_texture).rgb;
    vec3 normal = getNormalFromMap();
    vec3 emissive = vec3(0.0);
    float ao = aomrg.r;
    float m = aomrg.g;
    float r = aomrg.b;
    float g = aomrg.a;

    vec3 light_colors = vec3(0.0, 0.0, 0.0);
    int pl_num = floatBitsToInt(skybox_colors_blend_pl_num.w);
    for (int i = 0; i < pl_num; ++i)
    {
        light_colors += calcPointLight(i, world_position.rgb, world_to_camera, albedo, normal, m, r);
    }

    vec3 ambient = unpackRgba(al_sky_colors_viewportsz.x).rgb * albedo * ao;
	vec3 color = ambient + light_colors + calcDirectionalLight(world_position.rgb, world_to_camera, albedo, normal, m, r, ao) + emissive;
	vec4 grid = getGrid(g);
    color = mix(color, grid.rgb, grid.a);
    color = color + texture(g_emission, f_texture).rgb;
    if (gamma_correct)
    {
        color.rgb = pow(color.rgb, vec3(1.0 / gamma_factor));
    }

    float fow_mod = fow_scale_mod.y;
    if (fow_mod > eff_epsilon)
    {
        float fowVal = getFowMultiplier(world_position.rgb) * fow_mod + (1.0 * (1.0 - fow_mod));
        g_color = vec4(mix(unpackRgba(al_sky_colors_viewportsz.y).rgb, color, fowVal), world_position.a);
    }
    else
    {
        g_color = vec4(color, world_position.a);
    }
    
    // vec3 dl_direction = unpackNorm101010(camera_position_sundir.w);
    // g_color.rgb = color = vec3(dot(normal, -dl_direction));
}