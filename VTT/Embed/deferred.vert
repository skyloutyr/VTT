#version 330 core
layout (location = 0) in vec3 v_position;
layout (location = 1) in vec2 v_texture;
layout (location = 2) in vec3 v_normal;
layout (location = 3) in vec3 v_tangent;
layout (location = 4) in vec3 v_bitangent;
layout (location = 5) in vec4 v_color;

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

uniform mat4 model;
uniform mat4 mvp;

out mat3 f_tbn;
out vec3 f_position;
out vec3 f_normal;
out vec3 f_tangent;
out vec3 f_bitangent;
out vec3 f_world_position;
out vec4 f_color;
out vec2 f_texture;

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
	gl_Position = mvp * vec4(v_position, 1.0);
}