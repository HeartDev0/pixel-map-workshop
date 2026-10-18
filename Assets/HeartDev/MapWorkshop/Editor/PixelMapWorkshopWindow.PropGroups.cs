// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private readonly HashSet<int> selectedProps=new HashSet<int>();
        [SerializeField] private bool propsOnlySelection=true,groupPivot=true,stampGroup;
        [SerializeField] private int chosenGroup;
        [SerializeField] private string newGroupName="Mi conjunto";
        [SerializeField] private float groupAngle;
        private VisualElement groupGhost;
        private int ghostGroup=-1,ghostCount=-1;
        private PixelMapDocument ghostDocument;
        private Label propSelectionInfo;
        private PopupField<string> groupPicker;
        private static PixelMapDocument.Decoration CloneProp(PixelMapDocument.Decoration p)=>JsonUtility.FromJson<PixelMapDocument.Decoration>(JsonUtility.ToJson(p));
        private List<int> EditableSelectedProps()=>selectedProps.Where(i=>i>=0&&i<document.decorations.Count&&EditableProp(document.decorations[i])).OrderBy(i=>i).ToList();
        private void SelectPropsInArea()
        {
            if(document==null||selectionMask==null)return;selectedProps.Clear();
            for(int i=0;i<document.decorations.Count;i++){var p=document.decorations[i];if(EditableProp(p)&&SelectionContains(p.pixel.x,p.pixel.y))selectedProps.Add(i);}
            selectedProp=selectedProps.Count>0?selectedProps.First():-1;UpdatePropSelectionInfo();UpdatePickedLabel();RequestPreviewRepaint();
        }
        private void UpdatePropSelectionInfo(){if(canvas!=null&&document!=null)RefreshPaint();if(propSelectionInfo!=null)propSelectionInfo.text=$"{selectedProps.Count} props seleccionados · Mayús + clic en 3D para añadir o quitar";}
        private void BuildPropGroupControls(VisualElement parent)
        {
            var fold=new Foldout{text="Props · selección y conjuntos",value=false};parent.Add(fold);
            propSelectionInfo=Caption("","hint");fold.Add(propSelectionInfo);UpdatePropSelectionInfo();
            var only=new Toggle("Seleccionar solo props"){value=propsOnlySelection};only.RegisterValueChangedCallback(e=>propsOnlySelection=e.newValue);fold.Add(only);
            fold.Add(Button("Seleccionar props dentro del área",SelectPropsInArea));
            var pivot=new PopupField<string>("Pivote",new List<string>{"Centro del grupo","Cada objeto"},groupPivot?0:1);pivot.RegisterValueChangedCallback(e=>groupPivot=pivot.index==0);fold.Add(pivot);
            var delta=new Vector2IntField("Mover X/Z · px");fold.Add(delta);fold.Add(Button("Aplicar desplazamiento",()=>{TransformSelectedProps(delta.value,0,0,0);delta.SetValueWithoutNotify(Vector2Int.zero);}));
            var lift=new IntegerField("Elevar Y · px");fold.Add(lift);fold.Add(Button("Aplicar altura relativa",()=>{TransformSelectedProps(Vector2Int.zero,lift.value,0,0);lift.SetValueWithoutNotify(0);}));
            var angle=new FloatField("Girar · grados"){value=15};fold.Add(angle);fold.Add(Button("Aplicar giro",()=>TransformSelectedProps(Vector2Int.zero,0,angle.value,0)));
            var tilt=new FloatField("Inclinar · grados"){value=10};fold.Add(tilt);fold.Add(Button("Aplicar inclinación",()=>TransformSelectedProps(Vector2Int.zero,0,0,tilt.value)));
            var actions=Row();actions.style.flexWrap=Wrap.Wrap;fold.Add(actions);
            actions.Add(Button("Duplicar props",DuplicateSelectedProps));actions.Add(Button("Eliminar props",DeleteSelectedProps));
            actions.Add(Button("Apoyar en suelo",()=>EditSelectedProps(p=>p.heightOffsetPixels=0,"Apoyar props")));
            var shape=new EnumField("Convertir a",PixelMapDocument.PropShape.Sprite);fold.Add(shape);
            var depth=new IntegerField("Profundidad · px"){value=8};fold.Add(depth);
            var side=new ObjectField("Lateral de caja"){objectType=typeof(Sprite),allowSceneObjects=false};fold.Add(side);
            var top=new ObjectField("Tapa de caja"){objectType=typeof(Sprite),allowSceneObjects=false};fold.Add(top);
            fold.Add(Button("Aplicar forma a selección",()=>EditSelectedProps(p=>{p.shape=(PixelMapDocument.PropShape)shape.value;p.depthPixels=Mathf.Clamp(depth.value,1,256);p.sideSprite=side.value as Sprite;p.topSprite=top.value as Sprite;},"Convertir props")));
            fold.Add(Caption("Cajas: cada cara usa su sprite; sin asignación repite el frontal. La anchura y altura mantienen los píxeles originales.","hint"));
            var name=new TextField("Nombre del conjunto"){value=newGroupName};name.RegisterValueChangedCallback(e=>newGroupName=e.newValue);fold.Add(name);fold.Add(Button("Guardar selección como conjunto",SavePropGroup));
            groupPicker=new PopupField<string>("Conjunto",GroupNames(),Mathf.Clamp(chosenGroup,0,GroupNames().Count-1));groupPicker.RegisterValueChangedCallback(e=>chosenGroup=groupPicker.index);fold.Add(groupPicker);
            var groupToggle=new Toggle("Estampar conjunto con brocha"){value=stampGroup};groupToggle.RegisterValueChangedCallback(e=>{stampGroup=e.newValue;if(stampGroup)SelectTool(Brush.Figura);});fold.Add(groupToggle);
            var rotation=new Slider("Giro del conjunto",-180,180){value=groupAngle,showInputField=true};rotation.RegisterValueChangedCallback(e=>groupAngle=e.newValue);fold.Add(rotation);
            fold.Add(Button("Rellenar área con mezcla de props",()=>FillSelectionWithProps(false)));
            fold.Add(Button("Rellenar área con conjuntos",()=>FillSelectionWithProps(true)));
            fold.Add(Caption("Usa la selección rectangular o el lazo. Separación, densidad, caminos, agua y desnivel se configuran en Decoración inteligente. Un relleno se deshace en un paso.","hint"));
        }
        private List<string> GroupNames()=>document==null||document.propGroups.Count==0?new List<string>{"Sin conjuntos"}:document.propGroups.Select(g=>g.name).ToList();
        private void EditSelectedProps(Action<PixelMapDocument.Decoration> edit,string title)
        {
            if(document==null)return;var indices=EditableSelectedProps();if(indices.Count==0){status.text="Selecciona props primero.";return;}
            Undo.RegisterCompleteObjectUndo(document,title);foreach(int i in indices)edit(document.decorations[i]);FinishSelectionEdit(title);UpdatePickedLabel();
        }
        private bool CanMoveProps(List<int> indices,Vector2Int delta)
        {
            foreach(int i in indices){var p=document.decorations[i].pixel+delta;if(p.x<0||p.y<0||p.x>=document.width||p.y>=document.height)return false;}return true;
        }
        private void TransformSelectedProps(Vector2Int delta,int height,float angle,float tilt)
        {
            if(document==null)return;var indices=EditableSelectedProps();if(indices.Count==0)return;
            var center=Vector2.zero;foreach(int i in indices)center+=document.decorations[i].pixel;center/=indices.Count;
            var positions=new List<Vector2Int>();var rotation=Quaternion.Euler(0,angle,0);
            foreach(int i in indices)
            {
                var p=document.decorations[i];var position=p.pixel;
                if(groupPivot&&angle!=0){var offset=rotation*new Vector3(position.x-center.x,0,position.y-center.y);position=Vector2Int.RoundToInt(center+new Vector2(offset.x,offset.z));}
                position+=delta;if(position.x<0||position.y<0||position.x>=document.width||position.y>=document.height){status.text="El grupo saldría del lienzo. Amplía el lienzo o reduce el movimiento.";return;}positions.Add(position);
            }
            Undo.RegisterCompleteObjectUndo(document,"Transformar props");for(int j=0;j<indices.Count;j++){var p=document.decorations[indices[j]];p.pixel=positions[j];p.heightOffsetPixels=Mathf.Clamp(p.heightOffsetPixels+height,-2048,2048);p.yaw=Mathf.Repeat(p.yaw+angle,360);if(tilt!=0){p.pitch=SignedDegrees(document.PropPitch(p)+tilt);p.customPitch=true;}}FitPropSelectionBounds();FinishSelectionEdit("Transformar props");UpdatePickedLabel();
        }
        private void FitPropSelectionBounds()
        {
            if(!propsOnlySelection||selectionMask==null||document==null)return;var indices=EditableSelectedProps();if(indices.Count==0)return;
            int x0=document.width,y0=document.height,x1=0,y1=0;foreach(int i in indices){var p=document.decorations[i].pixel;x0=Mathf.Min(x0,p.x);y0=Mathf.Min(y0,p.y);x1=Mathf.Max(x1,p.x);y1=Mathf.Max(y1,p.y);}
            selectionWidth=document.width;selectionHeight=document.height;selectionMask=new bool[document.pixels.Length];selectionBounds=new RectInt(x0,y0,x1-x0+1,y1-y0+1);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)selectionMask[y*document.width+x]=true;RebuildSelectionTexture();
        }
        private void DuplicateSelectedProps()
        {
            if(document==null)return;var indices=EditableSelectedProps();if(indices.Count==0||document.decorations.Count+indices.Count>20000)return;
            var delta=new Vector2Int(3,-3);if(!CanMoveProps(indices,delta)){status.text="No hay margen para duplicar aquí.";return;}
            Undo.RegisterCompleteObjectUndo(document,"Duplicar props");selectedProps.Clear();string group=Guid.NewGuid().ToString("N");
            foreach(int i in indices){var copy=CloneProp(document.decorations[i]);copy.pixel+=delta;copy.groupId=group;selectedProps.Add(document.decorations.Count);document.decorations.Add(copy);}FinishSelectionEdit("Duplicar props");UpdatePropSelectionInfo();
        }
        private void DeleteSelectedProps()
        {
            if(document==null)return;var indices=EditableSelectedProps();if(indices.Count==0)return;Undo.RegisterCompleteObjectUndo(document,"Eliminar props seleccionados");
            for(int i=indices.Count-1;i>=0;i--)document.decorations.RemoveAt(indices[i]);selectedProps.Clear();selectedProp=-1;FinishSelectionEdit("Eliminar props seleccionados");UpdatePropSelectionInfo();UpdatePickedLabel();
        }
        private void SavePropGroup()
        {
            if(document==null)return;var indices=EditableSelectedProps();if(indices.Count==0){status.text="Selecciona los props que formarán el conjunto.";return;}
            var center=Vector2.zero;foreach(int i in indices)center+=document.decorations[i].pixel;var origin=Vector2Int.RoundToInt(center/indices.Count);
            Undo.RegisterCompleteObjectUndo(document,"Guardar conjunto");var group=new PixelMapDocument.PropGroup{name=string.IsNullOrWhiteSpace(newGroupName)?"Conjunto "+(document.propGroups.Count+1):newGroupName};
            foreach(int i in indices){var p=CloneProp(document.decorations[i]);p.pixel-=origin;p.layerId=null;p.groupId=null;group.props.Add(p);}document.propGroups.Add(group);chosenGroup=document.propGroups.Count-1;
            EditorUtility.SetDirty(document);groupPicker.choices=GroupNames();groupPicker.index=chosenGroup;RecordOperation("Guardar conjunto reutilizable");
        }
        private bool PlacePropGroup(Vector2Int center)
        {
            if(document==null||chosenGroup<0||chosenGroup>=document.propGroups.Count)return false;var group=document.propGroups[chosenGroup];if(document.decorations.Count+group.props.Count>20000)return false;
            var rotation=Quaternion.Euler(0,groupAngle,0);var points=new List<Vector2Int>();
            foreach(var prop in group.props)
            {
                var offset=rotation*new Vector3(prop.pixel.x,0,prop.pixel.y);var p=center+Vector2Int.RoundToInt(new Vector2(offset.x,offset.z));
                if(p.x<0||p.y<0||p.x>=document.width||p.y>=document.height||prop.sprite==null||!CanPlaceDecoration(p.x,p.y)||occupied.Contains(document.Index(p.x,p.y)))return false;
                points.Add(p);
            }
            string id=Guid.NewGuid().ToString("N");for(int i=0;i<group.props.Count;i++){var p=CloneProp(group.props[i]);p.pixel=points[i];p.yaw+=groupAngle;p.layerId=activeLayerId;p.groupId=id;document.decorations.Add(p);occupied.Add(document.Index(p.pixel.x,p.pixel.y));IndexPlacement(p.pixel);}return true;
        }
        private void FillSelectionWithProps(bool groups)
        {
            if(document==null||selectionMask==null){status.text="Dibuja una selección antes de rellenar.";return;}
            EndStroke();if(!PrepareLayer())return;Undo.RegisterCompleteObjectUndo(document,groups?"Rellenar con conjuntos":"Rellenar con props");BeginPlacementIndex();occupied.Clear();foreach(var p in document.decorations)occupied.Add(document.Index(p.pixel.x,p.pixel.y));
            int before=document.decorations.Count;int step=groups?Mathf.Max(2,decorationSpacing):1;
            for(int y=selectionBounds.yMin;y<selectionBounds.yMax;y+=step)for(int x=selectionBounds.xMin;x<selectionBounds.xMax;x+=step)
            {
                if(!SelectionContains(x,y))continue;if(groups){if(Hash01(x,y)<=density)PlacePropGroup(new Vector2Int(x,y));}else PlaceSmartDecoration(x,y,false);
            }
            selectedProps.Clear();for(int i=before;i<document.decorations.Count;i++)selectedProps.Add(i);FinishSelectionEdit("Rellenar selección con props");UpdatePropSelectionInfo();status.text=$"Colocados {document.decorations.Count-before} props. Ctrl+Z deshace el relleno.";
        }
        private void UpdateGroupGhost(Vector2 point)
        {
            if(groupGhost==null)return;bool show=document!=null&&stampGroup&&brush==Brush.Figura&&selectionTool==SelectionTool.Ninguna&&chosenGroup>=0&&chosenGroup<document.propGroups.Count;
            groupGhost.style.display=show?DisplayStyle.Flex:DisplayStyle.None;if(!show)return;
            var group=document.propGroups[chosenGroup];
            if(ghostGroup!=chosenGroup||ghostDocument!=document||ghostCount!=group.props.Count)
            {
                groupGhost.Clear();foreach(var p in group.props){var image=new UnityEngine.UIElements.Image{sprite=p.sprite,scaleMode=ScaleMode.StretchToFill,pickingMode=PickingMode.Ignore};image.style.position=Position.Absolute;groupGhost.Add(image);}ghostGroup=chosenGroup;ghostDocument=document;ghostCount=group.props.Count;
            }
            var center=Pixel(point);var rotation=Quaternion.Euler(0,groupAngle,0);
            for(int i=0;i<group.props.Count;i++)
            {
                var p=group.props[i];if(p.sprite==null)continue;var offset=rotation*new Vector3(p.pixel.x,0,p.pixel.y);var pixel=center+Vector2Int.RoundToInt(new Vector2(offset.x,offset.z));var image=groupGhost[i];
                image.style.width=p.sprite.rect.width*CanvasZoom;image.style.height=p.sprite.rect.height*CanvasZoom;image.style.left=(pixel.x+.5f-p.sprite.rect.width*.5f)*CanvasZoom;image.style.top=(document.height-pixel.y-.5f-p.sprite.rect.height)*CanvasZoom;
            }
        }
    }
}
