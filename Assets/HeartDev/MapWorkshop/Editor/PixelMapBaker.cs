// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace HeartDev.MapWorkshop
{
    public static partial class PixelMapBaker
    {
        public const int ChunkPixels=64;
        public sealed class Result : IDisposable
        {
            public GameObject root;
            public Texture2D texture;
            public Texture2D surfaceMask;
            public Material terrainMaterial;
            public bool animatedWater;
            public bool animatedGrass;
            public bool animatedClouds;
            public readonly List<ParticleSystem> ambientSystems=new List<ParticleSystem>();
            public readonly List<Material> animatedMaterials=new List<Material>();
            public int vertices,drawGroups;
            public readonly List<Object> owned=new List<Object>();
            public void Dispose()
            {
                if(root!=null)Object.DestroyImmediate(root);
                foreach(var item in owned)if(item!=null&&!AssetDatabase.Contains(item))Object.DestroyImmediate(item);
                owned.Clear();
            }
        }
        public static Texture2D PaintTexture(PixelMapDocument map,bool heights=false)
        {
            PixelMapPatterns.Prepare(map);
            var pixels=PixelMapSurfaces.OutlinedPixels(map);
            for(int y=0;y<map.height;y++)for(int x=0;x<map.width;x++)
            {
                int i=y*map.width+x;
                if(heights)
                {
                    float t=Mathf.Clamp01(.3f+map.elevations[i]/48f);
                    pixels[i]=Color.Lerp(new Color(.12f,.2f,.34f),new Color(1,.93f,.65f),t);
                }
                else
                {
                    if(map.Surface(i)==2)pixels[i]=map.WaterZoneAt(x,y)?.shore??PixelMapPatterns.WaterColorAt(map,i);
                }
            }
            var texture=new Texture2D(map.width,map.height,TextureFormat.RGBA32,false){name="GroundPixels",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,false);return texture;
        }
        private static Material MaterialFor(Texture texture,Result result,Dictionary<Texture,Material> cache)
        {
            if(cache.TryGetValue(texture,out var existing))return existing;
            var shader=Shader.Find("HeartDev/MapWorkshop/PixelMapUnlit");
            if(shader==null)throw new InvalidOperationException("Falta el shader PixelMapUnlit del taller.");
            var material=new Material(shader){name=texture.name+"_Pixel",mainTexture=texture};material.SetTexture("_BaseMap",texture);material.SetTexture("_SurfaceMap",Texture2D.blackTexture);material.EnableKeyword("_ALPHATEST_ON");cache.Add(texture,material);result.owned.Add(material);return material;
        }
        private static void MeshObject(string name,Mesh mesh,Material material,Transform parent,Result result,bool grass=false,bool castShadows=true)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.isStatic=true;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=grass||!castShadows?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;renderer.receiveShadows=!grass;
            mesh.RecalculateNormals();
            result.owned.Add(mesh);result.vertices+=mesh.vertexCount;result.drawGroups++;
        }
        public static Result Build(PixelMapDocument map,float curvature=0,float focus=0)
        {
            if(map==null||!map.IsValid)throw new InvalidOperationException("Selecciona un mapa válido.");
            var result=new Result();
            try
            {
                result.root=new GameObject(map.name+"_Baked");result.root.hideFlags=HideFlags.HideAndDontSave;
                result.texture=PaintTexture(map);result.owned.Add(result.texture);
                var materials=new Dictionary<Texture,Material>();
                var grassMaterials=new Dictionary<Texture,Material>();
                var terrain=MaterialFor(result.texture,result,materials);
                var gradients=new PixelMapGradients(map);var gradientTexture=gradients.Texture();result.owned.Add(gradientTexture);
                result.surfaceMask=PixelMapSurfaces.Mask(map,out result.animatedWater,gradients);result.owned.Add(result.surfaceMask);result.terrainMaterial=terrain;
                ConfigureGradient(terrain,map,gradientTexture,gradients.enabled);
                terrain.SetTexture("_SurfaceMap",result.surfaceMask);terrain.SetFloat("_SeparateWater",1);
                terrain.SetFloat("_TerrainLighting",1);
                Material water=null,bank=null;
                if(result.animatedWater)
                {
                    // Remove river texels from the land shadow silhouette as well as its visible pass.
                    var ground=result.texture.GetPixels32();for(int i=0;i<ground.Length;i++)if(map.Surface(i)==2)ground[i].a=0;
                    result.texture.SetPixels32(ground);result.texture.Apply(false,false);
                    water=new Material(Shader.Find("HeartDev/MapWorkshop/PixelMapWater")){name="RiverWater",mainTexture=result.texture};result.owned.Add(water);
                    water.SetTexture("_SurfaceMap",result.surfaceMask);water.SetFloat("_SeparateWater",1);water.SetFloat("_WaterOnly",1);
                    water.SetColor("_WaterColor",map.waterColor);water.SetColor("_WaterDeep",map.waterDeep);water.SetColor("_WaterFoam",map.waterFoam);water.SetFloat("_WaterFPS",map.waterFPS);
                    water.SetColor("_WaterMiddle",map.waterMiddle);water.SetVector("_WaterGradientRange",new Vector4(map.waterGradientNear,Mathf.Max(map.waterGradientNear+.1f,map.waterGradientFar),0,0));
                    water.SetFloat("_WaterPatternSpeed",map.waterPatternSpeed);water.SetFloat("_FoamStrength",map.waterFoamStrength);water.SetFloat("_WaveStrength",map.waterWaveStrength);
                    ConfigureGradient(water,map,gradientTexture,gradients.enabled);
                    result.animatedMaterials.Add(water);
                    int depth=Mathf.Max(1,map.riverDepthPixels),bands=Mathf.Clamp(map.riverBankBands,1,3);
                    var bankTexture=new Texture2D(1,depth,TextureFormat.RGBA32,false){name="RiverBankColor",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                    for(int row=0;row<depth;row++)
                    {
                        int fromTop=depth-1-row;
                        int top=map.riverBankEqualBands?Mathf.CeilToInt(depth/(float)bands):Mathf.Clamp(map.riverTopBandPixels,0,depth);
                        int middle=map.riverBankEqualBands?Mathf.CeilToInt(depth*2f/bands):top+Mathf.Max(0,map.riverMiddleBandPixels);
                        bankTexture.SetPixel(0,row,bands==1||fromTop<top?map.riverBankColor:bands==2||fromTop<middle?map.riverBankMiddle:map.riverBankBottom);
                    }
                    bankTexture.Apply();result.owned.Add(bankTexture);
                    bank=MaterialFor(bankTexture,result,materials);bank.SetFloat("_TerrainLighting",1);
                }
                int step=Mathf.Clamp(map.meshStep,1,16);
                var pathHeightWeights=map.BuildPathHeightWeights();
                float pathHeightScale=map.pathHeightOffsetPixels/(float)map.pixelsPerUnit;
                for(int y0=0;y0<map.height;y0+=ChunkPixels)
                {
                    int yEnd=Mathf.Min(map.height,y0+ChunkPixels),cols=Mathf.CeilToInt((float)map.width/step),rows=Mathf.CeilToInt((float)(yEnd-y0)/step),stride=cols+1;
                    var vertices=new Vector3[stride*(rows+1)];var uv=new Vector2[vertices.Length];var triangles=new int[cols*rows*6];
                    for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
                    {
                        int px=Mathf.Min(x*step,map.width),py=Mathf.Min(y0+y*step,yEnd),i=y*stride+x;
                        float vx=(px-map.width*.5f)/map.pixelsPerUnit,vz=(float)py/map.pixelsPerUnit;
                        float pathWeight=pathHeightWeights!=null?pathHeightWeights[map.Index(px,py)]:0;
                        vertices[i]=new Vector3(vx,map.RawVertexHeight(px,py)+pathHeightScale*pathWeight-curvature*((vz-focus)*(vz-focus)+.65f*vx*vx),vz);
                        uv[i]=new Vector2((float)px/map.width,(float)py/map.height);
                        if(x<cols&&y<rows){int k=(y*cols+x)*6;triangles[k]=i;triangles[k+1]=i+stride;triangles[k+2]=i+1;triangles[k+3]=i+1;triangles[k+4]=i+stride;triangles[k+5]=i+stride+1;}
                    }
                    var mesh=new Mesh{name="Ground_"+y0,indexFormat=vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
                    MeshObject(mesh.name,mesh,terrain,result.root.transform,result);
                    bool wet=false;for(int y=y0;y<yEnd&&!wet;y++)for(int x=0;x<map.width;x++)if(map.Surface(y*map.width+x)==2){wet=true;break;}
                    if(wet)
                    {
                        var lower=(Vector3[])vertices.Clone();for(int i=0;i<lower.Length;i++)lower[i].y-=map.RiverDepth;
                        var riverMesh=new Mesh{name="Water_"+y0,indexFormat=mesh.indexFormat};riverMesh.vertices=lower;riverMesh.uv=uv;riverMesh.triangles=triangles;riverMesh.RecalculateBounds();
                        MeshObject(riverMesh.name,riverMesh,water,result.root.transform,result,false,false);
                        if(map.RiverDepth>0)BuildBanks(map,y0,yEnd,curvature,focus,bank,result);
                    }
                    // One combined mesh per atlas and strip. Grass never creates a GameObject per blade.
                    var groups=new Dictionary<(Texture,bool),List<PixelMapDocument.Decoration>>();
                    foreach(var prop in map.decorations)
                    {
                        if(prop.shape==PixelMapDocument.PropShape.Caja||prop.sprite==null||!map.DecorationVisible(prop)||prop.pixel.y<y0||prop.pixel.y>=yEnd)continue;
                        var key=((Texture)prop.sprite.texture,prop.grass);
                        if(!groups.TryGetValue(key,out var group)){group=new List<PixelMapDocument.Decoration>();groups.Add(key,group);}group.Add(prop);
                    }
                    foreach(var pair in groups)
                    {
                        var v=new List<Vector3>();var u=new List<Vector2>();var motion=new List<Vector2>();var t=new List<int>();
                        var gradientRows=new List<Vector2>();foreach(var prop in pair.Value){int start=v.Count;AddSprite(map,prop,v,u,motion,t,curvature,focus);byte row=prop.grass?gradients.IndexAt(map.Index(prop.pixel.x,prop.pixel.y)):(byte)0;for(int i=start;i<v.Count;i++)gradientRows.Add(new Vector2(row,0));}
                        var decorationMesh=new Mesh{name="Figures_"+y0+"_"+pair.Key.Item1.name+(pair.Key.Item2?"_Grass":""),indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                        decorationMesh.SetVertices(v);decorationMesh.SetUVs(0,u);decorationMesh.SetUVs(1,motion);decorationMesh.SetUVs(2,gradientRows);decorationMesh.SetTriangles(t,0);decorationMesh.RecalculateBounds();
                        if(pair.Key.Item2){var bounds=decorationMesh.bounds;bounds.Expand(new Vector3(2*(map.grassSwayPixels+1)/map.pixelsPerUnit,0,0));decorationMesh.bounds=bounds;}
                        var propMaterial=MaterialFor(pair.Key.Item1,result,pair.Key.Item2?grassMaterials:materials);
                        if(pair.Key.Item2)
                        {
                            ConfigureGradient(propMaterial,map,gradientTexture,gradients.enabled);propMaterial.SetFloat("_PropGradient",1);propMaterial.name=pair.Key.Item1.name+"_Grass";propMaterial.SetFloat("_ReceiveShadows",0);propMaterial.SetFloat("_GrassSway",map.grassSwayPixels);propMaterial.SetFloat("_WindFPS",map.grassWindFPS);propMaterial.SetFloat("_PixelsPerUnit",map.pixelsPerUnit);
                            if(map.grassSwayPixels>0&&!result.animatedMaterials.Contains(propMaterial)){result.animatedGrass=true;result.animatedMaterials.Add(propMaterial);}
                        }
                        MeshObject(decorationMesh.name,decorationMesh,propMaterial,result.root.transform,result,pair.Key.Item2);
                    }
                }
                BuildBoxProps(map,curvature,focus,result);BuildPlatforms(map,curvature,focus,result);BuildWaterZones(map,curvature,focus,water,result);BuildWaterfalls(map,curvature,focus,result);BuildAmbientZones(map,curvature,focus,result);
                if(map.cloudsEnabled)BuildClouds(map,curvature,focus,result);
                return result;
            }
            catch{result.Dispose();throw;}
        }
        private static void BuildBanks(PixelMapDocument map,int y0,int yEnd,float curvature,float focus,Material material,Result result)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            PixelMapDocument.WaterZone currentZone=null;
            Vector3 Position(float px,float py,float level)
            {
                float x=(px-map.width*.5f)/map.pixelsPerUnit,z=py/map.pixelsPerUnit;
                return new Vector3(x,level-curvature*((z-focus)*(z-focus)+.65f*x*x),z);
            }
            float WaterLevel(PixelMapDocument.WaterZone zone,float px,float py)=>zone!=null?zone.levelPixels/(float)map.pixelsPerUnit:map.RawSurfaceHeight(px,py)-map.RiverDepth;
            void Edge(int nx,int ny,float ax,float ay,float bx,float by)
            {
                float topA=map.RawSurfaceHeight(ax,ay),topB=map.RawSurfaceHeight(bx,by);
                float bottomA=WaterLevel(currentZone,ax,ay),bottomB=WaterLevel(currentZone,bx,by);
                if(nx>=0&&ny>=0&&nx<map.width&&ny<map.height&&map.Surface(ny*map.width+nx)==2)
                {
                    var neighborZone=map.WaterZoneAt(nx,ny);
                    // A slope within one water body is continuous, never a bank.
                    // Only explicit zone boundaries may contain a level discontinuity.
                    if(currentZone==neighborZone)return;
                    topA=bottomA;topB=bottomB;
                    bottomA=WaterLevel(neighborZone,ax,ay);bottomB=WaterLevel(neighborZone,bx,by);
                    if((topA+topB)-(bottomA+bottomB)<=.001f)return;
                }
                bottomA=Mathf.Min(topA,bottomA);bottomB=Mathf.Min(topB,bottomB);
                if(topA-bottomA<.0001f&&topB-bottomB<.0001f)return;
                int i=vertices.Count;vertices.Add(Position(ax,ay,topA));vertices.Add(Position(bx,by,topB));vertices.Add(Position(ax,ay,bottomA));vertices.Add(Position(bx,by,bottomB));
                uv.Add(Vector2.up);uv.Add(Vector2.one);uv.Add(Vector2.zero);uv.Add(Vector2.right);
                triangles.Add(i);triangles.Add(i+2);triangles.Add(i+1);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i+3);
            }
            for(int y=y0;y<yEnd;y++)for(int x=0;x<map.width;x++)
            {
                if(map.Surface(y*map.width+x)!=2)continue;currentZone=map.WaterZoneAt(x,y);
                Edge(x-1,y,x,y,x,y+1);Edge(x+1,y,x+1,y+1,x+1,y);
                Edge(x,y-1,x+1,y,x,y);Edge(x,y+1,x,y+1,x+1,y+1);
            }
            if(vertices.Count==0)return;
            var mesh=new Mesh{name="RiverBanks_"+y0,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            MeshObject(mesh.name,mesh,material,result.root.transform,result);
        }
        private static void AddSprite(PixelMapDocument map,PixelMapDocument.Decoration prop,List<Vector3> vertices,List<Vector2> uv,List<Vector2> motion,List<int> triangles,float curvature,float focus)
        {
            var sprite=prop.sprite;var points=sprite.vertices;var spriteUV=sprite.uv;var indices=sprite.triangles;
            float minX=float.MaxValue,maxX=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
            foreach(var point in points){minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);minY=Mathf.Min(minY,point.y);maxY=Mathf.Max(maxY,point.y);}
            float x=prop.pixel.x+.5f,z=prop.pixel.y+.5f;
            float wx=(x-map.width*.5f)/map.pixelsPerUnit,wz=z/map.pixelsPerUnit;
            var foot=new Vector3(wx,map.SurfaceHeight(x,z)+prop.heightOffsetPixels/(float)map.pixelsPerUnit+(prop.grass?.125f/map.pixelsPerUnit:.001f)-curvature*((wz-focus)*(wz-focus)+.65f*wx*wx),wz);
            var rotation=Quaternion.Euler(Mathf.Atan(2*curvature*(wz-focus))*Mathf.Rad2Deg,0,-Mathf.Atan(1.3f*curvature*wx)*Mathf.Rad2Deg);
            float propPitch=map.PropPitch(prop);
            rotation*=Quaternion.Euler(0,prop.yaw,0)*Quaternion.Euler(propPitch,0,0);
            // One source texel always occupies one document pixel, regardless of sprite import PPU.
            float scale=sprite.pixelsPerUnit/map.pixelsPerUnit;
            int copies=prop.grass||prop.shape==PixelMapDocument.PropShape.Cruzado?2:1;
            for(int copy=0;copy<copies;copy++)
            {
                int start=vertices.Count;
                foreach(var point in points)
                {
                    float dx=(point.x-(minX+maxX)*.5f)*scale,dy=(point.y-minY)*scale;
                    vertices.Add(foot+rotation*(copy==0?new Vector3(dx,dy,0):new Vector3(0,dy,dx)));
                    motion.Add(prop.grass?new Vector2((point.y-minY)/Mathf.Max(.001f,maxY-minY),Mathf.Repeat(prop.pixel.x*.73f+prop.pixel.y*.47f,6.283185f)):Vector2.zero);
                }
                uv.AddRange(spriteUV);foreach(var index in indices)triangles.Add(start+index);
            }
        }
        public static string Bake(PixelMapDocument map,string parent)
        {
            if(!AssetDatabase.IsValidFolder(parent))throw new ArgumentException("La carpeta de exportación debe estar dentro de Assets.");
            string folder=AssetDatabase.GenerateUniqueAssetPath(parent+"/"+map.name+"_Baked");
            AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
            return BakeTo(map,folder+"/"+map.name+".prefab");
        }
        // Update asset contents while retaining GUIDs and references held by the game.
        public static string BakeTo(PixelMapDocument map,string prefabPath)
        {
            string folder=Path.GetDirectoryName(prefabPath).Replace('\\','/');
            if(!AssetDatabase.IsValidFolder(folder)||!prefabPath.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Elige un prefab dentro de Assets.");
            using(var result=Build(map))
            {
                var replacements=new Dictionary<Object,Object>();
                Texture2D WriteTexture(Texture2D source,string file,bool linear)
                {
                    string path=folder+"/"+file;
                    File.WriteAllBytes(path,source.EncodeToPNG());AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=false;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
                    importer.maxTextureSize=2048;importer.sRGBTexture=!linear;importer.SaveAndReimport();
                    var stored=AssetDatabase.LoadAssetAtPath<Texture2D>(path);replacements[source]=stored;return stored;
                }
                Object Store(Object source,string path)
                {
                    if(replacements.TryGetValue(source,out var known))return known;
                    var existing=AssetDatabase.LoadMainAssetAtPath(path);
                    if(existing!=null&&existing.GetType()!=source.GetType())throw new InvalidOperationException("Tipo incompatible en "+path);
                    if(existing!=null){EditorUtility.CopySerialized(source,existing);EditorUtility.SetDirty(existing);}
                    else{existing=Object.Instantiate(source);existing.name=source.name;existing.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(existing,path);}
                    replacements[source]=existing;return existing;
                }
                string Safe(string name){foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');return name;}
                WriteTexture(result.texture,"Ground.png",false);WriteTexture(result.surfaceMask,"Surfaces.png",true);
                var previous=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var oldMeshes=new Dictionary<string,string>();var oldMaterials=new Dictionary<string,string>();var oldTextures=new Dictionary<string,string>();
                if(previous!=null)
                {
                    foreach(var f in previous.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh!=null)oldMeshes.TryAdd(f.name,AssetDatabase.GetAssetPath(f.sharedMesh));
                    foreach(var r in previous.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(m!=null)
                    {
                        oldMaterials.TryAdd(m.name,AssetDatabase.GetAssetPath(m));
                        foreach(string property in m.GetTexturePropertyNames()){var texture=m.GetTexture(property);if(texture==null)continue;string path=AssetDatabase.GetAssetPath(texture);if(path.StartsWith(folder+"/",StringComparison.Ordinal)&&path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase))oldTextures.TryAdd(texture.name,path);}
                    }
                }
                foreach(var filter in result.root.GetComponentsInChildren<MeshFilter>(true))
                {
                    string path=oldMeshes.TryGetValue(filter.name,out var old)&&old.StartsWith(folder+"/",StringComparison.Ordinal)?old:folder+"/Geometry_"+Safe(filter.name)+".asset";
                    filter.sharedMesh=(Mesh)Store(filter.sharedMesh,path);
                }
                int serial=0;
                foreach(var renderer in result.root.GetComponentsInChildren<Renderer>(true))
                {
                    var assigned=renderer.sharedMaterials;
                    for(int slot=0;slot<assigned.Length;slot++)
                    {
                        var material=assigned[slot];if(material==null)continue;
                        if(replacements.TryGetValue(material,out var saved)){assigned[slot]=(Material)saved;continue;}
                        foreach(string property in material.GetTexturePropertyNames())
                        {
                            var texture=material.GetTexture(property);if(texture==null)continue;
                            if(replacements.TryGetValue(texture,out var replacement))material.SetTexture(property,(Texture)replacement);
                            else if(result.owned.Contains(texture)&&!AssetDatabase.Contains(texture))material.SetTexture(property,(Texture)Store(texture,oldTextures.TryGetValue(texture.name,out var texturePath)?texturePath:folder+"/Texture_"+Safe(texture.name)+".asset"));
                        }
                        string path=oldMaterials.TryGetValue(material.name,out var old)&&old.StartsWith(folder+"/",StringComparison.Ordinal)?old:folder+"/Surface_"+Safe(material.name)+"_"+(serial++)+".mat";
                        // Material names are unique within a build (atlas + purpose).
                        assigned[slot]=(Material)Store(material,path);
                    }
                    renderer.sharedMaterials=assigned;
                }
                result.root.hideFlags=HideFlags.None;
                var prefab=PrefabUtility.SaveAsPrefabAsset(result.root,prefabPath);
                if(prefab==null)throw new InvalidOperationException("No se pudo guardar el prefab.");
                map.exportedPrefab=prefab;EditorUtility.SetDirty(map);AssetDatabase.SaveAssets();return prefabPath;
            }
        }
    }
}
