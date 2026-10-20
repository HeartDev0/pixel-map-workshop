// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private enum BrushShape { Circle,Square,Diamond,Cross,Ring,Spray }
        private enum PaintSurface { Suelo,Camino,Agua }
        [SerializeField] private BrushShape shape;
        [SerializeField] private PaintSurface surfaceMode;
        [SerializeField] private bool curvedPreview=true;
        [SerializeField] private float previewCurvature=.16f;
        private VisualElement pathOptions,waterOptions;
        private EnumField surfacePicker;
        private bool BrushContains(int dx,int dy,int x,int y)
        {
            int square=dx*dx+dy*dy;
            switch(shape)
            {
                case BrushShape.Square:return true;
                case BrushShape.Diamond:return Mathf.Abs(dx)+Mathf.Abs(dy)<=radius;
                case BrushShape.Cross:return Mathf.Abs(dx)<=Mathf.Max(1,radius/3)||Mathf.Abs(dy)<=Mathf.Max(1,radius/3);
                case BrushShape.Ring:return square<=radius*radius&&square>=Mathf.Max(0,radius-2)*Mathf.Max(0,radius-2);
                case BrushShape.Spray:return square<=radius*radius&&Hash01(x,y)<.3f;
                default:return square<=radius*radius;
            }
        }
        private void BuildBrushFeatures(VisualElement tools)
        {
            var shapes=Row("brush-shapes");tools.Add(shapes);var buttons=new List<Button>();
            string[] names={"Círculo","Cuadrado","Rombo","Cruz","Anillo","Dispersa"};
            for(int i=0;i<names.Length;i++)
            {
                int selected=i;var button=new Button(()=>{shape=(BrushShape)selected;for(int j=0;j<buttons.Count;j++)buttons[j].EnableInClassList("selected",j==selected);cursorShape=-1;}){tooltip=names[i]};
                button.AddToClassList("brush-shape-button");button.EnableInClassList("selected",(int)shape==i);
                var icon=new UnityEngine.UIElements.Image{pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.ScaleToFit};
                var vector=AssetDatabase.LoadAssetAtPath<VectorImage>($"Assets/HeartDev/MapWorkshop/Editor/Icons/shape-{i}.svg");if(vector!=null)icon.vectorImage=vector;else icon.image=PixelMapToolIcons.Shape(i);
                button.Add(icon);buttons.Add(button);shapes.Add(button);
            }
        }
        private void SelectSurface(PaintSurface mode)
        {
            if(surfaceMode!=mode){sampledGradient=null;sampledLayerId=null;surfaceMode=mode;ChooseToolLayer();}surfacePicker?.SetValueWithoutNotify(mode);
            if(pathOptions!=null)pathOptions.style.display=mode==PaintSurface.Camino?DisplayStyle.Flex:DisplayStyle.None;
            if(waterOptions!=null)waterOptions.style.display=mode==PaintSurface.Agua?DisplayStyle.Flex:DisplayStyle.None;
        }
        private void BuildSurfaceFeatures(VisualElement parent)
        {
            surfacePicker=new EnumField("Pintar como",surfaceMode);surfacePicker.RegisterValueChangedCallback(e=>SelectSurface((PaintSurface)e.newValue));parent.Add(surfacePicker);
            pathOptions=new VisualElement();parent.Add(pathOptions);
            var pathHeight=new SliderInt("Excavar (−) / elevar (+) · px",-32,32){value=document!=null?Mathf.Clamp(document.pathHeightOffsetPixels,-32,32):0,showInputField=true,tooltip="Los valores negativos hunden el camino en el terreno como una zanja; los positivos crean un resalte. Se aplica a todos los píxeles marcados como camino."};
            pathHeight.RegisterValueChangedCallback(e=>ChangeMap(()=>document.pathHeightOffsetPixels=e.newValue,"Excavación / elevación del camino"));pathOptions.Add(pathHeight);
            var pathSmooth=new SliderInt("Volver al nivel del terreno · px",0,16){value=document!=null?Mathf.Clamp(document.pathHeightSmoothPixels,0,16):0,showInputField=true,tooltip="Controla cuántos píxeles tarda la hendidura en volver suavemente a la altura original del terreno. Cero deja un corte duro."};
            pathSmooth.RegisterValueChangedCallback(e=>ChangeMap(()=>document.pathHeightSmoothPixels=e.newValue,"Suavizado de altura del camino"));pathOptions.Add(pathSmooth);
            pathOptions.Add(Caption("Negativo = hendidura · positivo = resalte. El suavizado normaliza el borde al terreno.","hint"));
            var outlined=new Toggle("Borde del camino"){value=document==null||document.pathOutline};outlined.RegisterValueChangedCallback(e=>ChangeMap(()=>document.pathOutline=e.newValue));pathOptions.Add(outlined);
            var outlineColor=new ColorField("Color del borde"){value=document!=null?document.outlineColor:new Color32(219,162,106,255),showAlpha=false};outlineColor.RegisterValueChangedCallback(e=>ChangeMap(()=>document.outlineColor=e.newValue));AddPaletteField(pathOptions,outlineColor,"Borde del camino");
            var width=new SliderInt("Borde · px",0,8){value=document!=null?document.outlinePixels:2,showInputField=true};width.RegisterValueChangedCallback(e=>ChangeMap(()=>document.outlinePixels=e.newValue));pathOptions.Add(width);
            pathOptions.Add(Button("Marcar camino ya pintado",()=>RecognizeSurface(1)));
            var edgeStyle=new PopupField<string>("Acabado",new List<string>{"Borde sólido","Transición tramada","Borde doble"},document!=null?Mathf.Clamp(document.outlineStyle,0,2):0);edgeStyle.RegisterValueChangedCallback(_=>ChangeMap(()=>document.outlineStyle=edgeStyle.index,"Acabado del camino"));pathOptions.Add(edgeStyle);
            AddWaterColor(pathOptions,"Borde exterior",document!=null?document.outlineOuterColor:new Color32(115,79,47,255),c=>document.outlineOuterColor=c);
            pathOptions.Add(Caption("Reconocer usa el color actual. El borde no borra la pintura que hay debajo.","hint"));
            waterOptions=new VisualElement();parent.Add(waterOptions);
            AddWaterColor(waterOptions,"Agua · orilla",document!=null?document.waterColor:new Color(0,.68f,.86f),c=>document.waterColor=c);
            AddWaterColor(waterOptions,"Agua media",document!=null?document.waterMiddle:new Color(0,.635f,.835f),c=>document.waterMiddle=c);
            AddWaterColor(waterOptions,"Agua · centro",document!=null?document.waterDeep:new Color(0,.59f,.8f),c=>document.waterDeep=c);
            AddWaterColor(waterOptions,"Espuma",document!=null?document.waterFoam:Color.white,c=>document.waterFoam=c);
            BuildWaterAppearanceControls(waterOptions);
            var fps=new SliderInt("Fotogramas / s",1,12){value=document!=null?Mathf.RoundToInt(document.waterFPS):6,showInputField=true};fps.RegisterValueChangedCallback(e=>ChangeMap(()=>document.waterFPS=e.newValue));waterOptions.Add(fps);
            var carve=new Toggle("Excavar cauce"){value=document==null||document.carveRivers};carve.RegisterValueChangedCallback(e=>ChangeMap(()=>document.carveRivers=e.newValue));waterOptions.Add(carve);
            var depth=new SliderInt("Profundidad · px",1,16){value=document!=null?document.riverDepthPixels:3,showInputField=true};depth.RegisterValueChangedCallback(e=>ChangeMap(()=>document.riverDepthPixels=e.newValue));waterOptions.Add(depth);
            AddWaterColor(waterOptions,"Tierra de la orilla",document!=null?document.riverBankColor:new Color32(130,79,54,255),c=>document.riverBankColor=c);
            var bands=new SliderInt("Colores de tierra",1,3){value=document!=null?document.riverBankBands:1,showInputField=true};bands.RegisterValueChangedCallback(e=>ChangeMap(()=>document.riverBankBands=e.newValue));waterOptions.Add(bands);
            AddWaterColor(waterOptions,"Tierra media / inferior",document!=null?document.riverBankMiddle:new Color32(164,105,66,255),c=>document.riverBankMiddle=c);
            AddWaterColor(waterOptions,"Tierra inferior · 3",document!=null?document.riverBankBottom:new Color32(93,57,42,255),c=>document.riverBankBottom=c);
            var equal=new Toggle("Franjas iguales"){value=document==null||document.riverBankEqualBands};equal.RegisterValueChangedCallback(e=>ChangeMap(()=>document.riverBankEqualBands=e.newValue));waterOptions.Add(equal);
            var heights=new VisualElement();waterOptions.Add(heights);heights.style.display=equal.value?DisplayStyle.None:DisplayStyle.Flex;
            equal.RegisterValueChangedCallback(e=>heights.style.display=e.newValue?DisplayStyle.None:DisplayStyle.Flex);
            AddInt(heights,"Franja superior · px",document!=null?document.riverTopBandPixels:1,v=>ChangeMap(()=>document.riverTopBandPixels=Mathf.Clamp(v,0,16)));
            AddInt(heights,"Franja media · px",document!=null?document.riverMiddleBandPixels:1,v=>ChangeMap(()=>document.riverMiddleBandPixels=Mathf.Clamp(v,0,16)));
            heights.Add(Caption("Se miden desde arriba. La última franja ocupa la profundidad restante.","hint"));
            var foamWidth=new SliderInt("Espuma · px",1,8){value=document!=null?document.riverBankPixels:3,showInputField=true};foamWidth.RegisterValueChangedCallback(e=>ChangeMap(()=>document.riverBankPixels=e.newValue));waterOptions.Add(foamWidth);
            waterOptions.Add(Button("Marcar agua ya pintada",()=>RecognizeSurface(2)));SelectSurface(surfaceMode);
        }
        private void AddWaterColor(VisualElement parent,string label,Color initial,Action<Color> setter)
        {
            var field=new ColorField(label){value=initial,showAlpha=false};field.RegisterValueChangedCallback(e=>ChangeMap(()=>setter(e.newValue)));AddPaletteField(parent,field,label);
        }
        private void BuildGrassMotionControls(VisualElement parent)
        {
            var sway=new Slider("Balanceo · px",0,2){value=document!=null?document.grassSwayPixels:1,showInputField=true};sway.RegisterValueChangedCallback(e=>ChangeMap(()=>document.grassSwayPixels=e.newValue));parent.Add(sway);
            var frames=new SliderInt("Viento · fps",2,10){value=document!=null?Mathf.RoundToInt(document.grassWindFPS):8,showInputField=true};frames.RegisterValueChangedCallback(e=>ChangeMap(()=>document.grassWindFPS=e.newValue));parent.Add(frames);
        }
        private void RecognizeSurface(byte type)
        {
            if(document==null||!document.IsValid)return;
            EndStroke();Undo.RegisterCompleteObjectUndo(document,"Reconocer superficie por color");document.EnsureSurfaces();if(!PrepareLayer())return;Color32 target=color;int found=0;
            for(int i=0;i<document.pixels.Length;i++)
            {
                Color32 p=document.PixelColor(i);if(Mathf.Abs(p.r-target.r)+Mathf.Abs(p.g-target.g)+Mathf.Abs(p.b-target.b)>6)continue;
                ActiveLayer.pixels[i]=p;ActiveLayer.surfaces[i]=type;found++;
            }
            EditorUtility.SetDirty(document);RefreshPaint();InvalidatePreview();status.text=$"{found:N0} píxeles reconocidos como {(type==1?"camino":"agua")}. Ctrl+Z deshace.";
        }
        private void BuildCurveControls(VisualElement parent)
        {
            var enabled=new Toggle("Curvatura del mundo"){value=curvedPreview};enabled.RegisterValueChangedCallback(e=>{curvedPreview=e.newValue;InvalidatePreview();});parent.Add(enabled);
            var amount=new Slider("Curvatura",0,.35f){value=previewCurvature,showInputField=true};amount.RegisterValueChangedCallback(e=>{previewCurvature=e.newValue;InvalidatePreview();});parent.Add(amount);
            parent.Add(Caption("La curvatura es de previsualización; el prefab conserva las alturas originales.","hint"));
        }
    }
}
