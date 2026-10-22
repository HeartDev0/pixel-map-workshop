// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        [SerializeField] private bool paintSelectionOnly=true,paintPixelsOn3D;
        private int editStrokeUndoGroup;
        private readonly Dictionary<Texture2D,HashSet<int>> pixelStrokeTextures=new Dictionary<Texture2D,HashSet<int>>();
        private readonly HashSet<Texture2D> editedAtlases=new HashSet<Texture2D>();
        private Texture2D previousPaintTexture;
        private string previousPaintFace;
        private Vector2Int previousPaintPixel;
        private Vector2[] sampledTileUV;
        private string sampledTileSource;
        private Vector2Int sampledTile;
        private void BuildPaintTools(VisualElement parent)
        {
            var fold=new Foldout{text="Pintura de caras y píxeles",value=true};parent.Add(fold);
            var limited=new Toggle("Respetar selección al pintar"){value=paintSelectionOnly};limited.RegisterValueChangedCallback(e=>paintSelectionOnly=e.newValue);fold.Add(limited);
            var pixels=new Toggle("Pincel de píxeles sobre el prop 3D"){value=paintPixelsOn3D};pixels.RegisterValueChangedCallback(e=>paintPixelsOn3D=e.newValue);fold.Add(pixels);
            fold.Add(new Label("El modo píxel usa color y tamaño del pincel del atlas. Crea primero una copia editable. Cada trazo tiene un solo Undo."));
            fold.Add(ActionButton("Balde de tiles · Shift+F",()=>RunCommand("tool.fill")));fold.Add(ActionButton("Tomar tile / UV · I",()=>RunCommand("tool.sample")));
            fold.Add(ActionButton("Restaurar UV del pincel al tile completo",()=>sampledTileUV=null));
        }
        private HashSet<string> PaintLimit()=>paintSelectionOnly&&SelectionCount>0?new HashSet<string>(SelectedFacesForUv().Select(f=>f.id)):null;
        private bool EditablePaintTarget(TilePropDocument.Face face)=>face!=null&&!face.hidden&&!face.locked&&(!paintSelectionOnly||SelectionCount==0||IsFaceInSelection(face));
        private void SampleTile(TilePropDocument.Face face)
        {
            if(face==null)return;var source=document.GetTileset(face.tilesetId);if(source==null)return;
            document.SetActiveTileset(source.id);tileSelection=new RectInt(face.tile,Vector2Int.one);uvRotation=face.rotation;flipX=face.flipX;flipY=face.flipY;sampledTileUV=TilePropUV.Corners(face);sampledTileSource=source.id;sampledTile=face.tile;uvEditorMode=false;BuildUI();status.text="Tile y coordenadas UV recuperados de la cara.";
        }
        private void ApplyFaceTool(Rect rect,Vector2 mouse)
        {
            var face=PickFace(rect,mouse);if(tool==Tool.TomarTile){SampleTile(face);return;}if(!EditablePaintTarget(face))return;
            if(tool==Tool.Borrar){document.faces.Remove(face);selected.Remove(face.id);Changed();return;}
            if(tool==Tool.Rellenar){foreach(var target in TilePropPainting.Flood(document,face.id,PaintLimit()))SetTile(target);Changed();return;}
            if(!paintPixelsOn3D){SetTile(face);Changed();return;}
            var source=document.GetTileset(face.tilesetId);var texture=source?.texture;
            if(!IsEditableAtlas(texture)){status.text="Toma el tileset de esta cara y crea una copia editable antes de pintar píxeles.";return;}
            var ray=MouseRay(rect,mouse);var pixelRay=new Ray(ray.origin*document.pixelsPerUnit+(Vector3)document.pivotPixels,ray.direction);
            if(!TilePropPainting.TryRayPixel(document,face,pixelRay,out var pixel))return;
            var point=Vector2Int.FloorToInt(pixel);
            if(!pixelStrokeTextures.TryGetValue(texture,out var touched)){touched=new HashSet<int>();pixelStrokeTextures.Add(texture,touched);Undo.RegisterCompleteObjectUndo(texture,"Pintar píxeles sobre prop 3D");}
            var from=previousPaintTexture==texture&&previousPaintFace==face.id?previousPaintPixel:point;
            int painted=TilePropPainting.PaintPixels(texture,from,point,atlasBrushSize,atlasBrushColor,touched,p=>TilePropPainting.ContainsPixel(face,source.tilePixels,p));previousPaintTexture=texture;previousPaintFace=face.id;previousPaintPixel=point;
            if(painted>0){diagnosticsDirty=true;editedAtlases.Add(texture);lastPaintedAtlas=texture;EditorUtility.SetDirty(texture);palette?.MarkDirtyRepaint();viewport?.MarkDirtyRepaint();hasUnsavedChanges=true;}
        }
    }
}
