// Copyright (c) 2026 HeartDev. See LICENSE.txt.
Shader "HeartDev/MapWorkshop/Waterfall"
{
    Properties {
        _WaterColor("Water",Color)=(0,.64,.84,1)
        _FoamColor("Foam",Color)=(.87,.99,.98,1)
        _Speed("Speed",Float)=.7
        _PixelSize("Pixel dimensions",Vector)=(8,16,0,0)
        [HideInInspector] _WaterClock("Editor clock",Float)=-1
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
        Cull Off ZWrite On
        Pass {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _WaterColor,_FoamColor;float _Speed,_WaterClock;float4 _PixelSize;
            struct I {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct O {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;};
            O vert(I v){O o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(O i):SV_Target {
                float clock=_WaterClock>=0?_WaterClock:_Time.y;
                float2 pixel=floor(i.uv*max(1,_PixelSize.xy));
                float frame=floor(clock*6*max(.05,_Speed));
                half band=step(3,fmod(pixel.y-frame+pixel.x*2+4096,9));
                half edge=step(pixel.x,0)+step(_PixelSize.x-2,pixel.x);
                half foam=max(saturate(edge)*.5,(1-step(2,fmod(pixel.y-frame+4096,11)))*.8);
                half3 color=lerp(_WaterColor.rgb*.85,_WaterColor.rgb,band);
                color=lerp(color,_FoamColor.rgb,foam);return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
