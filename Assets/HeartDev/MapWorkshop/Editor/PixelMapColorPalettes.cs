// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    public static class PixelMapColorPalettes
    {
        public sealed class Palette
        {
            public readonly string name,author,url;
            public readonly Color[] colors;
            public Palette(string name,string author,string slug,string hex)
            {
                this.name=name;this.author=author;url="https://lospec.com/palette-list/"+slug;
                colors=Array.ConvertAll(hex.Split(' '),value=>{ColorUtility.TryParseHtmlString("#"+value,out var color);return color;});
            }
        }
        // Original color lists from their respective Lospec author pages (offline at runtime).
        public static readonly Palette[] All={
            new Palette("EDG32","ENDESGA","endesga-32","be4a2f d77643 ead4aa e4a672 b86f50 733e39 3e2731 a22633 e43b44 f77622 feae34 fee761 63c74d 3e8948 265c42 193c3e 124e89 0099db 2ce8f5 ffffff c0cbdc 8b9bb4 5a6988 3a4466 262b44 181425 ff0044 68386c b55088 f6757a e8b796 c28569"),
            new Palette("HEPT32","ENDESGA","hept32","000000 180d2f 353658 686b72 8b97b6 c5cddb ffffff 5ee9e9 2890dc 1831a7 053239 005f41 08b23b 47f641 e8ff75 fbbe82 de9751 b66831 8a4926 461c14 1e090d 720d0d 813704 da2424 ef6e10 ecab11 ece910 f78d8d f94e6d c12458 841252 3d083b"),
            new Palette("Rosy-42","PineappleOnPizza","rosy-42","21181b 3b2027 7d3833 ab5130 cf752b f0b541 ffee83 c8d45d 63ab3f 3b7d4f 2f5753 283540 1b1f21 2b2b45 3a3f5e 4c6885 4fa4b8 92e8c0 f5ffe8 dfe0e8 a3a7c2 686f99 404973 2c354d 14182e 4b1d52 692464 9c2a70 cc2f7b ff5277 ffc2a1 ff8933 e64539 ad2f45 781d4f 4f1d4c 291d2b 3d2936 52333f 8f4d57 bd6a62 ffae70"),
            new Palette("AAP-64","Adigun A. Polack","aap-64","060608 141013 3b1725 73172d b4202a df3e23 fa6a0a f9a31b ffd541 fffc40 d6f264 9cdb43 59c135 14a02e 1a7a3e 24523b 122020 143464 285cc4 249fde 20d6c7 a6fcdb ffffff fef3c0 fad6b8 f5a097 e86a73 bc4a9b 793a80 403353 242234 221c1a 322b28 71413b bb7547 dba463 f4d29c dae0ea b3b9d1 8b93af 6d758d 4a5462 333941 422433 5b3138 8e5252 ba756a e9b5a3 e3e6ff b9bffb 849be4 588dbe 477d85 23674e 328464 5daf8d 92dcba cdf7e2 e4d2aa c7b08b a08662 796755 5a4e44 423934")
        };
    }
}
