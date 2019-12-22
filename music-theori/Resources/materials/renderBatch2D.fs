#version 330
#extension GL_ARB_separate_shader_objects : enable

layout (location = 0) flat in int frag_PaintIndex;
layout (location = 1) in vec2 frag_Position;
layout (location = 2) in vec2 frag_TexCoord;
layout (location = 3) in vec4 frag_Color;

layout (location = 0) out vec4 target;

struct Paint
{
	int PaintType; // 0 image
	int TexId;
	mat3x2 PaintMatrix;
};

uniform Paint Paints[16];
uniform sampler2D Textures[16];
uniform vec2 ViewportSize;

vec2 TransformScreenToPaint(vec2 screen, mat3x2 paintMatrix)
{
	return screen;
}

void main()
{
	vec4 color;
	
	if (frag_PaintIndex == 32) // fill color
	{
		color = vec4(1);
	}
	else if (frag_PaintIndex >= 16)  // screen space paints
	{
		Paint paint = Paints[frag_PaintIndex - 16];
		if (paint.PaintType == 0) // image paint
		{
			vec2 tcoord = TransformScreenToPaint(frag_Position / ViewportSize, paint.PaintMatrix);
			vec4 texColor = texture(Textures[paint.TexId], tcoord);

			color = texColor;
		}
	}
	else // regular textures
	{
		vec4 texColor = texture(Textures[frag_PaintIndex], frag_TexCoord);
		color = texColor;
	}
	
	target = color * frag_Color;
}
