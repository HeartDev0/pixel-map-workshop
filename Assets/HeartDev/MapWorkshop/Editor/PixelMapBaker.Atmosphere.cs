// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    public static partial class PixelMapBaker
    {
        private static void ConfigureGradient(Material material,PixelMapDocument map,Texture2D texture,bool enabled)
        {
            material.SetTexture("_GradientMap",texture);material.SetFloat("_DistanceGradient",enabled?1:0);material.SetVector("_GradientRange",new Vector4(map.gradientNear,Mathf.Max(map.gradientNear+.1f,map.gradientFar),0,0));
        }
        private static Texture2D DefaultCloudPattern(int seed)
        {
            const int n=64;var pixels=new Color32[n*n];var centers=new Vector2[]{new Vector2(13,24),new Vector2(23,33),new Vector2(34,27),new Vector2(44,32),new Vector2(51,23)};
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float density=0;for(int i=0;i<centers.Length;i++){var point=centers[i]+new Vector2((seed+i*3)%5-2,0);float radius=10+i%3*2;density=Mathf.Max(density,Mathf.Clamp01((radius-Vector2.Distance(new Vector2(x,y),point))/4));}
                pixels[y*n+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(density*255));
            }
            var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="AtmosphereCloudPattern",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};texture.SetPixels32(pixels);texture.Apply();return texture;
        }
        private static void BuildClouds(PixelMapDocument map,float curvature,float focus,Result result)
        {
            var texture=map.cloudPattern;if(texture==null){texture=DefaultCloudPattern(map.seed);result.owned.Add(texture);}
            if(map.farCloudsEnabled)BuildCloudLayer(map,curvature,focus,result,texture,true);
            BuildCloudLayer(map,curvature,focus,result,texture,false);
        }
        private static void BuildCloudLayer(PixelMapDocument map,float curvature,float focus,Result result,Texture2D texture,bool far)
        {
            string name=far?"AtmosphericClouds_Far":"AtmosphericClouds_Near";
            float speed=far?map.farCloudSpeed:map.cloudSpeed;
            float height=map.cloudHeightPixels+(far?Mathf.Max(1,map.farCloudHeightOffsetPixels):0);
            var material=new Material(Shader.Find("HeartDev/MapWorkshop/PixelClouds")){name=name,mainTexture=texture,renderQueue=far?3009:3010};result.owned.Add(material);
            material.SetVector("_CloudLayout",new Vector4(map.width/(float)map.pixelsPerUnit,map.pixelsPerUnit,far?1.9f:0,0));
            float mapWidth=map.width/(float)map.pixelsPerUnit;
            float horizonStart=Mathf.Max(mapWidth*.9f,curvature>0?.35f/curvature:mapWidth*1.5f);
            material.SetVector("_CloudHorizon",new Vector4(0,focus,horizonStart,horizonStart+mapWidth*.7f));
            material.SetColor("_CloudColor",far?map.farCloudColor:map.cloudColor);material.SetFloat("_Opacity",far?map.farCloudOpacity:map.cloudOpacity);material.SetFloat("_Coverage",map.cloudCoverage);material.SetFloat("_Speed",speed);material.SetFloat("_PatternPixels",far?map.farCloudSizePixels:map.cloudSizePixels);material.SetFloat("_AlphaMask",map.cloudPattern==null||map.cloudAlphaMask?1:0);material.SetFloat("_Invert",map.cloudInvert?1:0);
            // Keep the original 8 x 16 patch and its UVs intact. Add a skirt in all four
            // directions; every vertex still maps directly to the same document pixels.
            int skirt=map.cloudWrapAround?8:0,columns=8+skirt*2,rows=16+skirt*2;
            var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[columns*rows*6];
            float ppu=map.pixelsPerUnit;
            float padding=(Mathf.Max(horizonStart+mapWidth*.7f,Mathf.Max(map.cloudSizePixels,map.farCloudSizePixels)/ppu)+mapWidth*.5f)*ppu;
            float outerCurve=Mathf.Max(0,.35f/Mathf.Max(1,mapWidth)-Mathf.Max(0,curvature));
            float Coordinate(int i,int originalSegments,float start,float end)
            {
                if(i<skirt){float t=1-i/(float)skirt;return start-padding*t*t;}
                if(i>skirt+originalSegments){float t=(i-skirt-originalSegments)/(float)skirt;return end+padding*t*t;}
                return Mathf.Lerp(start,end,(i-skirt)/(float)originalSegments);
            }
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                int i=y*(columns+1)+x;float px=Coordinate(x,8,-map.width*.3f,map.width*1.3f),py=Coordinate(y,16,-32,map.height+32);
                float wx=(px-map.width*.5f)/ppu,wz=py/ppu;
                float outsideX=Mathf.Max(0,Mathf.Abs(wx)-mapWidth*.8f),outsideZ=Mathf.Max(0,Mathf.Max(-32-py,py-map.height-32))/ppu;
                float skirtDrop=map.cloudWrapAround?outerCurve*(outsideX*outsideX+outsideZ*outsideZ):0;
                vertices[i]=new Vector3(wx,height/ppu-curvature*((wz-focus)*(wz-focus)+.65f*wx*wx)-skirtDrop,wz);
                uv[i]=new Vector2(px,py)+(far?new Vector2(53,79):Vector2.zero);
                if(x<columns&&y<rows){int k=(y*columns+x)*6;triangles[k]=i;triangles[k+1]=i+columns+1;triangles[k+2]=i+1;triangles[k+3]=i+1;triangles[k+4]=i+columns+1;triangles[k+5]=i+columns+2;}
            }
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();MeshObject(mesh.name,mesh,material,result.root.transform,result,true,false);
            result.animatedClouds|=speed>0;result.animatedMaterials.Add(material);
        }
    }
}
