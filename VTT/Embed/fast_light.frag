#version 330 core

in vec4 world_frag;
flat in vec4 light_position;

uniform sampler2D g_positions;
uniform sampler2D g_normals;
uniform sampler2D g_albedo;
uniform sampler2D g_aomrg;
uniform sampler2D g_emission;
uniform sampler2D g_depth;

uniform vec4 light_color;
uniform vec2 viewport_size;
uniform vec3 camera_position;

out layout (location = 0) vec4 g_color;

const float PI = 3.14159265359;
const vec3 surface_reflection_for_dielectrics = vec3(0.04);
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

vec3 calcPointLight(vec3 f_world_position, vec3 world_to_camera, vec3 albedo, vec3 normal, float metallic, float roughness)
{
    vec3 world_to_light = normalize(light_position.xyz - f_world_position);
	float light_distance = length(light_position.xyz - f_world_position);
    float attenuation = max(0, light_color.a - light_distance) / light_color.a;
    vec3 radiance = light_color.rgb * attenuation;
    return calcLight(world_to_light, radiance, world_to_camera, albedo, normal, metallic, roughness);
}

void main()
{
    vec2 f_screen_pos = gl_FragCoord.xy / viewport_size;
    vec3 albedo = texture(g_albedo, f_screen_pos).rgb;
    vec3 normal = texture(g_normals, f_screen_pos).xyz;
    vec4 aomrg = texture(g_aomrg, f_screen_pos);
    vec3 pos = texture(g_positions, f_screen_pos).xyz;
    vec3 world_to_camera = normalize(camera_position - pos);
    float m = aomrg.g;
    float r = aomrg.b;

    g_color = vec4(calcPointLight(pos, world_to_camera, albedo, normal, m, r), 1.0);
}