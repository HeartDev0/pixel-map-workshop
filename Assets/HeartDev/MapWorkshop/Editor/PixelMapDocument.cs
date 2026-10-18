// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    // Authoring data only. Baked maps contain no dependency on this Editor assembly.
    public sealed partial class PixelMapDocument : ScriptableObject
    {
        [Serializable] public sealed class Decoration
        {
            public Sprite sprite;
            public Vector2Int pixel;
            public bool grass;
            public float yaw;
            public float pitch;
            public bool customPitch;
            public int heightOffsetPixels;
            public string layerId;
            public string groupId;
            public PropShape shape;
            public int depthPixels=8;
            public Sprite sideSprite,topSprite;
        }
        [Serializable] public sealed class PaintLayer
        {
            public string id=Guid.NewGuid().ToString("N"),name="Nueva capa";
            public bool visible=true,locked;
            public Color32[] pixels=Array.Empty<Color32>();
            public byte[] surfaces=Array.Empty<byte>();
            public PatternStyle pattern=new PatternStyle();
            public DistanceGradient gradient=new DistanceGradient();
            public int[] materialIds=Array.Empty<int>();
        }
        [Serializable] public sealed class DistanceGradient
        {
            public bool enabled;
            public Color near=new Color32(104,185,71,255),middle=new Color32(134,203,106,255),far=new Color32(177,221,173,255);
            [Range(0,1)] public float strength=1;
            public bool keepPattern=true;
        }
        [Serializable] public sealed class PaintMaterial
        {
            public PatternStyle pattern=new PatternStyle();
            public DistanceGradient gradient=new DistanceGradient();
            public byte surface;
            public bool globalWater;
        }
        public List<PaintMaterial> paintMaterials=new List<PaintMaterial>();
        public PatternStyle waterPattern=new PatternStyle{background=new Color32(0,174,220,255),ink=new Color32(78,202,224,255)};
        public float waterPatternSpeed=.7f,waterFoamStrength=.35f,waterWaveStrength=.08f;
        public DistanceGradient groundGradient=new DistanceGradient();
        public DistanceGradient pathGradient=new DistanceGradient{near=new Color32(169,110,72,255),middle=new Color32(191,145,101,255),far=new Color32(215,187,147,255)};
        public float gradientNear=2,gradientFar=14;
        public bool cloudsEnabled;
        public Texture2D cloudPattern;
        public Color cloudColor=new Color32(224,246,242,255);
        public float cloudOpacity=.16f,cloudCoverage=.45f,cloudSpeed=.5f;
        public int cloudSizePixels=96,cloudHeightPixels=96;
        public bool cloudInvert;
        public bool cloudAlphaMask=true;
        public bool farCloudsEnabled=true;
        public Color farCloudColor=new Color32(146,155,169,255);
        public float farCloudOpacity=.18f,farCloudSpeed=.2f;
        public int farCloudSizePixels=144,farCloudHeightOffsetPixels=64;
        [Serializable] public sealed class PatternStyle
        {
            public bool enabled,twoColors=true,blend;
            public Texture2D texture;
            public int size=16;
            public int edgePixels;
            public Vector2Int offset;
            public Color background=new Color32(169,110,72,255),ink=new Color32(116,82,53,255);
            [Range(0,1)] public float blendAmount=.35f;
        }
        public List<PaintLayer> layers=new List<PaintLayer>();
        public bool originalInLayers;
        public Color32 backgroundColor=new Color32(104,185,71,255);
        public int width=128,height=512,pixelsPerUnit=32,meshStep=4;
        public float heightStep=.0625f;
        public Color32[] pixels=Array.Empty<Color32>();
        public int[] elevations=Array.Empty<int>();
        // 0 = ordinary paint, 1 = path, 2 = animated water. Old maps remain valid.
        public byte[] surfaces=Array.Empty<byte>();
        public bool pathOutline=true;
        public int outlinePixels=2;
        public Color outlineColor=new Color32(219,162,106,255);
        public int outlineStyle;
        public Color outlineOuterColor=new Color32(115,79,47,255);
        public bool terrainVariation;
        public Color variationColor=new Color32(82,151,61,255);
        public int variationScale=3;
        public float variationDensity=.15f;
        public Color waterColor=new Color32(0,174,220,255),waterDeep=new Color32(0,151,205,255),waterFoam=new Color32(222,252,249,255);
        public Color waterMiddle=new Color32(0,162,213,255);
        public float waterGradientNear=2,waterGradientFar=14;
        [Range(1,64)] public int waterGradientPixels=8;
        public float waterFPS=6;
        public bool carveRivers=true;
        public int riverDepthPixels=3,riverBankPixels=3;
        public Color riverBankColor=new Color32(130,79,54,255);
        public Color riverBankMiddle=new Color32(164,105,66,255),riverBankBottom=new Color32(93,57,42,255);
        public int riverBankBands=1;
        public bool riverBankEqualBands=true;
        public int riverTopBandPixels=1,riverMiddleBandPixels=1;
        public float grassSwayPixels=1,grassWindFPS=8;
        public List<Decoration> decorations=new List<Decoration>();
        [Range(0,90)] public float flowerTilt=55;
        public List<Sprite> flowerSprites=new List<Sprite>();
        public int seed=12345;
        public void Initialize(int w,int h,int ppu)
        {
            width=Mathf.Clamp(w,16,1024);height=Mathf.Clamp(h,16,2048);pixelsPerUnit=Mathf.Clamp(ppu,8,128);
            heightStep=1f/pixelsPerUnit;
            pixels=new Color32[width*height];elevations=new int[pixels.Length];surfaces=new byte[pixels.Length];decorations.Clear();layers.Clear();paintMaterials.Clear();originalInLayers=false;
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(104,185,71,255);
        }
        public bool IsValid=>width>0&&height>0&&pixels!=null&&pixels.Length==width*height&&elevations!=null&&elevations.Length==pixels.Length;
        public int Index(int x,int y)=>Mathf.Clamp(y,0,height-1)*width+Mathf.Clamp(x,0,width-1);
        public byte Surface(int index)
        {
            if(WaterZoneAt(index%width,index/width)!=null)return 2;
            for(int i=layers.Count-1;i>=0;i--){var l=layers[i];if(l.visible&&l.pixels.Length==pixels.Length&&l.pixels[index].a>0&&PixelMapPatterns.Covers(l,index))return l.surfaces.Length==pixels.Length?l.surfaces[index]:(byte)0;}
            return !originalInLayers&&surfaces!=null&&surfaces.Length==pixels.Length?surfaces[index]:(byte)0;
        }
        public Color32 PixelColor(int index)
        {
            for(int i=layers.Count-1;i>=0;i--){var l=layers[i];if(l.visible&&l.pixels.Length==pixels.Length&&l.pixels[index].a>0&&PixelMapPatterns.Covers(l,index))return PixelMapPatterns.ColorAt(l,index,width);}return originalInLayers?backgroundColor:pixels[index];
        }
        public PaintLayer TopLayer(int index)
        {
            for(int i=layers.Count-1;i>=0;i--){var layer=layers[i];if(layer.visible&&layer.pixels.Length==pixels.Length&&layer.pixels[index].a>0&&PixelMapPatterns.Covers(layer,index))return layer;}return null;
        }
        public bool DecorationVisible(Decoration prop)=>string.IsNullOrEmpty(prop.layerId)||layers.Exists(l=>l.id==prop.layerId&&l.visible);
        public PaintLayer AddLayer(string title)
        {
            var layer=new PaintLayer{name=title,pixels=new Color32[pixels.Length],surfaces=new byte[pixels.Length]};layers.Add(layer);return layer;
        }
        public void EnsureSurfaces(){if(surfaces==null||surfaces.Length!=pixels.Length)surfaces=new byte[pixels.Length];}
        public float VertexHeight(int x,int y)
        {
            return RawVertexHeight(x,y)-(carveRivers&&Surface(Index(x,y))==2?RiverDepth:0);
        }
        public float RiverDepth=>carveRivers?Mathf.Max(0,riverDepthPixels)/(float)pixelsPerUnit:0;
        public float RawVertexHeight(int x,int y)=>elevations[Index(x,y)]*heightStep;
        // Same triangle interpolation as the exported mesh, so props sit on the surface.
        public float SurfaceHeight(float x,float y)
        {
            return Surface(Index(Mathf.FloorToInt(x),Mathf.FloorToInt(y)))==2?WaterHeight(x,y):RawSurfaceHeight(x,y);
        }
        public float RawSurfaceHeight(float x,float y)
        {
            x=Mathf.Clamp(x,0,width);y=Mathf.Clamp(y,0,height);
            int step=Mathf.Clamp(meshStep,1,16),x0=Mathf.Min((int)x/step*step,((width-1)/step)*step),y0=Mathf.Min((int)y/step*step,((height-1)/step)*step);
            int x1=Mathf.Min(x0+step,width),y1=Mathf.Min(y0+step,height);
            float u=(x-x0)/(x1-x0),v=(y-y0)/(y1-y0);
            float a=RawVertexHeight(x0,y0),b=RawVertexHeight(x1,y0),c=RawVertexHeight(x0,y1),d=RawVertexHeight(x1,y1);
            return u+v<=1?a+(b-a)*u+(c-a)*v:d+(c-d)*(1-u)+(b-d)*(1-v);
        }
    }

    [CustomEditor(typeof(PixelMapDocument))]
    public sealed class PixelMapDocumentInspector : Editor
    {
        public override UnityEngine.UIElements.VisualElement CreateInspectorGUI()
        {
            var map=(PixelMapDocument)target;
            var root=new UnityEngine.UIElements.VisualElement();
            root.Add(new UnityEngine.UIElements.Label($"{map.width} × {map.height} píxeles · {map.decorations.Count} figuras"));
            root.Add(new UnityEngine.UIElements.HelpBox("Documento del taller. Exporta un prefab para usar el mapa en un juego.",UnityEngine.UIElements.HelpBoxMessageType.Info));
            root.Add(new UnityEngine.UIElements.Button(()=>PixelMapWorkshopWindow.OpenDocument(map)){text="Abrir Map Workshop"});return root;
        }
    }
}
