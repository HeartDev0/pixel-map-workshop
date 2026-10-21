// Copyright (c) 2026 HeartDev. See LICENSE.txt.
Shader "HeartDev/MapWorkshop/MarineSprite"
{
    Properties
    {
        _MainTex("Sprite",2D)="white"{} _SpriteRect("Sprite rectangle",Vector)=(0,0,1,1)
        _WaterClock("Preview clock",Float)=-1 _TailPixels("Tail movement",Float)=1 _TailFrequency("Tail frequency",Float)=5
        _PixelsPerUnit("Pixels per unit",Float)=32 _HeadRight("Source faces right",Float)=1 _MarineMode("Fish / jellyfish / silhouette",Float)=0 _SpriteWidth("Sprite world width",Float)=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);float4 _SpriteRect;float _WaterClock,_TailPixels,_TailFrequency,_PixelsPerUnit,_HeadRight,_MarineMode,_SpriteWidth;
            struct I {float4 positionOS:POSITION;float4 uv:TEXCOORD0;half4 color:COLOR;float4 motion:TEXCOORD1;};
            struct O {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half fog:TEXCOORD1;};
            O vert(I v)
            {
                float clock=_WaterClock>=0?_WaterClock:_Time.y;
                if(_MarineMode<.5||_MarineMode>1.5)
                {
                    float tail=1-saturate(v.uv.x),wave=sin(clock*_TailFrequency-tail*5.0+v.motion.z)*tail*tail*_TailPixels/max(1,_PixelsPerUnit);
                    v.positionOS.x-=v.motion.y*wave;v.positionOS.z+=v.motion.x*wave;
                    if(_HeadRight<.5)v.uv.x=1-v.uv.x;
                }
                else
                {
                    float phase=clock*_TailFrequency*.4+v.positionOS.x*.7+v.positionOS.z*.4;
                    float cap=smoothstep(.3,.8,v.uv.y),tentacle=1-smoothstep(.2,.7,v.uv.y);
                    v.positionOS.x+=(v.uv.x-.5)*_SpriteWidth*.16*sin(phase)*cap;
                    v.positionOS.x+=sin(phase*1.3+v.uv.y*5)*_TailPixels/max(1,_PixelsPerUnit)*tentacle;
                }
                O o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=_SpriteRect.xy+v.uv.xy*_SpriteRect.zw;o.color=v.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 frag(O i):SV_Target
            {
                half4 sampled=SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv);half4 c=sampled*i.color;
                if(_MarineMode>1.5)c.rgb=i.color.rgb;
                clip(c.a-.025);c.rgb=MixFog(c.rgb,i.fog);return c;
            }
            ENDHLSL
        }
    }
}
