#version 330 core

layout (location = 0) in vec3 v_position;
layout (location = 1) in vec2 v_texture;
layout (location = 2) in vec3 v_normal;
layout (location = 3) in vec3 v_tangent;
layout (location = 4) in vec3 v_bitangent;
layout (location = 5) in vec4 v_color;

// Camera
uniform mat4 model;
uniform mat4 mvp;

// Sun Shadows
uniform mat4 sun_view;
uniform mat4 sun_projection;

// Scene data
uniform uint frame;
uniform uint update;

// Lights data
// TODO lights

out mat3 f_tbn;
out vec3 f_normal;
out vec3 f_tangent;
out vec3 f_bitangent;
out vec3 f_world_position;
out vec3 f_position;
out vec4 f_color;
out vec2 f_texture;
out vec4 f_sun_coord;

void main()
{
	vec3 world_tan = normalize(vec3(model * vec4(v_tangent, 0.0)));
	vec3 world_bitan = normalize(vec3(model * vec4(v_bitangent, 0.0)));
	vec3 world_normal = normalize(vec3(model * vec4(v_normal, 0.0)));
	f_tbn = mat3(world_tan, world_bitan, world_normal);
	f_position = v_position;
	f_world_position = vec3(model * vec4(v_position, 1.0));
	f_color = v_color;
	f_texture = v_texture;
	f_normal = v_normal;
	f_tangent = v_tangent;
	f_bitangent = v_bitangent;
	f_sun_coord = sun_projection * sun_view * vec4(f_world_position, 1.0);
	gl_Position = mvp * vec4(v_position, 1.0);
}