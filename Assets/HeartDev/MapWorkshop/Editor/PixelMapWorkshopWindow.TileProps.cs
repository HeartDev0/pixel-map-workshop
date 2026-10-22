// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using HeartDev.TilePropStudio;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private GameObject tilePropPrefab;
        [SerializeField] private bool placeTileProps;
        [SerializeField] private string selectedTilePropId;
        private Action refreshTilePropPanel;
        private Toggle tilePropPlacingToggle;
        private void OnTilePropExported(GameObject prefab)
        {
            if(document==null||prefab==null)return;string updated=AssetDatabase.GetAssetPath(prefab);
            if(!document.tileProps.Any(prop=>prop?.prefab!=null&&AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(prop.prefab),true).Contains(updated)))return;
            // Curved previews own mesh copies: rebuild them once after the complete export.
            rootVisualElement.schedule.Execute(()=>{if(document==null)return;InvalidatePreview();refreshTilePropPanel?.Invoke();RequestPreviewRepaint();}).ExecuteLater(80);
        }
        private void LeaveTilePropPlacement(){placeTileProps=false;tilePropPlacingToggle?.SetValueWithoutNotify(false);}
        private void BuildTilePropControls(VisualElement parent)
        {
            var fold=new Foldout{text="Props de tiles · 3D",value=false};parent.Add(fold);
            fold.Add(Button("Abrir Tile Prop Studio",()=>EditorApplication.ExecuteMenuItem("Tools/HeartDev/Tile Prop Studio")));
            var selectedPrefab=new ObjectField("Prefab"){objectType=typeof(GameObject),allowSceneObjects=false,value=tilePropPrefab};fold.Add(selectedPrefab);
            var placing=new Toggle("Colocar en el mapa"){value=placeTileProps};tilePropPlacingToggle=placing;fold.Add(placing);
            selectedPrefab.RegisterValueChangedCallback(e=>{tilePropPrefab=e.newValue as GameObject;placeTileProps=tilePropPrefab!=null;placing.SetValueWithoutNotify(placeTileProps);if(placeTileProps){previewMode=PreviewMode.Pintar;selectionTool=SelectionTool.Ninguna;RefreshSelectionTools();}});
            placing.RegisterValueChangedCallback(e=>{placeTileProps=e.newValue;if(placeTileProps){previewMode=PreviewMode.Pintar;selectionTool=SelectionTool.Ninguna;RefreshSelectionTools();}});
            fold.Add(Caption("Clic en el lienzo 2D o en 3D → Pintar para colocar. Usa 3D → Props para seleccionar uno; ajusta posición, altura y giro aquí.","hint"));
            var saved=new ScrollView();saved.style.maxHeight=170;saved.style.flexShrink=0;fold.Add(saved);
            void Library()
            {
                saved.Clear();var found=new HashSet<GameObject>();
                foreach(var guid in AssetDatabase.FindAssets("t:TilePropDocument"))
                {
                    var doc=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));if(doc==null)continue;var serialized=new SerializedObject(doc);var prefab=serialized.FindProperty("exportedPrefab")?.objectReferenceValue as GameObject;
                    if(prefab==null||!found.Add(prefab))continue;var meta=prefab.GetComponent<TilePropAsset>();if(meta==null)continue;
                    var row=Row();var image=new UnityEngine.UIElements.Image{image=meta.thumbnail,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};image.style.width=48;image.style.height=48;row.Add(image);
                    var choose=Button(prefab.name,()=>{tilePropPrefab=prefab;selectedPrefab.SetValueWithoutNotify(prefab);placeTileProps=true;placing.SetValueWithoutNotify(true);previewMode=PreviewMode.Pintar;selectionTool=SelectionTool.Ninguna;RefreshSelectionTools();});choose.style.flexGrow=1;row.Add(choose);saved.Add(row);
                }
                if(found.Count==0)saved.Add(Caption("Exporta un prop desde Tile Prop Studio para verlo aquí.","hint"));
            }
            fold.Add(Button("Actualizar biblioteca 3D",Library));Library();
            var content=new VisualElement();fold.Add(content);
            void Refresh()
            {
                content.Clear();if(document==null||document.tileProps.Count==0)return;
                int selected=document.tileProps.FindIndex(p=>p.id==selectedTilePropId);if(selected<0)selected=0;var prop=document.tileProps[selected];selectedTilePropId=prop.id;
                var names=document.tileProps.Select((p,i)=>(i+1)+" · "+(p.prefab!=null?p.prefab.name:"Prefab ausente")).ToList();var picker=new PopupField<string>("Colocado",names,selected);content.Add(picker);
                picker.RegisterValueChangedCallback(e=>{selectedTilePropId=document.tileProps[picker.index].id;rootVisualElement.schedule.Execute(Refresh);RequestPreviewRepaint();});
                var transforms=new VisualElement();transforms.SetEnabled(!document.layers.Exists(l=>l.id==prop.layerId&&(l.locked||!l.visible)));content.Add(transforms);
                var position=new Vector2IntField("Posición · px"){value=prop.pixel};position.RegisterValueChangedCallback(e=>EditTileProp(()=>prop.pixel=new Vector2Int(Mathf.Clamp(e.newValue.x,0,document.width-1),Mathf.Clamp(e.newValue.y,0,document.height-1)),"Mover prop de tiles"));transforms.Add(position);
                AddInt(transforms,"Altura · px",prop.heightOffsetPixels,v=>EditTileProp(()=>prop.heightOffsetPixels=Mathf.Clamp(v,-2048,2048),"Altura de prop de tiles"));
                var angle=new Slider("Giro Y",-180,180){value=prop.yaw,showInputField=true};angle.RegisterValueChangedCallback(e=>EditTileProp(()=>prop.yaw=e.newValue,"Girar prop de tiles"));transforms.Add(angle);
                transforms.Add(Button("Eliminar este prop",()=>{EditTileProp(()=>document.tileProps.Remove(prop),"Eliminar prop de tiles");rootVisualElement.schedule.Execute(Refresh);}));
            }
            refreshTilePropPanel=Refresh;Refresh();
        }
        private void EditTileProp(Action change,string title)
        {
            if(document==null)return;EndStroke();Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(document,title);change();EditorUtility.SetDirty(document);RefreshPaint();InvalidatePreview();RecordOperation(title);
        }
        private bool TryPlaceTileProp(Vector2Int pixel)
        {
            if(!placeTileProps)return false;if(document==null||pixel.x<0||pixel.y<0||pixel.x>=document.width||pixel.y>=document.height)return true;
            if(tilePropPrefab==null||tilePropPrefab.GetComponent<TilePropAsset>()==null){if(status!=null)status.text="Elige un prefab exportado por Tile Prop Studio.";return true;}
            if(document.tileProps.Count>=1024){status.text="Límite de 1024 props de tiles por mapa.";return true;}
            var layer=ActiveLayer;if(layer!=null&&(layer.locked||!layer.visible)){status.text="La capa activa está oculta o bloqueada.";return true;}
            EditTileProp(()=>
            {
                if(layer==null){layer=document.AddLayer("Props de tiles");activeLayerId=layer.id;QueueLayerRefresh(false);}
                var prop=new PixelMapDocument.TilePropPlacement{prefab=tilePropPrefab,pixel=pixel,layerId=layer.id};document.tileProps.Add(prop);selectedTilePropId=prop.id;
            },"Colocar prop de tiles");rootVisualElement.schedule.Execute(()=>refreshTilePropPanel?.Invoke());return true;
        }
        private bool HandleTilePropInput(Rect rect,Event e)
        {
            if(e.type!=EventType.MouseDown||e.button!=0||!rect.Contains(e.mousePosition)||document==null)return false;
            if(placeTileProps&&previewMode==PreviewMode.Pintar){if(HitTerrain(rect,e.mousePosition,out var pixel))TryPlaceTileProp(pixel);e.Use();return true;}
            if(previewMode!=PreviewMode.Props||model==null)return false;
            var ray=preview.camera.ViewportPointToRay(new Vector3((e.mousePosition.x-rect.x)/rect.width,1-(e.mousePosition.y-rect.y)/rect.height,0));string selected=null;float closest=float.PositiveInfinity;
            foreach(var prop in document.tileProps)
            {
                if(!document.TilePropVisible(prop))continue;var root=model.root.transform.Find("TileProp_"+prop.id);if(root==null)continue;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(renderer.bounds.IntersectRay(ray,out float distance)&&distance<closest){closest=distance;selected=prop.id;}
            }
            if(selected==null)return false;selectedTilePropId=selected;selectedProps.Clear();selectedProp=-1;rootVisualElement.schedule.Execute(()=>refreshTilePropPanel?.Invoke());RequestPreviewRepaint();e.Use();return true;
        }
        private void DrawTilePropSelection(Rect rect)
        {
            if(model==null||string.IsNullOrEmpty(selectedTilePropId))return;var root=model.root.transform.Find("TileProp_"+selectedTilePropId);if(root==null)return;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var b=renderer.bounds;var points=new Vector3[8];for(int i=0;i<8;i++)points[i]=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Handles.color=new Color(.55f,.94f,.85f);for(int i=0;i<8;i++)for(int axis=0;axis<3;axis++){int next=i^(1<<axis);if(next<=i)continue;var a=Project(rect,points[i]);var c=Project(rect,points[next]);if(rect.Contains(a)&&rect.Contains(c))Handles.DrawLine(a,c);}
            }
        }
    }
}
