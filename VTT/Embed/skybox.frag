#version 330 core
in vec3 f_tex;

layout (location = 0) out vec4 o_color;

uniform sampler2DArray tex_skybox;
uniform vec4 animation_day;
uniform vec4 animation_night;
uniform float daynight_blend;
uniform vec3 day_color;
uniform vec3 night_color;

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

void main()
{
    vec3 v = cubemap(f_tex);
	vec4 day = sampleSkybox(v, animation_day, 0) * vec4(day_color, 1.0);
    vec4 night = sampleSkybox(v, animation_night, 1) * vec4(night_color, 1.0);
    o_color = mix(day, night, daynight_blend);
}