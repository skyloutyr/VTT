#version 330 core
layout (triangles) in; 
layout (triangle_strip, max_vertices=18) out;

uniform mat4 projView[6];
uniform int layer_offset;

out vec4 frag_pos;

void emitFace(int index)
{
	for (int j = 0; j < 3; ++j)
	{
		frag_pos = gl_in[j].gl_Position;
		gl_Position = projView[index] * frag_pos;
		EmitVertex();
	}

	EndPrimitive();
}

void main()
{
	gl_Layer = layer_offset;
	emitFace(0);
	gl_Layer = layer_offset + 1;
	emitFace(1);
	gl_Layer = layer_offset + 2;
	emitFace(2);
	gl_Layer = layer_offset + 3;
	emitFace(3);
	gl_Layer = layer_offset + 4;
	emitFace(4);
	gl_Layer = layer_offset + 5;
	emitFace(5);
}