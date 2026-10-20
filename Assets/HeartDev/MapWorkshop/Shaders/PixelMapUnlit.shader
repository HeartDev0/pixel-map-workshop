// Copyright (c) 2026 HeartDev. See LICENSE.txt.
Shader "HeartDev/MapWorkshop/PixelMapUnlit"
{
    Properties
    {
        _MainTex ("Pixel texture", 2D) = "white" {}
        _TerrainLighting ("Terrain light bands", Float) = 0
        _ReceiveShadows ("Receive shadows", Float) = 1
        [HideInInspector] _SeparateWater ("Separate water mesh", Float) = 0
        [HideInInspector] _WaterOnly ("Water only", Float) = 0
        _SurfaceMap ("Water and shoreline mask", 2D) = "black" {}
        [HideInInspector] _WaterZoneMap ("Water zones", 2D) = "black" {}
        [HideInInspector] _WaterZoneId ("Water zone id", Float) = 0
        _WaterColor ("Water", Color) = (0,.68,.86,1)
        _WaterDeep ("Deep water", Color) = (0,.59,.8,1)
        _WaterMiddle ("Middle water", Color) = (0,.635,.835,1)
        _WaterGradientRange ("Water gradient distance", Vector) = (2,14,0,0)
        _WaterFoam ("Foam", Color) = (.87,.99,.98,1)
        _WaterFPS ("Water frames per second", Float) = 6
        _WaterPatternSpeed ("Foam animation speed", Float) = .7
        [HideInInspector] _WaterPatternPeriod ("Legacy pattern period", Float) = 16
        _FoamStrength ("Shore foam", Range(0,1)) = .35
        [HideInInspector] _WaveStrength ("Legacy surface shimmer", Range(0,1)) = 0
        _GradientMap ("Three color gradients", 2D) = "white" {}
        [HideInInspector] _PropGradient ("Use per-prop gradient row", Float) = 0
        _DistanceGradient ("Use distance gradients", Float) = 0
        _GradientRange ("Near and far distance", Vector) = (2,14,0,0)
        _GrassSway ("Grass sway in pixels", Float) = 0
        _WindFPS ("Wind frames per second", Float) = 8
        _PixelsPerUnit ("Pixels per unit", Float) = 32
        _AlgaeSway ("Algae sway in pixels", Float) = 0
        _AlgaeSpeed ("Underwater current speed", Float) = .55
        _AlgaeWaveCycles ("Algae wave cycles", Float) = 1.5
        [HideInInspector] _WaterClock ("Editor preview clock", Float) = -1
        [HideInInspector] _BaseMap ("Shadow silhouette", 2D) = "white" {}
        [HideInInspector] _BaseColor ("Base color", Color) = (1,1,1,1)
        [HideInInspector] _Cutoff ("Alpha cutoff", Float) = .1
        [HideInInspector] _Cull ("Cull", Float) = 0
        [HideInInspector] _AlphaClip ("Alpha clip", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite On ZTest LEqual
        Pass
        {
            Name "PixelSurface"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_SurfaceMap);
            TEXTURE2D(_WaterZoneMap);float _WaterZoneId;
            TEXTURE2D(_GradientMap);
            float4 _GradientMap_TexelSize,_GradientRange;
            float4 _MainTex_TexelSize;
            float _TerrainLighting,_ReceiveShadows,_WaterFPS,_WaterClock,_GrassSway,_WindFPS,_PixelsPerUnit,_AlgaeSway,_AlgaeSpeed,_AlgaeWaveCycles,_SeparateWater,_WaterOnly;
            half4 _WaterColor,_WaterMiddle,_WaterDeep,_WaterFoam;
            float4 _WaterGradientRange;
            float _WaterPatternSpeed,_WaterPatternPeriod,_FoamStrength,_WaveStrength,_DistanceGradient,_PropGradient;
            struct Input { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float3 normalOS:NORMAL; float2 motion:TEXCOORD1; float2 gradient:TEXCOORD2; };
            struct Output { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; half fog:TEXCOORD2; half3 normalWS:TEXCOORD3; float gradient:TEXCOORD4; };
            Output vert(Input v)
            {
                float clock=_WaterClock>=0?_WaterClock:_Time.y;
                float time=floor(clock*max(1,_WindFPS))/max(1,_WindFPS);
                float sway=round(sin(time*1.7+v.motion.y)*_GrassSway);
                v.positionOS.x+=sway/max(1,_PixelsPerUnit)*v.motion.x*v.motion.x;
                float algaeTime=clock*max(.05,_AlgaeSpeed);
                float height=saturate(v.motion.x);
                // Resolve 1.25–2 traveling waves with ten height segments: alternating bends along the stalk.
                float wavePhase=v.motion.y-height*(clamp(_AlgaeWaveCycles,1.25,2.0)*6.2831853);
                float algaeWave=.9*sin(algaeTime+wavePhase)+.1*sin(algaeTime*.72+wavePhase+1.1);
                // Reach useful amplitude near the bottom; only the root is pinned, not the lower half.
                float algaeEnvelope=smoothstep(0.0,.2,height)*lerp(.65,1.0,height);
                float algaeBend=algaeWave*_AlgaeSway*algaeEnvelope;
                v.positionOS.x+=algaeBend/max(1,_PixelsPerUnit);
                Output o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.uv=v.uv;o.fog=ComputeFogFactor(p.positionCS.z);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.gradient=v.gradient.x;return o;
            }
            half4 frag(Output i):SV_Target
            {
                half4 mask=SAMPLE_TEXTURE2D(_SurfaceMap,sampler_PointClamp,i.uv);
                if(_SeparateWater>.5){if(_WaterOnly>.5)clip(mask.r-.5);else clip(.5-mask.r);}
                if(_WaterOnly>.5)clip(.25-abs(round(SAMPLE_TEXTURE2D(_WaterZoneMap,sampler_PointClamp,i.uv).r*255)-_WaterZoneId));
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv);
                if(_WaterOnly<.5)clip(c.a-.1);
                if(mask.r>.5h)
                {
                    float2 pixel=floor(i.uv*_MainTex_TexelSize.zw);
                    float clock=_WaterClock>=0?_WaterClock:_Time.y;
                    float frame=floor(clock*max(1,_WaterFPS));
                    // Calm, untextured water. Only the shoreline moves.
                    float distanceT=mask.a;
                    c.rgb=distanceT<.5?lerp(_WaterColor.rgb,_WaterMiddle.rgb,distanceT*2):lerp(_WaterMiddle.rgb,_WaterDeep.rgb,(distanceT-.5)*2);
                    float tick=frame/max(1,_WaterFPS)*max(0,_WaterPatternSpeed);
                    half width=.35h+.25h*sin(pixel.x*.31+pixel.y*.23+tick*3);
                    half foam=(1-smoothstep(width-.06h,width+.06h,1-mask.g))*step(.001h,mask.g)*_FoamStrength;
                    c.rgb=lerp(c.rgb,_WaterFoam.rgb,foam);
                }
                float gradientRow=_PropGradient>.5?i.gradient:floor(mask.b*255+.5);
                if(_DistanceGradient>.5&&gradientRow>.5&&mask.r<.5h)
                {
                    float row=(floor(gradientRow+.5)+.5)*_GradientMap_TexelSize.y;
                    half4 near=SAMPLE_TEXTURE2D(_GradientMap,sampler_PointClamp,float2(1./6.,row));
                    half4 middle=SAMPLE_TEXTURE2D(_GradientMap,sampler_PointClamp,float2(.5,row));
                    half3 far=SAMPLE_TEXTURE2D(_GradientMap,sampler_PointClamp,float2(5./6.,row)).rgb;
                    float t=saturate((distance(_WorldSpaceCameraPos,i.positionWS)-_GradientRange.x)/max(.01,_GradientRange.y-_GradientRange.x));
                    half3 tone=t<.5?lerp(near.rgb,middle.rgb,t*2):lerp(middle.rgb,far,(t-.5)*2);
                    half detail=clamp(dot(c.rgb,half3(.2126,.7152,.0722))/max(.02,dot(near.rgb,half3(.2126,.7152,.0722))),.25h,2.h);
                    tone*=lerp(1.h,detail,middle.a);c.rgb=lerp(c.rgb,tone,near.a);
                }
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                c.rgb*=lerp(1.h,lerp(.55h,1.h,sun.shadowAttenuation),saturate(_ReceiveShadows));
                half bands=floor((.65h+.35h*saturate(dot(normalize(i.normalWS),sun.direction)))*4.h+.5h)/4.h;
                c.rgb*=lerp(1.h,bands,saturate(_TerrainLighting)*(1-step(.5h,mask.r)));
                c.rgb=MixFog(c.rgb,i.fog);c.a=1;return c;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
