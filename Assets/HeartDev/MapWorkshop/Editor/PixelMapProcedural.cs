// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    internal static class PixelMapProcedural
    {
        internal enum Biome { Bosque, Montañas, Archipiélago, Cripta }
        internal static int Seed(string text){unchecked{uint hash=2166136261;foreach(char c in text??"")hash=(hash^c)*16777619;return (int)(hash&0x7fffffff);}}
        internal static PixelMapDocument Generate(Biome biome,string seed,int width,int height,int ppu,int relief,int scale,float coverage)
        {
            var map=ScriptableObject.CreateInstance<PixelMapDocument>();map.name="Base_"+biome;map.Initialize(width,height,ppu);map.seed=Seed(seed);
            map.generationPreset=biome.ToString();map.generationSeed=seed;map.meshStep=biome==Biome.Cripta?1:2;map.pathOutline=biome!=Biome.Cripta;
            map.waterFoamStrength=.8f;map.heightStep=1f/ppu;
            var random=new System.Random(map.seed);float ox=random.Next(100,10000),oy=random.Next(100,10000);
            float Noise(int x,int y){float f=Mathf.Max(8,scale);return Mathf.PerlinNoise(ox+x/f,oy+y/f)*.7f+Mathf.PerlinNoise(ox+x/f*2.1f,oy+y/f*2.1f)*.3f;}
            var ground=map.AddLayer(biome==Biome.Cripta?"Muros de cripta":"Suelo generado");
            var road=map.AddLayer(biome==Biome.Cripta?"Salas y corredores":"Camino generado");var water=map.AddLayer("Agua generada");map.originalInLayers=true;
            Color32 green=new Color32(104,185,71,255),rock=new Color32(103,111,121,255),earth=new Color32(169,110,72,255);
            for(int y=0;y<map.height;y++)for(int x=0;x<map.width;x++)
            {
                int i=map.Index(x,y);float noise=Noise(x,y);ground.pixels[i]=biome==Biome.Cripta?new Color32(65,62,82,255):biome==Biome.Montañas&&noise>.59f?rock:green;
                map.elevations[i]=Mathf.RoundToInt((biome==Biome.Montañas?Mathf.Pow(noise,2)*relief:biome==Biome.Bosque?noise*relief*.25f:0));
                if(biome==Biome.Archipiélago)
                {
                    float nx=(x+.5f)/map.width*2-1,ny=(y+.5f)/map.height*2-1;
                    float island=noise-(Mathf.Pow(Mathf.Abs(nx),4)+Mathf.Pow(Mathf.Abs(ny),4))*.25f;
                    bool sea=island<Mathf.Lerp(.7f,.25f,coverage);
                    if(sea){water.pixels[i]=map.waterColor;water.surfaces[i]=2;map.elevations[i]=0;}
                    else{map.elevations[i]=Mathf.RoundToInt(Mathf.Clamp01((island-.35f)*3)*relief*.5f)+2;if(island<.48f)ground.pixels[i]=new Color32(224,200,131,255);}
                }
                else if(biome!=Biome.Cripta)
                {
                    float path=map.width*.5f+Mathf.Sin(y/Mathf.Max(20f,scale*2f)+ox)*map.width*.16f;
                    if(Mathf.Abs(x-path)<Mathf.Max(3,map.width*.045f)){road.pixels[i]=earth;road.surfaces[i]=1;}
                }
            }
            if(biome==Biome.Bosque)
            {
                int riverY=map.height/2;for(int y=0;y<map.height;y++)for(int x=0;x<map.width;x++)if(Mathf.Abs(y-riverY-Mathf.Sin(x/19f+oy)*6)<Mathf.Max(3,map.width*.035f)){int i=map.Index(x,y);water.pixels[i]=map.waterColor;water.surfaces[i]=2;}
            }
            if(biome==Biome.Cripta)
            {
                int wall=Mathf.Max(4,relief);for(int i=0;i<map.elevations.Length;i++)map.elevations[i]=wall;
                var centers=new List<Vector2Int>();int count=Mathf.Clamp(map.height/48,3,24);
                void Floor(int x,int y){int i=map.Index(x,y);map.elevations[i]=0;road.pixels[i]=new Color32(122,115,135,255);road.surfaces[i]=1;}
                for(int r=0;r<count;r++)
                {
                    int rw=random.Next(10,Mathf.Max(11,map.width/2)),rh=random.Next(10,Mathf.Max(11,Mathf.Min(36,map.height/4)));
                    int cx=random.Next(2,Mathf.Max(3,map.width-rw-2)),cy=random.Next(2,Mathf.Max(3,map.height-rh-2));
                    for(int y=cy;y<Mathf.Min(map.height-2,cy+rh);y++)for(int x=cx;x<Mathf.Min(map.width-2,cx+rw);x++)Floor(x,y);
                    centers.Add(new Vector2Int(cx+rw/2,cy+rh/2));
                }
                for(int r=1;r<centers.Count;r++)
                {
                    var a=centers[r-1];var b=centers[r];
                    for(int x=Mathf.Min(a.x,b.x);x<=Mathf.Max(a.x,b.x);x++)for(int k=-2;k<=2;k++)Floor(x,a.y+k);
                    for(int y=Mathf.Min(a.y,b.y);y<=Mathf.Max(a.y,b.y);y++)for(int k=-2;k<=2;k++)Floor(b.x+k,y);
                }
            }
            return map;
        }
    }
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private PixelMapProcedural.Biome baseBiome;
        [SerializeField] private string baseSeed="Mi mundo";
        [SerializeField] private int baseRelief=24,baseScale=48;
        [SerializeField] private float baseCoverage=.55f;
        private PixelMapDocument generatedBase;
        private Texture2D generatedThumbnail;
        private UnityEngine.UIElements.Image basePreview;
        private Label generationInfo;
        private void BuildGeneratorControls(VisualElement parent)
        {
            var fold=new Foldout{text="Bases por semilla",value=false};parent.Add(fold);
            var preset=new EnumField("Bioma",baseBiome);preset.RegisterValueChangedCallback(e=>baseBiome=(PixelMapProcedural.Biome)e.newValue);fold.Add(preset);
            var seed=new TextField("Semilla"){value=baseSeed};seed.RegisterValueChangedCallback(e=>baseSeed=e.newValue);fold.Add(seed);
            AddInt(fold,"Relieve · px",baseRelief,v=>baseRelief=Mathf.Clamp(v,0,128));AddInt(fold,"Escala · px",baseScale,v=>baseScale=Mathf.Clamp(v,8,256));
            var coverage=new Slider("Tierra en islas",.05f,.95f){value=baseCoverage,showInputField=true};coverage.RegisterValueChangedCallback(e=>baseCoverage=e.newValue);fold.Add(coverage);
            fold.Add(Caption("Usa las dimensiones del documento actual. Se crea un documento nuevo; tu mapa actual se conserva.","hint"));
            var row=Row();row.style.flexWrap=Wrap.Wrap;fold.Add(row);row.Add(Button("Previsualizar base",PreviewGeneratedBase));row.Add(Button("Otra semilla",()=>{baseSeed=Guid.NewGuid().ToString("N").Substring(0,8);seed.SetValueWithoutNotify(baseSeed);PreviewGeneratedBase();}));
            basePreview=new UnityEngine.UIElements.Image{scaleMode=ScaleMode.ScaleToFit};basePreview.style.height=180;fold.Add(basePreview);
            generationInfo=Caption("Genera una vista antes de crear el mapa.","hint");fold.Add(generationInfo);fold.Add(Button("Crear mapa desde esta base…",CommitGeneratedBase));
        }
        private void DisposeGeneratedBase(){if(generatedBase!=null)DestroyImmediate(generatedBase);if(generatedThumbnail!=null)DestroyImmediate(generatedThumbnail);generatedBase=null;generatedThumbnail=null;}
        private void PreviewGeneratedBase()
        {
            DisposeGeneratedBase();generatedBase=PixelMapProcedural.Generate(baseBiome,baseSeed,document!=null?document.width:newWidth,document!=null?document.height:newHeight,document!=null?document.pixelsPerUnit:newPPU,baseRelief,baseScale,baseCoverage);
            generatedThumbnail=PixelMapBaker.PaintTexture(generatedBase);basePreview.image=generatedThumbnail;generationInfo.text=$"{generatedBase.width} × {generatedBase.height} px · {generatedBase.generationPreset} · semilla {generatedBase.generationSeed}";
        }
        private void CommitGeneratedBase()
        {
            if(generatedBase==null){status.text="Previsualiza una base primero.";return;}
            string path=EditorUtility.SaveFilePanelInProject("Crear base editable",generatedBase.name,"asset","Guardar un nuevo mapa");if(string.IsNullOrEmpty(path))return;
            EndStroke();var created=Instantiate(generatedBase);created.name=System.IO.Path.GetFileNameWithoutExtension(path);created.exportedPrefab=null;
            AssetDatabase.CreateAsset(created,path);AssetDatabase.SaveAssetIfDirty(created);document=created;ClearSelection();selectedProps.Clear();selectedProp=-1;BuildInterface();RecordOperation("Crear base procedural editable");
        }
    }
}
