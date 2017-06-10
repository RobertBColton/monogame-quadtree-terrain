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

int MapWidth;
int MapHeight;
float TileSize;
float MaxHeight;
float WaterHeight;

Texture HeightMap;
sampler HeightMapSampler = sampler_state {
	texture = <HeightMap>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = Linear;
	AddressU = Clamp;
	AddressV = Clamp;
};

Texture Texture0;
sampler TextureSampler0 = sampler_state {
	texture = <Texture0>;
	Filter = ANISOTROPIC;
	MinFilter = Anisotropic;
	MagFilter = Anisotropic;
	MipFilter = Linear;
	MaxAnisotropy = 16;
	AddressU = wrap;
	AddressV = wrap;
};
Texture Texture1;
sampler TextureSampler1 = sampler_state {
	texture = <Texture1>;
	Filter = ANISOTROPIC;
	MinFilter = Anisotropic;
	MagFilter = Anisotropic;
	MipFilter = Linear;
	MaxAnisotropy = 16;
	AddressU = wrap;
	AddressV = wrap;
};
Texture Texture2;
sampler TextureSampler2 = sampler_state {
	texture = <Texture2>;
	Filter = ANISOTROPIC;
	MinFilter = Anisotropic;
	MagFilter = Anisotropic;
	MipFilter = Linear;
	MaxAnisotropy = 16;
	AddressU = wrap;
	AddressV = wrap;
};

struct VertexShaderInput
{
	float4 Position : POSITION0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoords : TEXCOORD0;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

	float4 outPosition = float4(
		input.Position.x * TileSize,
		input.Position.y * MaxHeight,
		input.Position.z * TileSize,
		input.Position.w);
	output.Position = mul(outPosition, WorldViewProjection);
	output.TexCoords = float2(input.Position.x, input.Position.z);

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
	float2 heightMapCoords = float2(input.TexCoords.x / MapWidth, input.TexCoords.y / MapHeight);
	float3 off = float3(1.0f / MapWidth, 1.0f / MapHeight, 0.0);
	float hL = tex2D(HeightMapSampler, heightMapCoords.xy - off.xz).r * MaxHeight;
	float hR = tex2D(HeightMapSampler, heightMapCoords.xy + off.xz).r * MaxHeight;
	float hD = tex2D(HeightMapSampler, heightMapCoords.xy - off.zy).r * MaxHeight;
	float hU = tex2D(HeightMapSampler, heightMapCoords.xy + off.zy).r * MaxHeight;

/*
	float2 size = float2(TileSize, 0);
	float3 va = normalize(float3(TileSize, hR - hL, 0));
	float3 vb = normalize(float3(0, hD - hU, -TileSize));
	float3 N = normalize(cross(va, vb));
*/

	// deduce terrain normal
	float3 N = normalize(float3(hL - hR, TileSize, hD - hU) * TileSize);

	float slope = 1.0f - N.y;

	if (slope < 0.2f) {
		slope = 0;
	} else {
		slope = saturate((0.8f - (1.0f - slope)) / 0.1f);
	}

	float sand = tex2D(HeightMapSampler, heightMapCoords.xy).r * MaxHeight;

	if (sand < WaterHeight + 4.0f) {
		if (sand < WaterHeight + 3.0f) {
			sand = 1;
		} else {
			sand = saturate((4.0f - (sand - WaterHeight)) / 1.0f);
		}
	} else {
		sand = 0;
	}

	float2 TexCoordsScale1 = mul(-input.TexCoords / 6, RotationMatrix(90));
	float2 TexCoordsScale2 = mul(-input.TexCoords / 10, RotationMatrix(90));

	float4 color0 = tex2D(TextureSampler0, input.TexCoords);
	color0 = lerp(color0, tex2D(TextureSampler0, TexCoordsScale2), 0.5);
	float4 color1 = tex2D(TextureSampler1, input.TexCoords);
	color1 = lerp(color1, tex2D(TextureSampler1, TexCoordsScale1), 0.5);
	float4 color2 = tex2D(TextureSampler2, input.TexCoords);
	color2 = lerp(color2, tex2D(TextureSampler2, TexCoordsScale2), 0.5);

	// Calculate the amount of light on this pixel.
	float lightIntensity = saturate(sqrt(dot(N, normalize(-LightDirection))));
	// Determine the final amount of diffuse color based on the diffuse color combined with the light intensity.
	float4 color = lerp(lerp(color0, color2, sand), color1, slope);
	color = saturate(color * max(lightIntensity, AmbientIntensity));
	color.a = 1;

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