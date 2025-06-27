#version 330 core
in vec3 f_data_pos;

out vec4 color;

void main()
{
    float x = 1.0 - length(f_data_pos);
    float m = min(1.0, max(0.4, pow(x + 0.51, 10)) - 0.4);
    float d = (1.0 / (1.0 + x) * m + (x / 2.0));
    color = vec4(1, 1, 1, d);
}