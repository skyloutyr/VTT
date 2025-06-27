#version 330 core

#undef NODEGRAPH
#undef VTTGLSLIMPL
#define VTT_PKIND_PARTICLE

in vec4 inst_color;
in vec4 f_color;
in vec2 f_texture;
in vec3 f_world_position;
flat in int f_frame;
flat in int inst_id;
flat in float inst_lifespan;

uniform uint frame;
uniform uint update;

uniform vec4 m_diffuse_color;
uniform sampler2D m_texture_diffuse;
uniform vec4 m_diffuse_frame;

// FOW
uniform usampler2D fow_texture;
uniform vec2 fow_offset;
uniform vec2 fow_scale;
uniform float fow_mod;
uniform bool do_fow;

uniform vec3 sky_color;

uniform float gamma_factor;

out vec4 g_color;

// Custom shader boilerplate
uniform sampler2D m_texture_aomr;
uniform sampler2D m_texture_emissive;
uniform vec3 cursor_position;
uniform mat4 view;
uniform mat4 projection;
uniform vec2 viewport_size;

// extra textures for custom shaders
uniform sampler2DArray unifiedTexture;
uniform vec2 unifiedTextureData[64];
uniform vec4 unifiedTextureFrames[64];

// 2D lighting and shadows
uniform sampler2D texture_shadows2d;

// Skybox
uniform sampler2DArray tex_skybox;
uniform vec4 animation_day;
uniform vec4 animation_night;
uniform float daynight_blend;
uniform vec3 day_color;
uniform vec3 night_color;

// Constants not applicable to particle rendering pipeline
const vec4 m_aomr_frame = vec4(0.0, 0.0, 1.0, 1.0);
const vec4 m_emissive_frame = vec4(0.0, 0.0, 1.0, 1.0);
const float m_metal_factor = 0.0;
const float m_roughness_factor = 1.0;
const uint material_index = uint(0);
const float alpha = 1.0;
const vec4 tint_color = vec4(1.0, 1.0, 1.0, 1.0);
const vec3 f_tangent = vec3(1.0, 0.0, 0.0);
const vec3 f_bitangent = vec3(0.0, 1.0, 0.0);
const vec3 f_normal = vec3(0.0, 0.0, 1.0);
const mat3 f_tbn = mat3(
    1.0, 0.0, 0.0,
    0.0, 1.0, 0.0,
    0.0, 0.0, 1.0
);

vec3 getNormalFromMap()
{
    return vec3(0.0, 0.0, 1.0);
}

vec3 getNormalFromMapCustom(vec2 uvs)
{
    return vec3(0.0, 0.0, 1.0);
}

vec4 sampleExtraTexture(int layer, vec2 uvs)
{
    ivec3 ts = textureSize(unifiedTexture, 0);
    vec2 f_uvs = uvs * unifiedTextureData[layer] / vec2(ts.x, ts.y);
    vec4 frameData = unifiedTextureFrames[layer];
    f_uvs = f_uvs * frameData.zw + frameData.xy;
    return texture(unifiedTexture, vec3(f_uvs, layer));
}

vec4 sampleCustomMapAtFrame(sampler2D sampler, vec2 uvs, vec4 frameData, int frame)
{
    ivec2 twh = textureSize(sampler, 0);														        // 32768 x 512
	ivec2 fwh = ivec2(int(frameData.z * twh.x), int(frameData.w * twh.y));					            // 256 x 256
	vec2 fxy = vec2(float((frame * fwh.x) % twh.x), float(((frame * fwh.x) / twh.x) * fwh.y));	        // (256 * frame) % 32768 x [0~256]
	vec2 uv = (fxy / vec2(twh)) + (uvs * frameData.zw);								
	return texture(sampler, uv);
}

vec4 sampleMapCustom(sampler2D sampler, vec2 uvs, vec4 frameData)
{
    return sampleCustomMapAtFrame(sampler, uvs, frameData, f_frame);
}

vec4 sampleMap(sampler2D sampler, vec4 frameData)
{
    return sampleCustomMapAtFrame(sampler, f_texture, frameData, f_frame);
}

vec4 sampleMapAtFrame(sampler2D sampler, vec4 frameData, int frame)
{
    return sampleCustomMapAtFrame(sampler, f_texture, frameData, frame);
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
	vec4 day = sampleSkybox(v, animation_day, 0) * vec4(day_color, 1.0);
    vec4 night = sampleSkybox(v, animation_night, 1) * vec4(night_color, 1.0);
    return mix(day, night, daynight_blend).rgb;
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

#ifdef VTTGLSLIMPL
#pragma ANCHOR_GLSL
#endif

void shaderGraph(out vec3 albedo, out vec3 normal, out vec3 emissive, out float ao, out float m, out float r, out float a)
{
#ifndef NODEGRAPH
    vec4 albedo_tex = sampleMap(m_texture_diffuse, m_diffuse_frame) * f_color * inst_color;
    albedo = albedo_tex.rgb;
    normal = vec3(0.0, 0.0, 1.0);
    emissive = vec3(0.0, 0.0, 0.0);
    ao = 1.0;
    m = 0.0;
    r = 1.0;
    a = albedo_tex.a;
#else
#pragma ENTRY_NODEGRAPH
#endif
}

void main()
{
    vec3 albedo = vec3(0.0, 0.0, 0.0);
    vec3 normal = vec3(0.0, 0.0, 0.0);
    vec3 emissive = vec3(0.0, 0.0, 0.0);
    float ao = 0.0;
    float m = 0.0;
    float r = 0.0;
    float l_a = 0.0;
    shaderGraph(albedo, normal, emissive, ao, m, r, l_a);
	albedo = pow(albedo, vec3(1.0/gamma_factor));
    g_color = vec4(albedo, l_a);
    if (do_fow)
    {
        float fowVal = getFowMultiplier(f_world_position) * fow_mod + (1.0 - fow_mod);
        g_color = mix(vec4(sky_color, g_color.a), g_color, fowVal);
    }
	
    g_color.rgb *= texture(texture_shadows2d, gl_FragCoord.xy / viewport_size).r;
}