// Copyright (c) 2026 HeartDev. See LICENSE.txt.
Shader "HeartDev/MapWorkshop/MarineLight"
{
    Properties
    {
        _MainTex("Sprite mask",2D)="white"{} _SurfaceMap("Terrain mask",2D)="black"{} _SeparateWater("Respect terrain water holes",Float)=0 _UseSpriteMask("Use sprite mask",Float)=0
        _LightColor("Light color",Color)=(.4,.85,1,1) _Strength("Strength",Float)=.25 _PatternPixels("Pattern pixels",Float)=16
        _PixelsPerUnit("Pixels per unit",Float)=32 _WaterClock("Preview clock",Float)=-1 _Speed("Speed",Float)=1
        _MarineBounds("Zone X/Z bounds",Vector)=(0,0,1,1) _RayMode("Ray mode",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent"}
        Blend SrcAlpha One Cull Off ZWrite Off ZTest LEqual Offset -1,-1
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);TEXTURE2D(_SurfaceMap);float _SeparateWater;float4 _LightColor,_MarineBounds;float _UseSpriteMask,_Strength,_PatternPixels,_PixelsPerUnit,_WaterClock,_Speed,_RayMode;
            struct I {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct O {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;half fog:TEXCOORD2;half up:TEXCOORD3;float2 mapXZ:TEXCOORD4;};
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            O vert(I v)
            {
                float clock=_WaterClock>=0?_WaterClock:_Time.y;
                if(_RayMode>.5)v.positionOS.x+=sin(clock*.25*_Speed+v.positionOS.z)*.04*v.uv.y;
                O o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.mapXZ=v.positionOS.xz;o.uv=v.uv;o.fog=ComputeFogFactor(p.positionCS.z);o.up=dot(v.normalOS,v.normalOS)<.01?1:max(.16,abs(TransformObjectToWorldNormal(v.normalOS).y));return o;
            }
            half4 frag(O i):SV_Target
            {
                if(_SeparateWater>.5)clip(.5-SAMPLE_TEXTURE2D(_SurfaceMap,sampler_PointClamp,i.uv).r);
                float2 p=i.mapXZ;float edge=min(min(p.x-_MarineBounds.x,_MarineBounds.z-p.x),min(p.y-_MarineBounds.y,_MarineBounds.w-p.y));clip(edge);
                float clock=floor((_WaterClock>=0?_WaterClock:_Time.y)*6)/6*_Speed;
                float pattern;
                if(_RayMode>.5)
                {
                    float u=floor(i.uv.x*24)/24;pattern=pow(saturate(1-abs(u-.5)*2),2)*smoothstep(0,.18,i.uv.y)*(1-i.uv.y*.6);
                    pattern*=.75+.25*sin(clock*.4+i.positionWS.z*2);
                }
                else
                {
                    float2 pixel=floor(p*_PixelsPerUnit)/max(1,_PatternPixels);float2 cell=floor(pixel);float2 f=frac(pixel);float nearest=10,second=10;
                    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                    {
                        float2 offset=float2(x,y);float n=hash(cell+offset);float2 samplePoint=offset+.5+.26*sin(clock*.7+n*6.283185+float2(0,1.7));float d=length(samplePoint-f);
                        if(d<nearest){second=nearest;nearest=d;}else second=min(second,d);
                    }
                    pattern=(1-smoothstep(.025,.11,second-nearest))*i.up;
                    if(_UseSpriteMask>.5)clip(SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv).a-.1);
                }
                half3 color=MixFogColor(_LightColor.rgb,half3(0,0,0),i.fog);
                return half4(color,saturate(pattern*_Strength*_LightColor.a*smoothstep(0,.15,edge)));
            }
            ENDHLSL
        }
    }
}
