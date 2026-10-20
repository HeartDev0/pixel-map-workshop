// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    [CreateAssetMenu(menuName="HeartDev/Map Workshop/Paleta de props",fileName="PropPalette")]
    public sealed class PixelMapPropLibrary : ScriptableObject
    {
        public enum Category { Arboles,Rocas,Cesped,Otros,Construccion,Flores,Algas }
        [Serializable] public sealed class Entry
        {
            public Sprite sprite;
            public string title;
            public Category category;
            public bool repeatAlongLine;
            public float pitch;
        }
        public List<Entry> entries=new List<Entry>();
    }
}
