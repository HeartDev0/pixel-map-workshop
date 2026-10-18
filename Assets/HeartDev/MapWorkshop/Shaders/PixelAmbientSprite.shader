// Copyright (c) 2026 HeartDev. See LICENSE.txt.
Shader "HeartDev/MapWorkshop/AmbientSprite"
{
    Properties {_MainTex("Sprite atlas",2D)="white"{} _SpriteRect("Sprite rect",Vector)=(0,0,1,1)}
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest LEqual
        Pass {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);float4 _SpriteRect;
            struct I {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct O {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half fog:TEXCOORD1;};
            O vert(I v){O o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=_SpriteRect.xy+v.uv*_SpriteRect.zw;o.color=v.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(O i):SV_Target {half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv)*i.color;clip(c.a-.03);c.rgb=MixFog(c.rgb,i.fog);return c;}
            ENDHLSL
        }
    }
}
