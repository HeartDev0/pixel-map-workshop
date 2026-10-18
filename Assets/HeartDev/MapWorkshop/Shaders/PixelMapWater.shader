// Copyright (c) 2026 HeartDev. See LICENSE.txt.
Shader "HeartDev/MapWorkshop/PixelMapWater"
{
    Properties
    {
        _MainTex ("Pixel terrain", 2D) = "white" {}
        _SurfaceMap ("Water and shoreline mask", 2D) = "black" {}
        _TerrainLighting ("Terrain light bands", Float) = 1
        _ReceiveShadows ("Receive shadows", Float) = 1
        [HideInInspector] _SeparateWater ("Separate water mesh", Float) = 1
        [HideInInspector] _WaterOnly ("Water only", Float) = 1
        [HideInInspector] _WaterZoneMap ("Water zones", 2D) = "black" {}
        [HideInInspector] _WaterZoneId ("Water zone id", Float) = 0
        _WaterColor ("Water", Color) = (0,.68,.86,1)
        _WaterDeep ("Deep water", Color) = (0,.59,.8,1)
        _WaterMiddle ("Middle water", Color) = (0,.635,.835,1)
        _WaterGradientRange ("Water gradient distance", Vector) = (2,14,0,0)
        _WaterFoam ("Foam", Color) = (.87,.99,.98,1)
        _WaterFPS ("Frames per second", Range(1,12)) = 6
        _WaterPatternSpeed ("Foam animation speed", Float) = .7
        [HideInInspector] _WaterPatternPeriod ("Legacy pattern period", Float) = 16
        _FoamStrength ("Shore foam", Range(0,1)) = .35
        [HideInInspector] _WaveStrength ("Legacy surface shimmer", Range(0,1)) = 0
        _GradientMap ("Three color gradients", 2D) = "white" {}
        _DistanceGradient ("Use distance gradients", Float) = 0
        _GradientRange ("Near and far distance", Vector) = (2,14,0,0)
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
        // A lower opaque surface, clipped to the river mask.
        UsePass "HeartDev/MapWorkshop/PixelMapUnlit/PixelSurface"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
