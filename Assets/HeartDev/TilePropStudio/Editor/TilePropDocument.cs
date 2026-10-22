// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace HeartDev.TilePropStudio
{
    public enum TileWorkPlane { SueloXZ,FrenteXY,LateralZY }
    public enum TilePropCollider { Ninguno,Caja,Malla,Convexo }
    [CreateAssetMenu(menuName="HeartDev/Tile Prop Studio/Documento de prop",fileName="NuevoProp")]
    public sealed class TilePropDocument : ScriptableObject
    {
        public const int FaceLimit=8192;
        public const int CurrentFormatVersion=10;
        public bool IsSupportedFormat=>formatVersion>=0&&formatVersion<=CurrentFormatVersion;
        [Serializable] public sealed class TilesetSource
        {
            public string id=Guid.NewGuid().ToString("N");
            public string displayName;
            public Texture2D texture;
            public int tilePixels=16;
        }
        [Serializable] public sealed class Face
        {
            public string id=Guid.NewGuid().ToString("N");
            public Vector3Int a,b,c,d;
            public Vector2Int tile;
            public string tilesetId;
            public string groupId;
            public int rotation;
            public bool flipX,flipY;
            public bool hidden,locked;
            public bool flipDiagonal;
            public Vector4 uvRect=new Vector4(0,0,1,1);
            // Optional explicit coordinates in tile units; older faces keep the affine layout.
            public Vector2[] customUV=Array.Empty<Vector2>();
            public Vector3Int[] Corners=>new[]{a,b,c,d};
        }
        [Serializable] public sealed class FaceGroup
        {
            public string id=Guid.NewGuid().ToString("N");
            public string name="Grupo";
        }
        [Serializable] public sealed class ComponentInstance
        {
            public string id=Guid.NewGuid().ToString("N");
            public GameObject prefab;
            public Vector3Int positionPixels;
            public Vector3 rotationEuler;
            public Vector3 scale=Vector3.one;
            public string variantName;
            public List<MaterialOverride> appearance=new List<MaterialOverride>();
        }
        [Serializable] public sealed class MaterialOverride { public Material source,replacement; }
        [Serializable] public sealed class ConnectionPoint
        {
            public string id=Guid.NewGuid().ToString("N"),name="Conexión";
            public Vector3Int positionPixels;
            public Vector3 rotationEuler;
        }
        [SerializeField] public int formatVersion=CurrentFormatVersion;
        public Texture2D tileset;
        public int tilePixels=16,pixelsPerUnit=32;
        public bool generateMeshCollider;
        public TilePropCollider colliderMode;
        public TilePropCollider EffectiveCollider=>generateMeshCollider&&colliderMode==TilePropCollider.Ninguno?TilePropCollider.Malla:colliderMode;
        public List<TilesetSource> tilesets=new List<TilesetSource>();
        public string activeTilesetId;
        public List<FaceGroup> groups=new List<FaceGroup>();
        public List<ComponentInstance> components=new List<ComponentInstance>();
        public List<ConnectionPoint> connectionPoints=new List<ConnectionPoint>();
        public Vector3Int pivotPixels;
        public List<Face> faces=new List<Face>();
        public GameObject exportedPrefab;
        public Texture2D thumbnail;
        public TilesetSource ActiveTileset
        {
            get
            {
                if(tilesets==null||tilesets.Count==0)return null;
                return tilesets.FirstOrDefault(source=>source!=null&&source.id==activeTilesetId)??tilesets.FirstOrDefault(source=>source!=null);
            }
        }
        public int Columns=>ActiveTileset!=null&&ActiveTileset.texture!=null?ActiveTileset.texture.width/Mathf.Max(1,ActiveTileset.tilePixels):0;
        public int Rows=>ActiveTileset!=null&&ActiveTileset.texture!=null?ActiveTileset.texture.height/Mathf.Max(1,ActiveTileset.tilePixels):0;
        public bool HasTiles=>ActiveTileset!=null&&ActiveTileset.texture!=null&&Columns>0&&Rows>0;
        public bool EnsureTilesets()
        {
            if(!IsSupportedFormat)return false;
            bool changed=false;if(tilesets==null){tilesets=new List<TilesetSource>();changed=true;}
            if(tilesets.Count==0||!tilesets.Any(source=>source!=null))
            {
                tilesets.Clear();
                tilesets.Add(new TilesetSource{id="primary",displayName="Principal",texture=tileset,tilePixels=Mathf.Max(1,tilePixels)});activeTilesetId="primary";changed=true;
            }
            var ids=new HashSet<string>();
            foreach(var source in tilesets)
            {
                if(source==null)continue;
                if(string.IsNullOrEmpty(source.id)||!ids.Add(source.id)){source.id=Guid.NewGuid().ToString("N");ids.Add(source.id);changed=true;}
                if(source.tilePixels<=0||source.tilePixels!=tilePixels){source.tilePixels=Mathf.Max(1,tilePixels);changed=true;}
                if(string.IsNullOrWhiteSpace(source.displayName)){source.displayName=source.texture!=null?source.texture.name:"Tileset";changed=true;}
            }
            var first=tilesets.FirstOrDefault(source=>source!=null);
            if(first!=null)
            {
                if(string.IsNullOrEmpty(activeTilesetId)||GetTileset(activeTilesetId)==null){activeTilesetId=first.id;changed=true;}
                if(tileset!=first.texture){tileset=first.texture;changed=true;}
                if(faces!=null)foreach(var face in faces)if(face!=null&&string.IsNullOrEmpty(face.tilesetId)){face.tilesetId=first.id;changed=true;}
            }
            return changed;
        }
        public TilesetSource GetTileset(string id)
        {
            if(tilesets==null)return null;
            return tilesets.FirstOrDefault(source=>source!=null&&source.id==id)??(string.IsNullOrEmpty(id)?tilesets.FirstOrDefault(source=>source!=null):null);
        }
        public string AddTileset(Texture2D texture,string displayName=null)
        {
            EnsureTilesets();if(texture==null)throw new ArgumentNullException(nameof(texture));
            if(tilesets.Any(source=>source!=null&&source.texture==texture))throw new InvalidOperationException("Ese tileset ya está agregado al documento.");
            if(tilesets.Count==1&&tilesets[0]!=null&&tilesets[0].texture==null&&(faces==null||faces.Count==0))
            {
                var primary=tilesets[0];primary.texture=texture;primary.displayName=string.IsNullOrWhiteSpace(displayName)?texture.name:displayName;activeTilesetId=primary.id;tileset=texture;return primary.id;
            }
            var source=new TilesetSource{displayName=string.IsNullOrWhiteSpace(displayName)?texture.name:displayName,texture=texture,tilePixels=Mathf.Max(1,tilePixels)};
            tilesets.Add(source);activeTilesetId=source.id;if(tilesets.Count==1)tileset=texture;return source.id;
        }
        public void SetActiveTileset(string id){if(GetTileset(id)!=null)activeTilesetId=id;}
        public bool EnsureGroups()
        {
            if(!IsSupportedFormat)return false;
            if(groups==null){groups=new List<FaceGroup>();return true;}bool changed=false;var ids=new HashSet<string>();
            foreach(var group in groups)
            {
                if(group==null)continue;if(string.IsNullOrEmpty(group.id)||!ids.Add(group.id)){group.id=Guid.NewGuid().ToString("N");ids.Add(group.id);changed=true;}
                if(string.IsNullOrWhiteSpace(group.name)){group.name="Grupo "+(ids.Count);changed=true;}
            }
            return changed;
        }
        public FaceGroup GetGroup(string id)=>groups==null?null:groups.FirstOrDefault(group=>group!=null&&group.id==id);
        public FaceGroup AddGroup(string name)
        {
            EnsureGroups();var group=new FaceGroup{name=string.IsNullOrWhiteSpace(name)?"Grupo "+(groups.Count+1):name};groups.Add(group);return group;
        }
        public bool EnsureComponents()
        {
            if(!IsSupportedFormat)return false;
            if(components==null){components=new List<ComponentInstance>();return true;}bool changed=false;var ids=new HashSet<string>();
            foreach(var instance in components)
            {
                if(instance==null)continue;if(string.IsNullOrEmpty(instance.id)||!ids.Add(instance.id)){instance.id=Guid.NewGuid().ToString("N");ids.Add(instance.id);changed=true;}
                if(formatVersion<9&&instance.scale==Vector3.zero){instance.scale=Vector3.one;changed=true;}
            }
            return changed;
        }
        public bool UpgradeFormatIfNeeded()
        {
            if(formatVersion>=CurrentFormatVersion)return false;
            if(faces==null)faces=new List<Face>();
            if(formatVersion<3)foreach(var face in faces)if(face!=null){face.hidden=false;face.locked=false;if(face.uvRect.z<=face.uvRect.x||face.uvRect.w<=face.uvRect.y)face.uvRect=new Vector4(0,0,1,1);}
            if(formatVersion<10&&generateMeshCollider)colliderMode=TilePropCollider.Malla;
            if(connectionPoints==null)connectionPoints=new List<ConnectionPoint>();
            EnsureTilesets();EnsureGroups();EnsureComponents();formatVersion=CurrentFormatVersion;return true;
        }
        public Bounds PixelBounds
        {
            get
            {
                var bounds=new Bounds();bool any=false;void Include(Vector3 point){if(!any){bounds=new Bounds(point,Vector3.zero);any=true;}else bounds.Encapsulate(point);}
                if(faces!=null)foreach(var face in faces)if(face!=null&&!face.hidden)foreach(var point in face.Corners)Include(point);
                if(components!=null)foreach(var instance in components)
                {
                    if(instance?.prefab==null)continue;var metadata=instance.prefab.GetComponent<TilePropAsset>();float ppu=metadata!=null?Mathf.Max(1,metadata.pixelsPerUnit):pixelsPerUnit;
                    var transform=Matrix4x4.TRS(instance.positionPixels,Quaternion.Euler(instance.rotationEuler),instance.scale*ppu)*instance.prefab.transform.worldToLocalMatrix;
                    foreach(var filter in instance.prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if(filter.sharedMesh==null)continue;var matrix=transform*filter.transform.localToWorldMatrix;var b=filter.sharedMesh.bounds;
                        for(int i=0;i<8;i++)Include(matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                    }
                }
                return any?bounds:new Bounds(Vector3.zero,Vector3.one*tilePixels);
            }
        }
        public static Face Quad(TileWorkPlane plane,Vector2Int cell,int slice,int size,Vector2Int tile)
        {
            int u=cell.x*size,v=cell.y*size,w=slice*size;
            if(plane==TileWorkPlane.SueloXZ)return new Face{a=new Vector3Int(u,w,v+size),b=new Vector3Int(u+size,w,v+size),c=new Vector3Int(u+size,w,v),d=new Vector3Int(u,w,v),tile=tile};
            if(plane==TileWorkPlane.FrenteXY)return new Face{a=new Vector3Int(u,v,w),b=new Vector3Int(u+size,v,w),c=new Vector3Int(u+size,v+size,w),d=new Vector3Int(u,v+size,w),tile=tile};
            return new Face{a=new Vector3Int(w,v,u+size),b=new Vector3Int(w,v,u),c=new Vector3Int(w,v+size,u),d=new Vector3Int(w,v+size,u+size),tile=tile};
        }
        public readonly struct FaceKey : IEquatable<FaceKey>
        {
            private readonly Vector3Int center;private readonly int axis;
            public FaceKey(Face face){center=face.a+face.b+face.c+face.d;var n=Vector3.Cross(face.b-face.a,face.d-face.a);axis=Mathf.Abs(n.x)>Mathf.Abs(n.y)&&Mathf.Abs(n.x)>Mathf.Abs(n.z)?0:Mathf.Abs(n.y)>Mathf.Abs(n.z)?1:2;}
            public bool Equals(FaceKey other)=>center==other.center&&axis==other.axis;
            public override bool Equals(object obj)=>obj is FaceKey other&&Equals(other);
            public override int GetHashCode()=>unchecked(center.GetHashCode()*397^axis);
        }
        public Dictionary<FaceKey,Face> FaceLookup(){var result=new Dictionary<FaceKey,Face>();foreach(var face in faces)result[new FaceKey(face)]=face;return result;}

    }
    [CustomEditor(typeof(TilePropDocument))] public sealed class TilePropDocumentInspector : Editor
    {
        public override UnityEngine.UIElements.VisualElement CreateInspectorGUI()
        {
            var root=new UnityEngine.UIElements.VisualElement();var doc=(TilePropDocument)target;
            root.Add(new UnityEngine.UIElements.Label(doc.faces.Count+" caras · "+doc.tilePixels+" px por tile"));
            root.Add(new UnityEngine.UIElements.Button(()=>TilePropStudioWindow.OpenDocument(doc)){text="Abrir Tile Prop Studio"});return root;
        }
    }
}
