#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

matrix WorldViewProjection;

float3 LightDirection;
float AmbientIntensity;

float TextureOffset;

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float2 TexCoords : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoords : TEXCOORD0;
};

Texture ColorMap;
sampler ColorMapSampler = sampler_state {
	texture = <ColorMap>;
	Filter = ANISOTROPIC;
	MinFilter = Anisotropic;
	MagFilter = Anisotropic;
	MipFilter = Linear;
	MaxAnisotropy = 16;
	AddressU = Wrap;
	AddressV = Wrap;
};

Texture NormalMap;
sampler NormalMapSampler = sampler_state {
	texture = <NormalMap>;
	Filter = ANISOTROPIC;
	MinFilter = Anisotropic;
	MagFilter = Anisotropic;
	MipFilter = Linear;
	MaxAnisotropy = 16;
	AddressU = Wrap;
	AddressV = Wrap;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

	output.Position = mul(input.Position, WorldViewProjection);
	output.TexCoords = input.TexCoords;
	output.TexCoords.y += TextureOffset;

	return output;
}

float2x2 RotationMatrix(float rotation)
{
	float c = cos(rotation);
	float s = sin(rotation);

	return float2x2(c, -s, s ,c);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	float4 color = tex2D(ColorMapSampler, input.TexCoords);
	color = lerp(color, tex2D(ColorMapSampler, mul(-input.TexCoords / 6, RotationMatrix(90))), 0.5);

	float3 normalMap = tex2D(NormalMapSampler, input.TexCoords);
	normalMap = lerp(normalMap, tex2D(NormalMapSampler, mul(-input.TexCoords / 6, RotationMatrix(90))), 0.5);

	normalMap = 2.0 * normalMap - float3(0.5, 0.5, 0.5);
	normalMap = normalize(normalMap);
	float4 normal = float4(normalMap, 1.0);

	float4 diffuse = saturate(dot(normal, normalize(LightDirection)));

	color = saturate(color * max(diffuse, AmbientIntensity));
	color.a = 0.9;

	return color;
}

technique BasicColorDrawing
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};