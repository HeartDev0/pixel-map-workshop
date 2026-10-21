// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        private bool paletteDragging;
        private Dictionary<TilePropDocument.FaceKey,TilePropDocument.Face> faceLookup;
        private Vector2Int paletteStart;
        private int paletteControl;
        private bool atlasPixelPainting,atlasPixelStrokeChanged;
        private Texture2D atlasStrokeTexture;
        private Texture2D lastPaintedAtlas;
        private Vector2 atlasStrokeMouse;
        private int atlasStrokeUndoGroup;
        private readonly HashSet<int> atlasPixelsChanged=new HashSet<int>();

        private bool IsEditableAtlas(Texture2D texture)=>texture!=null&&texture.isReadable&&texture.name.EndsWith("_PixelEditable",StringComparison.Ordinal);
        private static string SafeFileName(string value){if(string.IsNullOrWhiteSpace(value))return "Tileset";foreach(char invalid in Path.GetInvalidFileNameChars())value=value.Replace(invalid,'_');return value.Replace(' ','_');}
        private void CreateEditableAtlasCopy()
        {
            if(document==null||document.ActiveTileset==null||document.ActiveTileset.texture==null){if(status!=null)status.text="Elige primero un atlas con imagen.";return;}
            var source=document.ActiveTileset;var original=source.texture;Texture2D copy=null;RenderTexture temporary=null;var previous=RenderTexture.active;
            try
            {
                copy=new Texture2D(original.width,original.height,TextureFormat.RGBA32,false,false){name=SafeFileName(document.name+"_"+source.displayName)+"_PixelEditable",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                if(original.isReadable)copy.SetPixels32(original.GetPixels32());
                else
                {
                    temporary=RenderTexture.GetTemporary(original.width,original.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default);Graphics.Blit(original,temporary);RenderTexture.active=temporary;copy.ReadPixels(new Rect(0,0,original.width,original.height),0,0,false);
                }
                copy.Apply(false,false);string folder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(document))?.Replace('\\','/');if(string.IsNullOrEmpty(folder)||!folder.StartsWith("Assets/",StringComparison.Ordinal))folder="Assets";
                string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+copy.name+".asset");AssetDatabase.CreateAsset(copy,path);AssetDatabase.SaveAssetIfDirty(copy);
                Edit(()=>{source.texture=copy;if(document.tilesets.FirstOrDefault(candidate=>candidate!=null)==source)document.tileset=copy;},"Crear copia editable del atlas",true);
                if(status!=null)status.text="Copia editable lista. Pinta sobre ella; la imagen original queda intacta.";
            }
            catch(Exception e){if(copy!=null&&string.IsNullOrEmpty(AssetDatabase.GetAssetPath(copy)))DestroyImmediate(copy);if(status!=null)status.text="No se pudo copiar el atlas: "+e.Message;}
            finally{RenderTexture.active=previous;if(temporary!=null)RenderTexture.ReleaseTemporary(temporary);}
        }
        private void SaveEditableAtlases()
        {
            if(IsEditableAtlas(lastPaintedAtlas)&&AssetDatabase.Contains(lastPaintedAtlas))AssetDatabase.SaveAssetIfDirty(lastPaintedAtlas);
        }
        private void ResizePalette(){if(palette==null)return;var source=document!=null?document.ActiveTileset:null;var texture=source!=null?source.texture:null;if(document!=null&&document.HasTiles){int x=Mathf.Clamp(tileSelection.x,0,document.Columns-1),y=Mathf.Clamp(tileSelection.y,0,document.Rows-1);tileSelection=new RectInt(x,y,Mathf.Clamp(tileSelection.width,1,Mathf.Min(16,document.Columns-x)),Mathf.Clamp(tileSelection.height,1,Mathf.Min(16,document.Rows-y)));}palette.style.width=texture!=null?texture.width*zoom:200;palette.style.height=texture!=null?texture.height*zoom:180;palette.MarkDirtyRepaint();RefreshLabels();}
        private void DrawPalette()
        {
            if(document==null||!document.HasTiles){GUI.Label(new Rect(8,8,210,55),"Asigna una imagen con al menos un tile completo.");return;}
            var source=document.ActiveTileset;var texture=source.texture;var rect=new Rect(0,0,texture.width*zoom,texture.height*zoom);GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,true);
            int step=Mathf.Max(1,source.tilePixels)*zoom;Handles.BeginGUI();Handles.color=new Color(.08f,.08f,.1f,.5f);
            for(int x=0;x<=document.Columns;x++)Handles.DrawLine(new Vector3(x*step,0),new Vector3(x*step,rect.height));
            for(int y=0;y<=document.Rows;y++)Handles.DrawLine(new Vector3(0,y*step),new Vector3(rect.width,y*step));
            var selection=new Rect(tileSelection.x*step,tileSelection.y*step,tileSelection.width*step,tileSelection.height*step);Handles.DrawSolidRectangleWithOutline(selection,new Color(1,.75f,.35f,.15f),new Color(1,.8f,.4f));Handles.EndGUI();
            var e=Event.current;int control=GUIUtility.GetControlID(0x4731,FocusType.Passive,rect);
            Vector2Int TileAt(Vector2 p)=>new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(p.x/step),0,document.Columns-1),Mathf.Clamp(Mathf.FloorToInt(p.y/step),0,document.Rows-1));
            if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition))
            {
                if(paintAtlasMode)
                {
                    if(!IsEditableAtlas(texture)){if(status!=null)status.text="Crea una copia editable antes de pintar; así no se altera el archivo original.";e.Use();return;}
                    BeginAtlasPixelStroke(texture,control,e.mousePosition);e.Use();return;
                }
                paletteDragging=true;paletteControl=control;paletteStart=TileAt(e.mousePosition);GUIUtility.hotControl=control;tileSelection=new RectInt(paletteStart,Vector2Int.one);e.Use();
            }
            if(atlasPixelPainting&&(e.type==EventType.MouseDrag||e.type==EventType.MouseUp))
            {
                PaintAtlasSegment(atlasStrokeTexture,atlasStrokeMouse,e.mousePosition);atlasStrokeMouse=e.mousePosition;
                if(e.type==EventType.MouseUp){EndAtlasPixelStroke();e.Use();return;}
                e.Use();return;
            }
            if(paletteDragging&&(e.type==EventType.MouseDrag||e.type==EventType.MouseUp))
            {
                var end=TileAt(e.mousePosition);var min=Vector2Int.Min(paletteStart,end);var extent=Vector2Int.Max(paletteStart,end)-min+Vector2Int.one;tileSelection=new RectInt(min,new Vector2Int(Mathf.Min(16,extent.x),Mathf.Min(16,extent.y)));
                if(e.type==EventType.MouseUp){paletteDragging=false;if(GUIUtility.hotControl==control)GUIUtility.hotControl=0;}e.Use();
            }
            if(e.type==EventType.Used){palette.MarkDirtyRepaint();RefreshLabels();}
        }
        private Vector2Int AtlasPixelAt(Texture2D texture,Vector2 point)=>new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(point.x/zoom),0,texture.width-1),Mathf.Clamp(Mathf.FloorToInt(point.y/zoom),0,texture.height-1));
        private void BeginAtlasPixelStroke(Texture2D texture,int control,Vector2 point)
        {
            atlasPixelPainting=true;atlasPixelStrokeChanged=false;atlasStrokeTexture=texture;lastPaintedAtlas=texture;atlasStrokeMouse=point;paletteControl=control;atlasPixelsChanged.Clear();Undo.IncrementCurrentGroup();atlasStrokeUndoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Pintar atlas con píxeles");Undo.RegisterCompleteObjectUndo(texture,"Pintar atlas con píxeles");GUIUtility.hotControl=control;PaintAtlasSegment(texture,point,point);
        }
        private void PaintAtlasSegment(Texture2D texture,Vector2 from,Vector2 to)
        {
            if(texture==null||!atlasPixelPainting)return;var first=AtlasPixelAt(texture,from);var last=AtlasPixelAt(texture,to);int steps=Mathf.Max(Mathf.Abs(last.x-first.x),Mathf.Abs(last.y-first.y));
            for(int i=0;i<=steps;i++)
            {
                var point=Vector2Int.RoundToInt(Vector2.Lerp(first,last,steps==0?0:i/(float)steps));int size=Mathf.Clamp(atlasBrushSize,1,16),start=-(size-1)/2;
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    int px=point.x+start+x,py=point.y+start+y;if(px<0||py<0||px>=texture.width||py>=texture.height)continue;int key=py*texture.width+px;if(!atlasPixelsChanged.Add(key))continue;texture.SetPixel(px,texture.height-1-py,atlasBrushColor);atlasPixelStrokeChanged=true;
                }
            }
            if(!atlasPixelStrokeChanged)return;texture.Apply(false,false);EditorUtility.SetDirty(texture);viewport?.MarkDirtyRepaint();palette?.MarkDirtyRepaint();Repaint();
        }
        private void EndAtlasPixelStroke()
        {
            if(!atlasPixelPainting)return;atlasPixelPainting=false;if(GUIUtility.hotControl==paletteControl)GUIUtility.hotControl=0;
            if(atlasPixelStrokeChanged){EditorUtility.SetDirty(atlasStrokeTexture);AssetDatabase.SaveAssetIfDirty(atlasStrokeTexture);Undo.CollapseUndoOperations(atlasStrokeUndoGroup);if(status!=null)status.text="Pincel aplicado a "+atlasPixelsChanged.Count+" píxeles; atlas editable guardado.";}
            atlasStrokeTexture=null;atlasPixelsChanged.Clear();palette?.MarkDirtyRepaint();viewport?.MarkDirtyRepaint();Repaint();
        }
        private Vector2Int ActiveTile(int u=0,int v=0)
        {
            return new Vector2Int(Mathf.Clamp(tileSelection.x+u%Mathf.Max(1,tileSelection.width),0,Mathf.Max(0,document.Columns-1)),Mathf.Clamp(tileSelection.y+v%Mathf.Max(1,tileSelection.height),0,Mathf.Max(0,document.Rows-1)));
        }
        private void SetTile(TilePropDocument.Face face,int u=0,int v=0){var source=document.ActiveTileset;face.tilesetId=source!=null?source.id:null;face.tile=ActiveTile(u,v);face.rotation=uvRotation;face.flipX=flipX;face.flipY=flipY;face.uvRect=new Vector4(0,0,1,1);}
        private void BeginEditStroke()
        {
            Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Construir prop por tiles");Undo.RegisterCompleteObjectUndo(document,"Construir prop por tiles");strokeCells.Clear();faceLookup=document.FaceLookup();
        }
        private void Stamp(Vector2Int cell)
        {
            if(document==null||!document.HasTiles||!strokeCells.Add(cell)||document.faces.Count>=TilePropDocument.FaceLimit)return;
            for(int v=0;v<Mathf.Max(1,tileSelection.height);v++)for(int u=0;u<Mathf.Max(1,tileSelection.width);u++)
            {
                var face=TilePropDocument.Quad(workPlane,cell+new Vector2Int(u,v),slice,document.tilePixels,ActiveTile(u,v));var key=new TilePropDocument.FaceKey(face);if(faceLookup==null)faceLookup=document.FaceLookup();faceLookup.TryGetValue(key,out var found);
                if(found!=null){SetTile(found,u,v);continue;}if(document.faces.Count>=TilePropDocument.FaceLimit){Changed();return;}SetTile(face,u,v);document.faces.Add(face);faceLookup[key]=face;
            }
            Changed();
        }
        private void PaintSelection(){if(document==null||SelectionCount==0||!document.HasTiles)return;Edit(()=>{foreach(var f in document.faces)if(IsFaceInSelection(f))SetTile(f);},"Pintar caras seleccionadas");}
        private List<TilePropDocument.Face> SelectedFacesForUv()=>document==null?new List<TilePropDocument.Face>():document.faces.Where(face=>face!=null&&!face.hidden&&!face.locked&&IsFaceInSelection(face)).ToList();
        private void SyncAtlasFromSelection()
        {
            var face=SelectedFacesForUv().FirstOrDefault();if(face==null){if(status!=null)status.text="Selecciona una cara visible para localizar su tile.";return;}
            var source=document.GetTileset(face.tilesetId);if(source==null||source.texture==null){if(status!=null)status.text="La cara seleccionada apunta a un tileset sin imagen.";return;}
            Edit(()=>{document.SetActiveTileset(source.id);tileSelection=new RectInt(face.tile,Vector2Int.one);uvRotation=face.rotation;flipX=face.flipX;flipY=face.flipY;},"Localizar tile de la selección",true);
        }
        private void ApplyAtlasRegionToSelection()
        {
            var faces=SelectedFacesForUv();if(faces.Count==0||document==null||!document.HasTiles)return;
            var source=document.ActiveTileset;var tile=new Vector2Int(tileSelection.x,tileSelection.y);var size=new Vector2Int(Mathf.Max(1,tileSelection.width),Mathf.Max(1,tileSelection.height));
            Edit(()=>{foreach(var face in faces){SetTile(face);face.tile=tile;face.uvRect=new Vector4(0,0,size.x,size.y);}},"Asignar región del atlas");
        }
        private void RotateSelectedUv()
        {
            var faces=SelectedFacesForUv();if(faces.Count==0)return;Edit(()=>{foreach(var face in faces)face.rotation=(face.rotation+1)%4;},"Girar UV seleccionada");
        }
        private void FlipSelectedUv(bool horizontal)
        {
            var faces=SelectedFacesForUv();if(faces.Count==0)return;Edit(()=>{foreach(var face in faces){if(horizontal)face.flipX=!face.flipX;else face.flipY=!face.flipY;}},horizontal?"Reflejar UV horizontal":"Reflejar UV vertical");
        }
        private void MarkReplacementSource()
        {
            var face=SelectedFacesForUv().FirstOrDefault();if(face==null){if(status!=null)status.text="Selecciona una cara para marcar el tile que vas a cambiar.";return;}
            replaceSourceTilesetId=string.IsNullOrEmpty(face.tilesetId)&&document.ActiveTileset!=null?document.ActiveTileset.id:face.tilesetId;replaceSourceTile=face.tile;
            if(status!=null){var source=document.GetTileset(replaceSourceTilesetId);status.text="Tile marcado: "+(source!=null?source.displayName:"tileset")+" ["+replaceSourceTile.x+", "+replaceSourceTile.y+"]. Elige el reemplazo en el atlas.";}
        }
        private void ReplaceMarkedTile()
        {
            if(document==null||string.IsNullOrEmpty(replaceSourceTilesetId)){if(status!=null)status.text="Primero marca el tile de una cara seleccionada.";return;}
            var source=document.ActiveTileset;if(source==null||source.texture==null||!document.HasTiles){if(status!=null)status.text="Elige el tileset de reemplazo.";return;}
            var replacement=ActiveTile();var affected=document.faces.Where(face=>face!=null&&face.tilesetId==replaceSourceTilesetId&&face.tile==replaceSourceTile&&!face.locked).ToList();
            if(affected.Count==0){if(status!=null)status.text="No hay caras sin bloquear con ese tile.";return;}
            Edit(()=>{foreach(var face in affected){face.tilesetId=source.id;face.tile=replacement;}}, "Reemplazar tile en el prop");
            if(status!=null)status.text="Tile reemplazado en "+affected.Count+" caras.";
        }
        private bool ComponentWouldCycle(GameObject prefab,out string message)
        {
            message=null;if(prefab==null){message="Elige un prefab de prop.";return true;}
            if(!PrefabUtility.IsPartOfPrefabAsset(prefab)||prefab.GetComponent<TilePropAsset>()==null){message="Usa un prefab exportado desde Tile Prop Studio.";return true;}
            string documentPath=AssetDatabase.GetAssetPath(document),documentGuid=AssetDatabase.AssetPathToGUID(documentPath),candidatePath=AssetDatabase.GetAssetPath(prefab);
            if(prefab.GetComponent<TilePropAsset>().sourceGuid==documentGuid){message="Un prop no puede incluirse a sí mismo como componente.";return true;}
            string ownPrefabPath=document.exportedPrefab!=null?AssetDatabase.GetAssetPath(document.exportedPrefab):null;
            foreach(var dependency in AssetDatabase.GetDependencies(candidatePath,true))
            {
                if(!string.IsNullOrEmpty(ownPrefabPath)&&dependency==ownPrefabPath){message="Esta referencia crearía un ciclo entre prefabs.";return true;}
                var nested=AssetDatabase.LoadAssetAtPath<GameObject>(dependency);var metadata=nested!=null?nested.GetComponent<TilePropAsset>():null;
                if(metadata!=null&&!string.IsNullOrEmpty(documentGuid)&&metadata.sourceGuid==documentGuid){message="Este prefab ya contiene una instancia de este prop.";return true;}
            }
            return false;
        }
        private void AddComponentInstance()
        {
            if(document==null)return;var prefab=componentPrefabField!=null?componentPrefabField.value as GameObject:null;
            if(ComponentWouldCycle(prefab,out var message)){if(status!=null)status.text=message;return;}
            TilePropDocument.ComponentInstance created=null;Edit(()=>{created=new TilePropDocument.ComponentInstance{prefab=prefab};document.components.Add(created);selectedComponentId=created.id;},"Añadir componente enlazado",true);FrameComponent(created);
        }
        private void RemoveComponentInstance(string id)
        {
            if(document==null)return;Edit(()=>{document.components.RemoveAll(component=>component!=null&&component.id==id);if(selectedComponentId==id)selectedComponentId=null;},"Quitar componente enlazado",true);
        }
        private void FrameComponent(TilePropDocument.ComponentInstance component)
        {
            if(document==null||component==null)return;cameraTarget=((Vector3)component.positionPixels-document.pivotPixels)/Mathf.Max(1,document.pixelsPerUnit);cameraDistance=Mathf.Max(.6f,document.tilePixels/Mathf.Max(1,document.pixelsPerUnit)*5f);cameraReady=true;viewport?.MarkDirtyRepaint();Repaint();
        }
        private void DeleteSelection(){if(document==null||SelectionCount==0)return;Edit(()=>{document.faces.RemoveAll(IsFaceInSelection);ClearComponentSelection();},"Eliminar geometría seleccionada");}
        private void MoveSelection(bool duplicate)
        {
            if(document==null||SelectionCount==0)return;if(movePixels==Vector3Int.zero){status.text="Indica un desplazamiento distinto de cero.";return;}
            if(!duplicate){TransformSelection(p=>p+movePixels,"Mover selección");return;}
            if(componentMode!=ComponentMode.Caras&&componentMode!=ComponentMode.Objeto){status.text="Duplica bordes o vértices seleccionando una cara o cambiando a Caras.";return;}
            var faces=document.faces.Where(f=>selected.Contains(f.id)).ToList();
            if(duplicate&&faces.Count+document.faces.Count>TilePropDocument.FaceLimit){status.text="La duplicación superaría el límite de caras.";return;}
            Edit(()=>
            {
                if(duplicate)selected.Clear();foreach(var face in faces)
                {
                    var target=duplicate?JsonUtility.FromJson<TilePropDocument.Face>(JsonUtility.ToJson(face)):face;
                    if(duplicate){target.id=Guid.NewGuid().ToString("N");document.faces.Add(target);selected.Add(target.id);}
                    target.a+=movePixels;target.b+=movePixels;target.c+=movePixels;target.d+=movePixels;
                }
            },duplicate?"Duplicar caras":"Mover caras");
        }
        private void RotateSelection()
        {
            if(document==null||SelectionCount==0)return;var rotation=Quaternion.AngleAxis(90,rotationAxis==0?Vector3.right:rotationAxis==1?Vector3.up:Vector3.forward);
            if(pivotMode==PivotMode.Individual&&(componentMode==ComponentMode.Caras||componentMode==ComponentMode.Objeto))
            {
                Edit(()=>{foreach(var face in document.faces)if(selected.Contains(face.id)){var points=face.Corners;var pivot=FaceCenter(points);Vector3Int Turn(Vector3Int p)=>pivot+Vector3Int.RoundToInt(rotation*(p-pivot));face.a=Turn(points[0]);face.b=Turn(points[1]);face.c=Turn(points[2]);face.d=Turn(points[3]);}},"Girar caras individualmente");return;
            }
            var pivot=pivotMode==PivotMode.PivoteObjeto?document.pivotPixels:SelectionCenter();Vector3Int Rotate(Vector3Int p)=>pivot+Vector3Int.RoundToInt(rotation*(p-pivot));TransformSelection(Rotate,"Girar selección 90 grados");
        }
        private void AddBox()
        {
            if(document==null||!document.HasTiles)return;var size=new Vector3Int(Mathf.Clamp(boxSize.x,1,32),Mathf.Clamp(boxSize.y,1,32),Mathf.Clamp(boxSize.z,1,32));
            int count=2*(size.x*size.y+size.x*size.z+size.y*size.z);if(document.faces.Count+count>TilePropDocument.FaceLimit){status.text="La caja superaría el límite de caras.";return;}
            componentMode=ComponentMode.Caras;if(componentModeField!=null)componentModeField.SetValueWithoutNotify(componentMode);
            Edit(()=>
            {
                ClearComponentSelection();faceLookup=document.FaceLookup();void Add(TileWorkPlane plane,int u,int v,int level,bool reverse)
                {
                    var face=TilePropDocument.Quad(plane,new Vector2Int(u,v),level,document.tilePixels,ActiveTile(u,v));if(reverse){var b=face.b;face.b=face.d;face.d=b;}SetTile(face,u,v);var key=new TilePropDocument.FaceKey(face);if(faceLookup==null)faceLookup=document.FaceLookup();faceLookup.TryGetValue(key,out var found);if(found!=null){selected.Add(found.id);return;}document.faces.Add(face);faceLookup[key]=face;selected.Add(face.id);
                }
                for(int z=0;z<size.z;z++)for(int x=0;x<size.x;x++){Add(TileWorkPlane.SueloXZ,x,z,0,true);Add(TileWorkPlane.SueloXZ,x,z,size.y,false);}
                for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++){Add(TileWorkPlane.FrenteXY,x,y,0,true);Add(TileWorkPlane.FrenteXY,x,y,size.z,false);}
                for(int y=0;y<size.y;y++)for(int z=0;z<size.z;z++){Add(TileWorkPlane.LateralZY,z,y,0,true);Add(TileWorkPlane.LateralZY,z,y,size.x,false);}
            },"Crear caja por tiles");FrameModel();
        }
    }
}
