// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private bool smartDecoration,avoidWater=true,avoidPaths=true;
        [SerializeField] private int decorationSpacing=5,maxDecorationSlope=2;
        [SerializeField] private List<Sprite> decorationMix=new List<Sprite>();
        private readonly Dictionary<Vector2Int,List<Vector2Int>> placementGrid=new Dictionary<Vector2Int,List<Vector2Int>>();
        private readonly List<Sprite> propCandidates=new List<Sprite>(),grassCandidates=new List<Sprite>();
        private VisualElement mixList;
        private void BuildSmartDecorationControls(VisualElement parent)
        {
            var fold=new Foldout{text="Decoración inteligente",value=false};parent.Add(fold);
            var smart=new Toggle("Pintar mezcla de sprites"){value=smartDecoration};smart.RegisterValueChangedCallback(e=>smartDecoration=e.newValue);fold.Add(smart);
            var spacing=new SliderInt("Separación · px",2,32){value=decorationSpacing,showInputField=true};spacing.RegisterValueChangedCallback(e=>decorationSpacing=e.newValue);fold.Add(spacing);
            var amount=new Slider("Densidad",.02f,1){value=density,showInputField=true};amount.RegisterValueChangedCallback(e=>density=e.newValue);fold.Add(amount);
            var water=new Toggle("Evitar agua"){value=avoidWater};water.RegisterValueChangedCallback(e=>avoidWater=e.newValue);fold.Add(water);
            var path=new Toggle("Evitar caminos"){value=avoidPaths};path.RegisterValueChangedCallback(e=>avoidPaths=e.newValue);fold.Add(path);
            var slope=new SliderInt("Desnivel máximo · px",0,16){value=maxDecorationSlope,showInputField=true};slope.RegisterValueChangedCallback(e=>maxDecorationSlope=e.newValue);fold.Add(slope);
            var seed=new IntegerField("Semilla"){value=document!=null?document.seed:12345};seed.RegisterValueChangedCallback(e=>ChangeMap(()=>document.seed=e.newValue,"Semilla de decoración"));fold.Add(seed);
            fold.Add(Button("Añadir sprite seleccionado",()=>AddDecorationMix(brush==Brush.Cesped?grass:figure)));fold.Add(Caption("Mayús + clic sobre una miniatura añade o quita ese sprite de la mezcla. Se conserva la escala de píxel, sin tamaños aleatorios.","hint"));
            mixList=new VisualElement();fold.Add(mixList);RefreshDecorationMix();
        }
        private void AddDecorationMix(Sprite sprite){if(sprite==null)return;if(decorationMix.Contains(sprite))decorationMix.Remove(sprite);else decorationMix.Add(sprite);RefreshDecorationMix();}
        private void RefreshDecorationMix()
        {
            if(mixList==null)return;mixList.Clear();decorationMix.RemoveAll(s=>s==null);
            foreach(var sprite in decorationMix){var row=Row();row.style.alignItems=Align.Center;var image=new UnityEngine.UIElements.Image{sprite=sprite,scaleMode=ScaleMode.ScaleToFit};image.style.width=28;image.style.height=32;row.Add(image);var label=new Label(library?.entries.Find(e=>e.sprite==sprite)?.title??sprite.name);label.style.flexGrow=1;row.Add(label);row.Add(new Button(()=>{decorationMix.Remove(sprite);RefreshDecorationMix();}){text="×",tooltip="Quitar de la mezcla"});mixList.Add(row);}
        }
        private void BeginPlacementIndex()
        {
            placementGrid.Clear();foreach(var prop in document.decorations)IndexPlacement(prop.pixel);propCandidates.Clear();grassCandidates.Clear();
            foreach(var sprite in decorationMix)if(sprite!=null){propCandidates.Add(sprite);if(IsGrassSprite(sprite))grassCandidates.Add(sprite);}
        }
        private bool IsGrassSprite(Sprite sprite)=>library!=null&&library.entries.Exists(e=>e.sprite==sprite&&e.category==PixelMapPropLibrary.Category.Cesped);
        private float DefaultPropPitch(Sprite sprite)=>library?.entries.Find(e=>e.sprite==sprite)?.pitch??0;
        private Vector2Int PlacementCell(Vector2Int p)=>new Vector2Int(Mathf.FloorToInt(p.x/(float)Mathf.Max(2,decorationSpacing)),Mathf.FloorToInt(p.y/(float)Mathf.Max(2,decorationSpacing)));
        private void IndexPlacement(Vector2Int p){var cell=PlacementCell(p);if(!placementGrid.TryGetValue(cell,out var list)){list=new List<Vector2Int>();placementGrid.Add(cell,list);}list.Add(p);}
        private bool CanPlaceDecoration(int x,int y)
        {
            if(selectionMask!=null&&!SelectionContains(x,y))return false;byte surface=document.Surface(document.Index(x,y));if(avoidWater&&surface==2||avoidPaths&&surface==1)return false;
            float center=document.RawSurfaceHeight(x,y)*document.pixelsPerUnit;
            for(int i=0;i<4;i++){int dx=i==0?-2:i==1?2:0,dy=i==2?-2:i==3?2:0;if(Mathf.Abs(document.RawSurfaceHeight(x+dx,y+dy)*document.pixelsPerUnit-center)>maxDecorationSlope)return false;}
            var p=new Vector2Int(x,y);var cell=PlacementCell(p);int square=decorationSpacing*decorationSpacing;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(placementGrid.TryGetValue(cell+new Vector2Int(dx,dy),out var list))foreach(var other in list)if((other-p).sqrMagnitude<square)return false;return true;
        }
        private void PlaceSmartDecoration(int x,int y,bool grassOnly)
        {
            if(document.decorations.Count>=20000||Hash01(x,y)>density*.16f||!CanPlaceDecoration(x,y))return;
            var candidates=grassOnly?grassCandidates:propCandidates;Sprite chosen=candidates.Count>0?candidates[Mathf.Min(candidates.Count-1,(int)(Hash01(x+831,y+313)*candidates.Count))]:grassOnly?grass:figure;if(chosen==null)return;
            var pixel=new Vector2Int(x,y);if(!occupied.Add(document.Index(x,y)))return;
            document.decorations.Add(new PixelMapDocument.Decoration{sprite=chosen,pixel=pixel,grass=grassOnly||IsGrassSprite(chosen),pitch=DefaultPropPitch(chosen),layerId=activeLayerId});IndexPlacement(pixel);
        }
        private void StampDecoration(Vector2Int center)
        {
            for(int y=Mathf.Max(0,center.y-radius);y<=Mathf.Min(document.height-1,center.y+radius);y++)for(int x=Mathf.Max(0,center.x-radius);x<=Mathf.Min(document.width-1,center.x+radius);x++)if(BrushContains(x-center.x,y-center.y,x,y)&&touched.Add(document.Index(x,y)))PlaceSmartDecoration(x,y,false);
        }
        private void BuildTerrainFinishControls(VisualElement parent)
        {
            var fold=new Foldout{text="Acabado del terreno",value=false};parent.Add(fold);
            var enabled=new Toggle("Variación por patrón de color"){value=document!=null&&document.terrainVariation};enabled.RegisterValueChangedCallback(e=>ChangeMap(()=>document.terrainVariation=e.newValue,"Variación del terreno"));fold.Add(enabled);
            AddWaterColor(fold,"Color de variación",document!=null?document.variationColor:new Color32(82,151,61,255),c=>document.variationColor=c);
            var scale=new SliderInt("Grupo · px",1,16){value=document!=null?document.variationScale:3,showInputField=true};scale.RegisterValueChangedCallback(e=>ChangeMap(()=>document.variationScale=e.newValue,"Escala de variación"));fold.Add(scale);
            var densityField=new Slider("Cobertura",0,.6f){value=document!=null?document.variationDensity:.15f,showInputField=true};densityField.RegisterValueChangedCallback(e=>ChangeMap(()=>document.variationDensity=e.newValue,"Cobertura de variación"));fold.Add(densityField);
            fold.Add(Caption("Variación no destructiva sobre suelo. El agua y los caminos se conservan; el borde del camino tiene opciones sólido, tramado y doble.","hint"));
        }
    }
}
