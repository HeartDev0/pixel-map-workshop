#ifndef HEARTDEV_PIXEL_MAP_SHADOW_CASTER_INCLUDED
#define HEARTDEV_PIXEL_MAP_SHADOW_CASTER_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
TEXTURE2D(_BaseMap);
float _Cutoff, _GrassSway, _WindFPS, _PixelsPerUnit, _WaterClock;
float3 _LightDirection, _LightPosition;
struct PixelMapShadowInput
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float2 motion : TEXCOORD1;
};
struct PixelMapShadowOutput
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};
PixelMapShadowOutput PixelMapShadowVertex(PixelMapShadowInput v)
{
    float clock = _WaterClock >= 0 ? _WaterClock : _Time.y;
    float time = floor(clock * max(1, _WindFPS)) / max(1, _WindFPS);
    v.positionOS.x += round(sin(time * 1.7 + v.motion.y) * _GrassSway)
        / max(1, _PixelsPerUnit) * v.motion.x * v.motion.x;
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    PixelMapShadowOutput o;
    o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    o.positionCS = ApplyShadowClamping(o.positionCS);
    o.uv = v.uv;
    return o;
}
half4 PixelMapShadowFragment(PixelMapShadowOutput i) : SV_Target
{
    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_PointClamp, i.uv).a - _Cutoff);
    return 0;
}
#endif
