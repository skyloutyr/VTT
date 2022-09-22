#version 330 core
uniform vec3 s_direction;

in vec3 f_data_pos;

out vec4 color;

void main()
{
    float x = 1.0f - length(f_data_pos);
    float m = min(1.0f, max(0.4f, pow(x + 0.51f, 10)) - 0.4f);
    float d = (1.0f / (1.0f + x) * m + (x / 2.0f));
    color = vec4(1, 1, 1, d);
}