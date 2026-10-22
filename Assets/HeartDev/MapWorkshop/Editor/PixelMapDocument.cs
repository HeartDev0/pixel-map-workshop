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
        public const int CurrentFormatVersion=1;
        // Missing field in existing assets must mean legacy, not already migrated.
        [SerializeField,HideInInspector] private int formatVersion;
        public int FormatVersion=>formatVersion;
        public bool IsSupportedFormat=>formatVersion>=0&&formatVersion<=CurrentFormatVersion;
        internal void SetFormatVersion(int version)=>formatVersion=version;
        public enum MaterialAppearance { RecordedRecipe, Material, Raster }
        [Serializable] public sealed class VariationStyle
        {
            public bool enabled;
            public Color color=new Color32(82,151,61,255);
            public int scale=3,seedOffset;
            [Range(0,1)] public float density=.15f;
        }
        [Serializable] public sealed class Decoration
        {
            public Sprite sprite;
            public Vector2Int pixel;
            public bool grass;
            public bool algae;
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
            public bool materialStyleOverride;
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
            public string id,name;
            public MaterialAppearance appearance;
            public PatternStyle pattern=new PatternStyle();
            public DistanceGradient gradient=new DistanceGradient();
            public VariationStyle variation=new VariationStyle();
            public bool gradientIncludesWaterZones;
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
        public bool cloudWrapAround=true;
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
        [Tooltip("Excava el camino por debajo del terreno con valores negativos, o crea un resalte con valores positivos. El borde vuelve al nivel original según el suavizado.")]
        public int pathHeightOffsetPixels;
        [Tooltip("Ancho, en píxeles, de la pendiente que conecta el fondo excavado con el terreno original. Cero conserva un corte duro.")]
        public int pathHeightSmoothPixels;
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
        [Min(0)] public float algaeSwayPixels=4,algaeSwaySpeed=.55f,algaeWaveCycles=1.5f;
        public List<Decoration> decorations=new List<Decoration>();
        [Range(0,90)] public float flowerTilt=55;
        public List<Sprite> flowerSprites=new List<Sprite>();
        public int seed=12345;
        public void Initialize(int w,int h,int ppu)
        {
            formatVersion=CurrentFormatVersion;
            width=Mathf.Clamp(w,16,1024);height=Mathf.Clamp(h,16,2048);pixelsPerUnit=Mathf.Clamp(ppu,8,128);
            heightStep=1f/pixelsPerUnit;
            pixels=new Color32[width*height];elevations=new int[pixels.Length];surfaces=new byte[pixels.Length];decorations.Clear();tileProps.Clear();layers.Clear();paintMaterials.Clear();originalInLayers=false;
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(104,185,71,255);
        }
        public bool IsValid=>width>0&&height>0&&pixels!=null&&pixels.Length==width*height&&elevations!=null&&elevations.Length==pixels.Length;
        public int Index(int x,int y)=>Mathf.Clamp(y,0,height-1)*width+Mathf.Clamp(x,0,width-1);
        public byte Surface(int index)
        {
            return PixelMapSurfaceResolver.Surface(this,index,PixelMapSurfaceResolver.VisibleLayer(this,index));
        }
        public Color32 PixelColor(int index)
        {
            return PixelMapSurfaceResolver.Resolve(this,index).Color;
        }
        public PaintLayer TopLayer(int index)
        {
            return PixelMapSurfaceResolver.VisibleLayer(this,index);
        }
        public bool DecorationVisible(Decoration prop)=>string.IsNullOrEmpty(prop.layerId)||layers.Exists(l=>l.id==prop.layerId&&l.visible);
        public bool IsAlgae(Decoration prop)=>prop!=null&&(prop.algae||prop.sprite!=null&&prop.sprite.name.StartsWith("Alga",StringComparison.OrdinalIgnoreCase));
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
            if(Surface(Index(Mathf.FloorToInt(x),Mathf.FloorToInt(y)))==2)return WaterHeight(x,y);
            return RawSurfaceHeight(x,y)+PathHeightOffsetAt(x,y);
        }
        // A path is a terrain cut/fill, like a carved river bed. Its painted mask
        // defines the floor; smoothing blends surrounding ground back to its
        // original height rather than moving the path horizontally.
        public float PathHeightOffsetAt(float x,float y)
        {
            if(pathHeightOffsetPixels==0||width<=0||height<=0)return 0;
            int smooth=Mathf.Clamp(pathHeightSmoothPixels,0,32);
            if(smooth==0)return Surface(Index(Mathf.FloorToInt(x),Mathf.FloorToInt(y)))==1?pathHeightOffsetPixels/(float)pixelsPerUnit:0;

            int cx=Mathf.FloorToInt(x),cy=Mathf.FloorToInt(y),reach=smooth+1;
            float nearest=float.PositiveInfinity;
            for(int py=Mathf.Max(0,cy-reach);py<=Mathf.Min(height-1,cy+reach);py++)
            for(int px=Mathf.Max(0,cx-reach);px<=Mathf.Min(width-1,cx+reach);px++)
            {
                if(Surface(Index(px,py))!=1)continue;
                float dx=Mathf.Max(0,Mathf.Abs(x-(px+.5f))-.5f),dy=Mathf.Max(0,Mathf.Abs(y-(py+.5f))-.5f);
                nearest=Mathf.Min(nearest,Mathf.Sqrt(dx*dx+dy*dy));
                if(nearest<=0)break;
            }
            if(float.IsPositiveInfinity(nearest)||nearest>=smooth)return 0;
            float t=1-nearest/smooth;t=t*t*(3-2*t);
            return pathHeightOffsetPixels/(float)pixelsPerUnit*t;
        }
        public float[] BuildPathHeightWeights()
        {
            if(pathHeightOffsetPixels==0||pixels==null||pixels.Length!=width*height)return null;
            var weights=new float[pixels.Length];
            int smooth=Mathf.Clamp(pathHeightSmoothPixels,0,32);
            if(smooth==0)
            {
                for(int i=0;i<weights.Length;i++)if(Surface(i)==1)weights[i]=1;
                return weights;
            }

            const float diagonal=1.41421356f;
            float[] distances=new float[pixels.Length];
            for(int i=0;i<distances.Length;i++)distances[i]=Surface(i)==1?0:float.PositiveInfinity;
            // Two-pass chamfer distance transform; it stays linear in map size and
            // avoids a per-vertex neighborhood scan while baking the ground mesh.
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int i=y*width+x;float d=distances[i];
                if(x>0)d=Mathf.Min(d,distances[i-1]+1);
                if(y>0)d=Mathf.Min(d,distances[i-width]+1);
                if(x>0&&y>0)d=Mathf.Min(d,distances[i-width-1]+diagonal);
                if(x+1<width&&y>0)d=Mathf.Min(d,distances[i-width+1]+diagonal);
                distances[i]=d;
            }
            for(int y=height-1;y>=0;y--)for(int x=width-1;x>=0;x--)
            {
                int i=y*width+x;float d=distances[i];
                if(x+1<width)d=Mathf.Min(d,distances[i+1]+1);
                if(y+1<height)d=Mathf.Min(d,distances[i+width]+1);
                if(x+1<width&&y+1<height)d=Mathf.Min(d,distances[i+width+1]+diagonal);
                if(x>0&&y+1<height)d=Mathf.Min(d,distances[i+width-1]+diagonal);
                distances[i]=d;
            }
            for(int i=0;i<weights.Length;i++)
            {
                float d=distances[i];if(d<=0){weights[i]=1;continue;}
                // Treat the outside half of each pixel as the transition distance,
                // so even a one-pixel blend visibly softens the cut back to grade.
                d=Mathf.Max(0,d-.5f);if(d>=smooth)continue;
                float t=1-d/smooth;weights[i]=t*t*(3-2*t);
            }
            return weights;
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
            root.Add(new UnityEngine.UIElements.Label("Formato "+map.FormatVersion+" · "+map.paintMaterials.Count+" materiales/recetas"));
            if(!map.IsSupportedFormat){root.Add(new UnityEngine.UIElements.HelpBox("Este documento usa un formato más reciente. Actualiza Map Workshop antes de editarlo.",UnityEngine.UIElements.HelpBoxMessageType.Warning));return root;}
            if(map.FormatVersion<PixelMapDocument.CurrentFormatVersion)
            {
                root.Add(new UnityEngine.UIElements.HelpBox("Mapa anterior al sistema de materiales independientes. Puedes preparar una copia conservando el original.",UnityEngine.UIElements.HelpBoxMessageType.Info));
                root.Add(new UnityEngine.UIElements.Button(()=>PixelMapMaterialMigration.SaveUpgradedCopy(map)){text="Crear copia con materiales actualizados…"});
            }
            root.Add(new UnityEngine.UIElements.HelpBox("Documento del taller. Exporta un prefab para usar el mapa en un juego.",UnityEngine.UIElements.HelpBoxMessageType.Info));
            root.Add(new UnityEngine.UIElements.Button(()=>PixelMapWorkshopWindow.OpenDocument(map)){text="Abrir Map Workshop"});return root;
        }
    }
}
