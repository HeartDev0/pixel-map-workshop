// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System;

namespace HeartDev.MapWorkshop
{
    public static class PixelMapWorkshopPerformance
    {
        private const string PreferenceKey="HeartDev.MapWorkshop.LightweightPreview";
        public static event Action Changed;

        public static bool Lightweight
        {
            get=>EditorPrefs.GetBool(PreferenceKey,IsModestMachine());
            set
            {
                if(Lightweight==value)return;
                EditorPrefs.SetBool(PreferenceKey,value);
                Changed?.Invoke();
            }
        }

        public static double PaintRefreshInterval=>Lightweight?1.0/20.0:1.0/30.0;
        public static int PreviewMeshStep=>Lightweight?2:1;

        public static double PreviewRepaintInterval(bool interacting)
        {
            // 30 fps maps cleanly to common 60 Hz displays and avoids uneven 24 fps camera cadence.
            if(interacting)return 1.0/30.0;
            return .1;
        }

        public static void Configure(PreviewRenderUtility preview,bool shadowsWhenFullQuality=false)
        {
            if(preview==null)return;
            var lights=preview.lights;
            for(int i=0;i<lights.Length;i++)
            {
                var light=lights[i];
                if(light==null)continue;
                var shadows=Lightweight||i!=0||!shadowsWhenFullQuality?LightShadows.None:LightShadows.Hard;
                if(light.shadows!=shadows)light.shadows=shadows;
            }
        }

        public static void Configure(GameObject root)
        {
            if(!Lightweight||root==null)return;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
            }
        }

        private static bool IsModestMachine()
        {
            return SystemInfo.systemMemorySize<=8192||SystemInfo.processorCount<=4||
                   SystemInfo.graphicsMemorySize>0&&SystemInfo.graphicsMemorySize<=2048;
        }
    }
}
