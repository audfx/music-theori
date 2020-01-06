#version 460
#extension GL_ARB_separate_shader_objects : enable

layout (location = 0) in int in_RenderKind;
layout (location = 1) in vec2 in_Position;
layout (location = 2) in vec2 in_TexCoord;
layout (location = 3) in vec4 in_Color;

out gl_PerVertex
{
	vec4 gl_Position;
};

layout (location = 0) flat out int frag_RenderKind;
layout (location = 1) out vec2 frag_Position;
layout (location = 2) out vec2 frag_TexCoord;
layout (location = 3) out vec4 frag_Color;

uniform mat4 Projection;
uniform mat4 Camera;
uniform mat4 World;

void main()
{
	frag_RenderKind = in_RenderKind;
	frag_Position = in_Position;
	frag_TexCoord = in_TexCoord;
	frag_Color = in_Color;

	gl_Position = Projection * Camera * World * vec4(in_Position.xy, 0, 1);
}