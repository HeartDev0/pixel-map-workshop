// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private void AppearanceSlider(VisualElement parent,string title,float min,float max,float initial,System.Action<float> setter)
        {
            var slider=new Slider(title,min,max){value=initial,showInputField=true};slider.RegisterValueChangedCallback(e=>ChangeMap(()=>setter(e.newValue),title));parent.Add(slider);
        }
        private void BuildDistanceGradientFields(VisualElement parent,PixelMapDocument.DistanceGradient g,string title)
        {
            if(g==null)return;var fold=new Foldout{text=title,value=false};parent.Add(fold);
            var enabled=new Toggle("Degradado de 3 colores"){value=g.enabled};enabled.RegisterValueChangedCallback(e=>ChangeMap(()=>g.enabled=e.newValue,"Degradado de superficie"));fold.Add(enabled);
            void Color(string label,UnityEngine.Color initial,System.Action<UnityEngine.Color> setter){var field=new ColorField(label){value=initial,showAlpha=false};field.RegisterValueChangedCallback(e=>ChangeMap(()=>setter(e.newValue),"Color del degradado"));AddPaletteField(fold,field,title+" · "+label);}
            Color("Cerca",g.near,c=>g.near=c);Color("Medio",g.middle,c=>g.middle=c);Color("Lejos",g.far,c=>g.far=c);
            AppearanceSlider(fold,"Intensidad",0,1,g.strength,v=>g.strength=v);
            var detail=new Toggle("Conservar detalle del patrón"){value=g.keepPattern};detail.RegisterValueChangedCallback(e=>ChangeMap(()=>g.keepPattern=e.newValue,"Detalle del degradado"));fold.Add(detail);
        }
        private void BuildAtmosphereControls(VisualElement parent)
        {
            var gradients=new Foldout{text="Degradados por distancia",value=false};parent.Add(gradients);
            if(document!=null)
            {
                BuildDistanceGradientFields(gradients,document.groundGradient,"Suelo / césped pintado");BuildDistanceGradientFields(gradients,document.pathGradient,"Camino");
                AppearanceSlider(gradients,"Distancia cerca",0,30,document.gradientNear,v=>document.gradientNear=v);AppearanceSlider(gradients,"Distancia lejos",.1f,60,document.gradientFar,v=>document.gradientFar=v);
            }
            gradients.Add(Caption("Depende de la distancia a la cámara 3D. No modifica clusters ni props. Cualquier otra textura puede tener su degradado en Estilo editable de su capa.","hint"));
            var clouds=new Foldout{text="Nubes atmosféricas",value=false};parent.Add(clouds);
            var enabled=new Toggle("Mostrar capa de nubes"){value=document!=null&&document.cloudsEnabled};enabled.RegisterValueChangedCallback(e=>ChangeMap(()=>document.cloudsEnabled=e.newValue,"Nubes atmosféricas"));clouds.Add(enabled);
            var texture=new ObjectField("Patrón"){objectType=typeof(Texture2D),allowSceneObjects=false,value=document!=null?document.cloudPattern:null};texture.RegisterValueChangedCallback(e=>ChangeMap(()=>document.cloudPattern=e.newValue as Texture2D,"Patrón de nubes"));clouds.Add(texture);
            var preview=new UnityEngine.UIElements.Image{image=document!=null?document.cloudPattern:null,scaleMode=ScaleMode.ScaleToFit};preview.style.width=72;preview.style.height=60;clouds.Add(preview);texture.RegisterValueChangedCallback(e=>preview.image=e.newValue as Texture2D);
            clouds.Add(Caption("Ambas capas comparten el patrón. Una malla por capa, sin partículas ni sombras.","hint"));
            var alpha=new Toggle("Usar transparencia del patrón"){value=document==null||document.cloudAlphaMask};alpha.RegisterValueChangedCallback(e=>ChangeMap(()=>document.cloudAlphaMask=e.newValue,"Máscara de nubes"));clouds.Add(alpha);
            var invert=new Toggle("Invertir patrón"){value=document!=null&&document.cloudInvert};invert.RegisterValueChangedCallback(e=>ChangeMap(()=>document.cloudInvert=e.newValue,"Invertir nubes"));clouds.Add(invert);
            AddWaterColor(clouds,"Color de nube",document!=null?document.cloudColor:UnityEngine.Color.white,c=>document.cloudColor=c);
            AppearanceSlider(clouds,"Opacidad",0,1,document!=null?document.cloudOpacity:.16f,v=>document.cloudOpacity=v);AppearanceSlider(clouds,"Cobertura",.05f,1,document!=null?document.cloudCoverage:.45f,v=>document.cloudCoverage=v);
            var size=new SliderInt("Patrón · px",8,256){value=document!=null?document.cloudSizePixels:96,showInputField=true};size.RegisterValueChangedCallback(e=>ChangeMap(()=>document.cloudSizePixels=e.newValue,"Tamaño de nubes"));clouds.Add(size);
            var height=new SliderInt("Altura · px",0,512){value=document!=null?document.cloudHeightPixels:96,showInputField=true};height.RegisterValueChangedCallback(e=>ChangeMap(()=>document.cloudHeightPixels=e.newValue,"Altura de nubes"));clouds.Add(height);
            AppearanceSlider(clouds,"Velocidad · px/s",0,8,document!=null?document.cloudSpeed:.5f,v=>document.cloudSpeed=v);
            var far=new Foldout{text="Capa lejana · gris",value=true};clouds.Add(far);
            var farEnabled=new Toggle("Mostrar capa lejana"){value=document==null||document.farCloudsEnabled};farEnabled.RegisterValueChangedCallback(e=>ChangeMap(()=>document.farCloudsEnabled=e.newValue,"Capa lejana de nubes"));far.Add(farEnabled);
            AddWaterColor(far,"Color lejano",document!=null?document.farCloudColor:new Color32(146,155,169,255),c=>document.farCloudColor=c);
            AppearanceSlider(far,"Opacidad",0,1,document!=null?document.farCloudOpacity:.18f,v=>document.farCloudOpacity=v);
            AppearanceSlider(far,"Velocidad · px/s",0,8,document!=null?document.farCloudSpeed:.2f,v=>document.farCloudSpeed=v);
            var farSize=new SliderInt("Patrón · px",8,512){value=document!=null?document.farCloudSizePixels:144,showInputField=true};farSize.RegisterValueChangedCallback(e=>ChangeMap(()=>document.farCloudSizePixels=e.newValue,"Tamaño de nubes lejanas"));far.Add(farSize);
            var separation=new SliderInt("Altura adicional · px",1,512){value=document!=null?document.farCloudHeightOffsetPixels:64,showInputField=true};separation.RegisterValueChangedCallback(e=>ChangeMap(()=>document.farCloudHeightOffsetPixels=e.newValue,"Separación de nubes"));far.Add(separation);
        }
        private void BuildWaterAppearanceControls(VisualElement parent)
        {
            if(document==null)return;
            parent.Add(Caption("Degradado fijo de la orilla al centro del río. Solo se anima la espuma.","hint"));
            var width=new SliderInt("Degradado · px",1,64){value=document.waterGradientPixels,showInputField=true};
            width.RegisterValueChangedCallback(e=>ChangeMap(()=>document.waterGradientPixels=e.newValue,"Ancho del degradado del agua"));parent.Add(width);
            AppearanceSlider(parent,"Velocidad espuma",0,6,document.waterPatternSpeed,v=>document.waterPatternSpeed=v);
            AppearanceSlider(parent,"Intensidad espuma",0,1,document.waterFoamStrength,v=>document.waterFoamStrength=v);
        }
    }
}
