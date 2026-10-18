// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private int selectedColorPalette;
        [SerializeField] private bool patternMask=true,autoPatternSize=true;
        [SerializeField] private int patternPixels=16;
        [SerializeField] private Color patternInk=new Color32(62,137,72,255);
        [SerializeField] private bool patternBlend;
        [SerializeField] private float patternBlendAmount=.35f;
        [SerializeField] private Vector2Int patternOffset;
        [SerializeField] private int sampledEdgePixels;
        private VisualElement paletteColors;
        private ColorField paletteTarget;
        private Label paletteDestination,patternInfo;
        private UnityEngine.UIElements.Image patternPreview;
        private Texture2D patternPreviewTexture;

        private void AddPaletteField(VisualElement parent,ColorField field,string title)
        {
            var row=Row();row.style.alignItems=Align.Center;field.style.flexGrow=1;field.style.minWidth=0;row.Add(field);
            var target=Button("Paleta",()=>SetPaletteTarget(field,title));target.tooltip="Elegir "+title.ToLowerInvariant()+" con los colores de abajo";target.style.width=68;target.style.flexShrink=0;row.Add(target);parent.Add(row);
            field.RegisterCallback<FocusInEvent>(_=>SetPaletteTarget(field,title));
        }
        private void SetPaletteTarget(ColorField field,string title)
        {
            paletteTarget?.RemoveFromClassList("palette-target");paletteTarget=field;paletteTarget.AddToClassList("palette-target");
            if(paletteDestination!=null)paletteDestination.text="Aplicar a: "+title;
        }
        private void BuildColorPaletteBar(VisualElement parent)
        {
            var bar=new VisualElement();bar.AddToClassList("color-palette-bar");parent.Add(bar);bar.Add(Heading("COLORES"));
            var options=Row();options.style.alignItems=Align.Center;bar.Add(options);
            var names=new List<string>();foreach(var palette in PixelMapColorPalettes.All)names.Add(palette.name);
            var choose=new PopupField<string>(names,Mathf.Clamp(selectedColorPalette,0,names.Count-1));choose.style.width=150;
            choose.RegisterValueChangedCallback(_=>{selectedColorPalette=choose.index;RefreshColorPalette();});options.Add(choose);
            paletteDestination=new Label("Aplicar a: Pincel");paletteDestination.AddToClassList("hint");bar.Add(paletteDestination);
            options.Add(Button("Pincel",()=>SetPaletteTarget(tintField,"Pincel")));
            options.Add(Button("Fuente",()=>Application.OpenURL(PixelMapColorPalettes.All[selectedColorPalette].url)));
            var scroll=new ScrollView();scroll.style.flexGrow=1;scroll.style.minHeight=0;bar.Add(scroll);
            paletteColors=Row("palette-colors");scroll.Add(paletteColors);RefreshColorPalette();SetPaletteTarget(tintField,"Pincel");
        }
        private void RefreshColorPalette()
        {
            if(paletteColors==null)return;paletteColors.Clear();
            var palette=PixelMapColorPalettes.All[Mathf.Clamp(selectedColorPalette,0,PixelMapColorPalettes.All.Length-1)];
            foreach(var swatch in palette.colors)
            {
                var picked=swatch;var button=new Button(()=>{if(paletteTarget==null||paletteTarget.panel==null)SetPaletteTarget(tintField,"Pincel");paletteTarget.value=picked;});
                button.AddToClassList("color-chip");button.style.backgroundColor=swatch;button.tooltip="#"+ColorUtility.ToHtmlStringRGB(swatch)+" · "+palette.name+" · "+palette.author;paletteColors.Add(button);
            }
        }
        private void BuildPatternControls(VisualElement parent)
        {
            var pattern=new Foldout{text="Patrón · tamaño y colores",value=true};parent.Add(pattern);
            var automatic=new Toggle("Ajustar al pincel"){value=autoPatternSize};automatic.RegisterValueChangedCallback(e=>{autoPatternSize=e.newValue;ReadTile();});pattern.Add(automatic);
            var size=new SliderInt("Repetición · px",2,128){value=patternPixels,showInputField=true};size.RegisterValueChangedCallback(e=>{patternPixels=e.newValue;autoPatternSize=false;automatic.SetValueWithoutNotify(false);ReadTile();});pattern.Add(size);
            var offset=new Vector2IntField("Desplazamiento"){value=patternOffset};offset.RegisterValueChangedCallback(e=>{patternOffset=e.newValue;ReadTile();});pattern.Add(offset);
            var mask=new Toggle("Teñir con dos colores"){value=patternMask};mask.RegisterValueChangedCallback(e=>{patternMask=e.newValue;ReadTile();});pattern.Add(mask);
            var ink=new ColorField("Color del patrón"){value=patternInk,showAlpha=false};ink.RegisterValueChangedCallback(e=>{patternInk=e.newValue;ReadTile();});AddPaletteField(pattern,ink,"Patrón");
            var blend=new Toggle("Mezclar con color de fondo"){value=patternBlend};blend.RegisterValueChangedCallback(e=>{patternBlend=e.newValue;ReadTile();});pattern.Add(blend);
            var amount=new Slider("Mezcla",0,1){value=patternBlendAmount,showInputField=true};amount.SetEnabled(patternBlend);blend.RegisterValueChangedCallback(e=>amount.SetEnabled(e.newValue));amount.RegisterValueChangedCallback(e=>{patternBlendAmount=e.newValue;ReadTile();});pattern.Add(amount);
            pattern.Add(Button("Aplicar estilo a capa activa",()=>{if(ActiveLayer==null||ActiveLayer.locked)return;ChangeMap(()=>CopyBrushPattern(ActiveLayer),"Cambiar patrón de capa");RefreshLayerStyle();}));
            patternPreview=new UnityEngine.UIElements.Image{scaleMode=ScaleMode.ScaleToFit};patternPreview.AddToClassList("pattern-preview");pattern.Add(patternPreview);patternInfo=Caption("Elige un patrón. Se adapta sin cambiar el archivo original.","hint");pattern.Add(patternInfo);
        }
        private void AdaptPattern(Color32[] source,int width,int height)
        {
            int period=autoPatternSize?Mathf.Clamp(Mathf.NextPowerOfTwo(radius*2+1),4,64):Mathf.Clamp(patternPixels,2,128);
            tileWidth=period;tileHeight=Mathf.Clamp(Mathf.RoundToInt(period*(float)height/width),2,256);
            tilePixels=PixelMapPatterns.Sample(source,width,height,tileWidth,tileHeight,patternMask,color,patternInk,patternBlend?patternBlendAmount:0);
            RefreshPatternPreview(width,height);
        }
        private void RefreshPatternPreview(int sourceWidth=0,int sourceHeight=0)
        {
            if(patternPreviewTexture!=null)DestroyImmediate(patternPreviewTexture);patternPreviewTexture=null;
            if(patternPreview==null)return;
            if(tilePixels==null){patternPreview.image=null;if(patternInfo!=null)patternInfo.text="Sin patrón: usa color liso.";return;}
            const int size=64;patternPreviewTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};
            var repeated=new Color32[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)repeated[y*size+x]=BrushPatternColor(x,y);
            patternPreviewTexture.SetPixels32(repeated);patternPreviewTexture.Apply();patternPreview.image=patternPreviewTexture;
            if(patternInfo!=null)patternInfo.text=$"{sourceWidth} × {sourceHeight} → {tileWidth} × {tileHeight} px"+(patternMask&&ColorUtility.ToHtmlStringRGB(color)==ColorUtility.ToHtmlStringRGB(patternInk)?"\nEl fondo y el dibujo tienen el mismo color.":patternMask?" · máscara de dos colores":" · colores originales");
        }
        private Color32 BrushPatternColor(int x,int y)
        {
            if(tilePixels==null)return color;int tx=((x-patternOffset.x)%tileWidth+tileWidth)%tileWidth,ty=((y-patternOffset.y)%tileHeight+tileHeight)%tileHeight;return tilePixels[ty*tileWidth+tx];
        }
    }
}
