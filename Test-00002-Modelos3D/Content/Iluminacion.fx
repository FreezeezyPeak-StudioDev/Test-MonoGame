// Iluminacion.fx - luz direccional cálida con relleno frío, oclusión
// ambiental por altura, especular de Blinn-Phong, niebla de distancia
// y piso de luz para que ningún fragmento quede negro.
float4x4 World;
float4x4 View;
float4x4 Projection;
float2 AoRango;
float3 LuzDir;
float3 CameraPos;
float3 FogColor;
float FogStart;
float FogEnd;
float Resalte;

texture DiffuseMap;
sampler2D DiffuseSampler = sampler_state
{
    Texture = <DiffuseMap>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

struct VSEntrada
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

struct VSSalida
{
    float4 Position : POSITION0;
    float3 WorldPos : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 TexCoord : TEXCOORD2;
    float LocalY : TEXCOORD3;
};

VSSalida VSPrincipal(VSEntrada input)
{
    VSSalida o;
    float4 wp = mul(input.Position, World);
    float4 vp = mul(wp, View);
    o.Position = mul(vp, Projection);
    o.WorldPos = wp.xyz;
    o.Normal = normalize(mul(input.Normal, (float3x3)World));
    o.TexCoord = input.TexCoord;
    o.LocalY = input.Position.y;
    return o;
}

float4 PSPrincipal(VSSalida input) : COLOR0
{
    float3 N = normalize(input.Normal);
    float3 L = normalize(LuzDir);
    float3 V = normalize(CameraPos - input.WorldPos);

    float3 albedo = tex2D(DiffuseSampler, input.TexCoord).rgb;

    // Oclusión ambiental suave: la base queda algo más oscura, nunca negra.
    float t = saturate((input.LocalY - AoRango.x) / max(AoRango.y - AoRango.x, 0.001));
    albedo *= lerp(0.62, 1.0, t);

    // Luz envolvente amplia: ni las caras traseras quedan negras.
    float dif = saturate((dot(N, L) + 0.65) / 1.65);
    float3 H = normalize(L + V);
    float spec = pow(saturate(dot(N, H)), 48.0) * 0.5;
    float3 amb = lerp(float3(0.36, 0.38, 0.44), float3(0.56, 0.58, 0.64), N.y * 0.5 + 0.5);

    float3 col = albedo * (amb + dif * float3(1.0, 0.96, 0.88))
               + spec * float3(1.0, 0.98, 0.94);

    float dist = length(input.WorldPos - CameraPos);
    col = lerp(col, FogColor, saturate((dist - FogStart) / (FogEnd - FogStart)));
    col += Resalte * float3(0.10, 0.10, 0.08);
    // Piso de luz: ningún fragmento puede quedar negro.
    col = max(col, albedo * 0.15);
    return float4(col, 1.0);
}

technique Principal
{
    pass P0
    {
        VertexShader = compile vs_3_0 VSPrincipal();
        PixelShader = compile ps_3_0 PSPrincipal();
    }
}
