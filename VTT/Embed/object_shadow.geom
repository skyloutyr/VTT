#version 330 core
#define NUM_CASCADES 5

layout (triangles) in; 
layout (triangle_strip, max_vertices=3) out;

flat in int self_instance_id[3];

uniform int layer_indices[NUM_CASCADES];
uniform mat4 light_matrices[NUM_CASCADES];

void main()
{
	int l = layer_indices[self_instance_id[0]];
	mat4 pv = light_matrices[l];
	gl_Layer = l;
	gl_Position = pv * gl_in[0].gl_Position;
	EmitVertex();
	gl_Layer = l;
	gl_Position = pv * gl_in[1].gl_Position;
	EmitVertex();
	gl_Layer = l;
	gl_Position = pv * gl_in[2].gl_Position;
	EmitVertex();
	EndPrimitive();
}