// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private VisualElement layerStylePanel;
        private void CopyBrushPattern(PixelMapDocument.PaintLayer layer)
        {
            layer.pattern=BrushStyle();
        }
        private void RefreshLayerStyle()
        {
            if(layerStylePanel==null)return;layerStylePanel.Clear();var layer=ActiveLayer;if(layer==null)return;
            if(layer.pattern==null)layer.pattern=new PixelMapDocument.PatternStyle();var p=layer.pattern;
            var fold=new Foldout{text="Estilo editable · "+layer.name,value=false};layerStylePanel.Add(fold);fold.SetEnabled(!layer.locked);
            var enabled=new Toggle("Patrón de capa"){value=p.enabled};enabled.RegisterValueChangedCallback(e=>ChangeMap(()=>p.enabled=e.newValue,"Activar patrón de capa"));fold.Add(enabled);
            var edge=new SliderInt("Transición · px",0,8){value=p.edgePixels,showInputField=true};edge.RegisterValueChangedCallback(e=>ChangeMap(()=>p.edgePixels=e.newValue,"Transición de material"));fold.Add(edge);
            fold.Add(Caption("La transición tramada mezcla materiales de suelo con la capa inferior. Caminos y agua usan sus propios bordes.","hint"));
            var texture=new ObjectField("Patrón"){value=p.texture,objectType=typeof(Texture2D),allowSceneObjects=false};texture.RegisterValueChangedCallback(e=>ChangeMap(()=>{p.texture=e.newValue as Texture2D;p.enabled=p.texture!=null;enabled.SetValueWithoutNotify(p.enabled);},"Cambiar patrón de capa"));fold.Add(texture);
            var period=new SliderInt("Repetición · px",2,128){value=p.size,showInputField=true};period.RegisterValueChangedCallback(e=>ChangeMap(()=>p.size=e.newValue,"Escala de patrón"));fold.Add(period);
            var offset=new Vector2IntField("Desplazamiento"){value=p.offset};offset.RegisterValueChangedCallback(e=>ChangeMap(()=>p.offset=e.newValue,"Desplazar patrón"));fold.Add(offset);
            var mask=new Toggle("Dos colores"){value=p.twoColors};mask.RegisterValueChangedCallback(e=>ChangeMap(()=>p.twoColors=e.newValue,"Teñir patrón de capa"));fold.Add(mask);
            void ColorField(string name,Color initial,System.Action<Color> setter){var field=new UnityEditor.UIElements.ColorField(name){value=initial,showAlpha=false};field.RegisterValueChangedCallback(e=>ChangeMap(()=>setter(e.newValue),"Color de patrón"));AddPaletteField(fold,field,name);}
            ColorField("Fondo de capa",p.background,c=>p.background=c);ColorField("Dibujo de capa",p.ink,c=>p.ink=c);
            var blend=new Toggle("Mezclar con fondo"){value=p.blend};blend.RegisterValueChangedCallback(e=>ChangeMap(()=>p.blend=e.newValue,"Mezcla de patrón"));fold.Add(blend);
            var amount=new Slider("Mezcla",0,1){value=p.blendAmount,showInputField=true};amount.SetEnabled(p.blend);blend.RegisterValueChangedCallback(e=>amount.SetEnabled(e.newValue));amount.RegisterValueChangedCallback(e=>ChangeMap(()=>p.blendAmount=e.newValue,"Mezcla de patrón"));fold.Add(amount);
            fold.Add(Caption("El estilo cambia toda la máscara de esta capa sin repintarla. Desactivarlo muestra la pintura original.","hint"));
            if(layer.gradient==null)layer.gradient=new PixelMapDocument.DistanceGradient();BuildDistanceGradientFields(fold,layer.gradient,"Degradado de esta textura");
        }
    }
}
