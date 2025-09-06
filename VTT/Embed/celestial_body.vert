#version 330 core

#ifdef USE_VTX_COMPRESSION
layout (location = 0) in vec4 v_position_padding; // xyz position, w unused
layout (location = 1) in vec4 v_texture_normal_tangent; // xy texture, z is int_14_14_4 normal, w is int_14_14_4 tangent
layout (location = 2) in vec4 v_bones_weights_color; // xy is bone indices (2xuint16_16), z is a int10_10_10_2 weights, w is int_8_8_8_8 color
#else
layout (location = 0) in vec3 v_position; // Has to be a vec3f, can't do compression
layout (location = 1) in vec2 v_texture; // Could be compressed to 2x16bit component vector, but if we want to support texture ranges outside of 0-1, then must be a vec2f
layout (location = 2) in vec3 v_normal; // Could be compressed to a int_14_14_4, if a normalize() is an acceptable instruction
layout (location = 3) in vec3 v_tangent; // Could be compressed to a int_14_14_4, if a normalize() is an acceptable instruction
layout (location = 4) in vec3 v_bitangent; // Can be entirely omitted if normalize(cross(normal, tangent)) is acceptable
layout (location = 5) in vec4 v_color; // Can be compressed to a int8_8_8_8
layout (location = 6) in vec4 v_weights; // Depending on the precision desired can be compressed to a int10_10_10_2?
layout (location = 7) in vec2 v_bones; // Already compressed
#endif

uniform mat4 mvp;

out vec4 f_color;
out vec2 f_texture;

vec4 unpackRgba(float packedRgbaVal)
{
	const float oneOver255 = 1.0 / 255.0;
    uint packed_ui = floatBitsToUint(packedRgbaVal);
    return vec4(
        float((packed_ui >> 24) & 0xffu),
        float((packed_ui >> 16) & 0xffu),
        float((packed_ui >> 8) & 0xffu),
        float(packed_ui & 0xffu)
    ) * oneOver255;
}

void main()
{
#ifdef USE_VTX_COMPRESSION
    vec3 v_position = v_position_padding.xyz;
    vec2 v_texture = v_texture_normal_tangent.xy;
    vec4 v_color = unpackRgba(v_bones_weights_color.w);
#endif

    f_color = v_color;
    f_texture = v_texture;
    gl_Position = mvp * vec4(v_position, 1.0);
}