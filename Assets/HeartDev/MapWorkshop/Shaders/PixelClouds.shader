// Copyright (c) 2026 HeartDev. See LICENSE.txt.
Shader "HeartDev/MapWorkshop/PixelClouds"
{
    Properties
    {
        _MainTex ("Cloud pattern", 2D) = "white" {}
        _CloudColor ("Cloud color", Color) = (.88,.97,.95,1)
        _Opacity ("Opacity", Range(0,1)) = .16
        _Coverage ("Coverage", Range(.01,1)) = .45
        _PatternPixels ("Pattern in map pixels", Float) = 96
        _Speed ("Pixels per second", Float) = .5
        _AlphaMask ("Use pattern alpha", Float) = 1
        _Invert ("Invert mask", Float) = 0
        [HideInInspector] _CloudLayout ("Map width, pixel scale, layer phase", Vector) = (4,32,0,0)
        [HideInInspector] _CloudHorizon ("Focus, horizon start and end", Vector) = (0,0,5,8)
        [HideInInspector] _WaterClock ("Preview clock", Float) = -1
    }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline"}
        Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            half4 _CloudColor;
            float4 _CloudLayout;
            float4 _CloudHorizon;
            float _Opacity,_Coverage,_PatternPixels,_Speed,_AlphaMask,_Invert,_WaterClock;
            struct Input{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Output{float4 position:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;float2 mapPosition:TEXCOORD2;};
            Output vert(Input v){Output o;VertexPositionInputs p=GetVertexPositionInputs(v.vertex.xyz);o.position=p.positionCS;o.uv=v.uv;o.mapPosition=v.vertex.xz;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
            half4 frag(Output i):SV_Target
            {
                // Irregular cloud banks belong to the map, rather than forming a screen vignette.
                float2 mapPixel=floor(i.mapPosition*max(1,_CloudLayout.y));
                float width=max(1,_CloudLayout.x*_CloudLayout.y);
                float phase=_CloudLayout.z+(mapPixel.x<0?2.7:0);
                float edge=width*(.32+.045*sin(mapPixel.y*.043+phase)+.025*sin(mapPixel.y*.097+phase*1.8));
                half sides=saturate((abs(mapPixel.x)-edge)/2);
                float horizonDistance=distance(i.mapPosition,_CloudHorizon.xy);
                float horizonEdge=_CloudHorizon.z+.15*sin(mapPixel.x*.071+_CloudLayout.z);
                half horizon=smoothstep(horizonEdge,max(horizonEdge+.01,_CloudHorizon.w),horizonDistance);
                half cloudArea=max(sides,horizon);
                clip(cloudArea-.001h);
                float time=_WaterClock>=0?_WaterClock:_Time.y;
                float2 uv=floor(i.uv+floor(time*_Speed)*float2(1,.35))/max(8,_PatternPixels);
                half4 source=SAMPLE_TEXTURE2D_LOD(_MainTex,sampler_PointRepeat,uv,0);
                half mask=lerp(1-dot(source.rgb,half3(.2126,.7152,.0722)),source.a,step(.5,_AlphaMask));mask=lerp(mask,1-mask,step(.5,_Invert));
                half alpha=saturate((mask-(1-_Coverage))/max(.01,_Coverage))*_Opacity*cloudArea;clip(alpha-.002h);
                return half4(MixFog(_CloudColor.rgb,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
