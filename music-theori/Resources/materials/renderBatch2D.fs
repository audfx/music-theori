#version 460
#extension GL_ARB_separate_shader_objects : enable

layout (location = 0) flat in int frag_RenderKind; // [0,16) Paint, 16 Texture, 17 Solid Color
layout (location = 1) in vec2 frag_Position;
layout (location = 2) in vec2 frag_TexCoord;
layout (location = 3) in vec4 frag_Color;

layout (location = 0) out vec4 target;

struct Paint
{
	int PaintIndex; 
};

// paints can reference textures
uniform Paint Paints[16];
// textures, whether used as fill or paint
uniform sampler2D Texture;
uniform vec2 ViewportSize;

void main()
{
	vec4 color;
	
	int rk = frag_RenderKind;
	if (rk == 17) // fill color
	{
		color = vec4(1);
	}
	else if (rk == 16)  // local space textures
	{
		vec4 texColor = texture(Texture, frag_TexCoord);
		color = texColor;
	}
	else // screen space paints
	{
		color = vec4(1, 0, 1, 1);
	}
	
	target = color * frag_Color;
}
