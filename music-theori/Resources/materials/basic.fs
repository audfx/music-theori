#version 330
#extension GL_ARB_separate_shader_objects : enable

layout (location = 1) in vec2 frag_TexCoord;
layout (location = 0) out vec4 target;

uniform sampler2D MainTexture;
uniform vec4 Color;

uniform vec4 TempMappedTextureCoords = vec4(0, 0, 1, 1);

void main()
{
	vec4 t = TempMappedTextureCoords;
	vec2 c = frag_TexCoord;

	vec2 newCoord = vec2(t.x + t.z * c.x, t.y + t.w * c.y);

	vec4 mainColor = texture(MainTexture, newCoord);
	target = mainColor * Color;
}