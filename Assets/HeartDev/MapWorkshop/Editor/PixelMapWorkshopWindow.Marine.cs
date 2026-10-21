// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using System;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private void BuildMarineControls(VisualElement parent,PixelMapDocument.AmbientZone zone)
        {
            var motion=PixelMapBaker.AmbientMotionFor(zone);bool fish=PixelMapBaker.IsMarineSwarm(motion),light=PixelMapBaker.IsMarineLight(motion),jelly=motion==PixelMapDocument.AmbientMotion.Medusas;
            if(!fish&&!light&&!jelly&&motion!=PixelMapDocument.AmbientMotion.BurbujasArrecife)return;
            var panel=new Foldout{text="Ajustes marinos",value=true};parent.Add(panel);
            void Int(string label,int value,int min,int max,Action<int> set)=>AddInt(panel,label,value,v=>ChangeAmbient(()=>set(Mathf.Clamp(v,min,max)),label));
            void Float(string label,float value,float min,float max,Action<float> set){var slider=new Slider(label,min,max){value=value,showInputField=true};slider.RegisterValueChangedCallback(e=>ChangeAmbient(()=>set(e.newValue),label));panel.Add(slider);}
            void Toggle(string label,bool value,Action<bool> set){var toggle=new Toggle(label){value=value};toggle.RegisterValueChangedCallback(e=>ChangeAmbient(()=>set(e.newValue),label));panel.Add(toggle);}
            if(fish)
            {
                Int("NÃºmero de grupos",zone.schoolCount,1,16,v=>zone.schoolCount=v);Int("Peces por grupo",zone.fishPerSchool,1,32,v=>zone.fishPerSchool=v);
                panel.Add(Caption("El mÃ¡ximo por zona y el presupuesto global limitan la cantidad final. Si no cabe un pez en el volumen, no se muestra.","hint"));
                Float("SeparaciÃ³n Â· px",zone.fishSpacingPixels,1,48,v=>zone.fishSpacingPixels=v);Float("Respuesta de giro",zone.turnSpeed,.1f,8,v=>zone.turnSpeed=v);
                Int("Margen sobre el fondo Â· px",zone.swimBottomPixels,0,256,v=>zone.swimBottomPixels=v);Int("LÃ­mite superior Â· px",zone.swimTopPixels,1,256,v=>zone.swimTopPixels=v);
                panel.Add(Caption("Alturas relativas al terreno. Vincula una zona de agua para limitar tambiÃ©n el recorrido a su superficie y mÃ¡scara.","hint"));
                var names=new System.Collections.Generic.List<string>{"Volumen libre"};names.AddRange(document.waterZones.Select((w,i)=>(i+1)+" Â· "+w.name));
                int current=document.waterZones.FindIndex(w=>w.id==zone.marineWaterZoneId)+1;var water=new PopupField<string>("Zona de agua",names,Mathf.Clamp(current,0,names.Count-1));water.RegisterValueChangedCallback(e=>ChangeAmbient(()=>zone.marineWaterZoneId=water.index>0?document.waterZones[water.index-1].id:"","LÃ­mite acuÃ¡tico"));panel.Add(water);
                var route=new EnumField("Recorrido",zone.marineRoute);route.RegisterValueChangedCallback(e=>ChangeAmbient(()=>zone.marineRoute=(MarineRoute)e.newValue,"Recorrido de peces"));panel.Add(route);
                Toggle("Evitar props grandes",zone.avoidLargeProps,v=>zone.avoidLargeProps=v);Toggle("Sprite original mira a la derecha",zone.fishFacesRight,v=>zone.fishFacesRight=v);
            }
            if(fish||jelly){Float("OndulaciÃ³n Â· px",zone.tailSwayPixels,0,5,v=>zone.tailSwayPixels=v);Float("Ritmo de ondulaciÃ³n",zone.tailFrequency,.5f,10,v=>zone.tailFrequency=v);}
            if(light){Float("Fuerza de luz",zone.marineLightStrength,0,1,v=>zone.marineLightStrength=v);Float("Escala Â· px",zone.marinePatternPixels,2,64,v=>zone.marinePatternPixels=v);panel.Add(Caption("AnimaciÃ³n en shader, con pÃ­xeles del mismo tamaÃ±o que el mapa. No necesita sprite.","hint"));}
            if(motion==PixelMapDocument.AmbientMotion.BurbujasArrecife)Int("Puntos de burbujas",zone.bubbleVents,1,4,v=>zone.bubbleVents=v);
        }
    }
}
