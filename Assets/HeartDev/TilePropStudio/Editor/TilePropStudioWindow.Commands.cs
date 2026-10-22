// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        private sealed class EditorCommand
        {
            public string id;
            public string label;
            public string shortcut;
            public KeyCode key;
            public bool control,shift,alt;
            public Action execute;
        }

        private readonly List<EditorCommand> commands = new List<EditorCommand>();
        private VisualElement commandSearchOverlay, commandSearchResults;
        private TextField commandSearchField;

        private void BuildCommandRegistry()
        {
            commands.Clear();
            void Add(string id,string label,string shortcut,KeyCode key,Action execute,bool control=false,bool shift=false,bool alt=false)
                => commands.Add(new EditorCommand{id=id,label=label,shortcut=shortcut,key=key,execute=execute,control=control,shift=shift,alt=alt});

            Add("mode.toggle","Alternar Object / Edit Mode","Tab",KeyCode.Tab,ToggleObjectEditMode);
            Add("mode.vertices","Modo vértices","1",KeyCode.Alpha1,()=>SetEditComponentMode(ComponentMode.Vertices));
            Add("mode.edges","Modo bordes","2",KeyCode.Alpha2,()=>SetEditComponentMode(ComponentMode.Bordes));
            Add("mode.faces","Modo caras","3",KeyCode.Alpha3,()=>SetEditComponentMode(ComponentMode.Caras));
            Add("transform.move","Mover","G",KeyCode.G,()=>BeginModalTransform(ModalOperation.Move));
            Add("transform.rotate","Rotar","R",KeyCode.R,()=>BeginModalTransform(ModalOperation.Rotate));
            Add("transform.scale","Escalar","S",KeyCode.S,()=>BeginModalTransform(ModalOperation.Scale));
            Add("mesh.extrude","Extruir","E",KeyCode.E,()=>BeginModalTransform(ModalOperation.Extrude));
            Add("mesh.duplicate","Duplicar y mover","Shift+D",KeyCode.D,()=>BeginModalTransform(ModalOperation.Duplicate),shift:true);
            Add("mesh.cut","Cortar caras por plano","",KeyCode.None,CutSelectedFaces);
            Add("mesh.weld","Soldar vértices por distancia","",KeyCode.None,WeldSelectedVertices);
            Add("mesh.bridge","Conectar dos bordes","",KeyCode.None,BridgeSelectedEdges);
            Add("uv.move","Desplazar UV en píxeles","",KeyCode.None,MoveSelectedUV);
            Add("uv.density","Restaurar densidad UV 1:1","",KeyCode.None,RestoreSelectedDensity);
            Add("uv.stitch","Coser UV contiguas","",KeyCode.None,StitchSelectedUV);
            Add("component.extract","Extraer selección a componente","",KeyCode.None,ExtractComponentSelection);
            Add("component.connect","Encajar conexiones del componente","",KeyCode.None,ConnectSelectedComponent);
            Add("selection.all","Seleccionar todo","A",KeyCode.A,SelectAllComponents);
            Add("selection.none","Deseleccionar todo","Alt+A",KeyCode.A,ClearSelection,alt:true);
            Add("selection.invert","Invertir selección","Ctrl+I",KeyCode.I,InvertSelection,control:true);
            Add("selection.connected","Seleccionar geometría conectada","L",KeyCode.L,SelectConnected);
            Add("selection.hide","Ocultar selección","H",KeyCode.H,()=>SetSelectedVisibility(true));
            Add("selection.show","Mostrar geometría","Alt+H",KeyCode.H,ShowAllFaces,alt:true);
            Add("selection.delete","Eliminar geometría","X / Delete",KeyCode.X,DeleteSelection);
            Add("selection.delete.backspace","Eliminar geometría","Delete",KeyCode.Delete,DeleteSelection);
            Add("selection.delete.backspace2","Eliminar geometría","Backspace",KeyCode.Backspace,DeleteSelection);
            Add("commands.search","Buscar comandos","F3",KeyCode.F3,ShowCommandSearch);
            Add("edit.undo","Deshacer","Ctrl+Z",KeyCode.Z,Undo.PerformUndo,control:true);
            Add("edit.redo","Rehacer","Ctrl+Shift+Z",KeyCode.Z,Undo.PerformRedo,control:true,shift:true);
            Add("operation.confirm","Confirmar operación","Enter",KeyCode.Return,ConfirmModalTransform);
            Add("operation.cancel","Cancelar operación","Escape",KeyCode.Escape,CancelModalTransform);
            Add("view.frame","Encuadrar selección","F",KeyCode.F,FrameSelection);
            Add("tool.place","Herramienta colocar","B",KeyCode.B,()=>SetTool(Tool.Colocar));
            Add("tool.paint","Herramienta pintar","P",KeyCode.P,()=>SetTool(Tool.Pintar));
            Add("tool.select","Herramienta seleccionar","V",KeyCode.V,()=>SetTool(Tool.Seleccionar));
            Add("tool.erase","Herramienta borrar","Shift+E",KeyCode.E,()=>SetTool(Tool.Borrar),shift:true);
            Add("tool.fill","Balde de tiles por región","Shift+F",KeyCode.F,()=>SetTool(Tool.Rellenar),shift:true);
            Add("tool.sample","Tomar tile y UV","I",KeyCode.I,()=>SetTool(Tool.TomarTile));
        }

        private void EnsureCommandRegistry()
        {
            if(commands.Count==0)BuildCommandRegistry();
        }

        private void RunCommand(string id)
        {
            if(document!=null&&!document.IsSupportedFormat)return;
            EnsureCommandRegistry();
            var command=commands.FirstOrDefault(item=>item.id==id);
            if(command==null||command.execute==null)return;
            command.execute();
        }

        private void Shortcuts(KeyDownEvent e)
        {
            if(document!=null&&!document.IsSupportedFormat)return;
            if(uvDragging&&e.keyCode==KeyCode.Escape){FinishUVDrag(true);e.PreventDefault();e.StopPropagation();return;}
            var focused=rootVisualElement!=null?rootVisualElement.focusController?.focusedElement as VisualElement:null;
            if(IsTextEntryTarget(e.target as VisualElement)||IsTextEntryTarget(focused))return;
            if(commandSearchOverlay!=null&&commandSearchOverlay.style.display==DisplayStyle.Flex)return;

            if(modalOperationActive)
            {
                if(HandleModalTransformKey(e)){e.PreventDefault();e.StopPropagation();}
                return;
            }

            bool control=e.ctrlKey||e.commandKey;
            KeyCode pressed=e.keyCode;
            int keyValue=(int)pressed,keypadStart=(int)KeyCode.Keypad0;
            if(keyValue>=keypadStart&&keyValue<=keypadStart+9)pressed=(KeyCode)((int)KeyCode.Alpha0+(keyValue-keypadStart));
            var command=commands.FirstOrDefault(item=>item.key==pressed&&item.control==control&&item.shift==e.shiftKey&&item.alt==e.altKey);
            if(command==null)return;
            if(command.id=="operation.confirm"||command.id=="operation.cancel")return;
            command.execute?.Invoke();
            e.PreventDefault();e.StopPropagation();
        }

        private static bool IsTextEntryTarget(VisualElement target)
        {
            for(var element=target;element!=null;element=element.parent)
            {
                string type=element.GetType().Name;
                if(type.Contains("TextField")||type.Contains("IntegerField")||type.Contains("FloatField")||type.Contains("DoubleField")||type.Contains("LongField"))return true;
                if(element.ClassListContains("unity-text-input")||element.ClassListContains("unity-base-text-field__input")||element.ClassListContains("unity-text-field__input"))return true;
            }
            return false;
        }

        private void SetTool(Tool next)
        {
            tool=next;RefreshToolButtons();SelectionChanged();
        }

        private void SetEditComponentMode(ComponentMode next)
        {
            if(next==ComponentMode.Objeto)return;
            editComponentMode=next;componentMode=next;tool=Tool.Seleccionar;ClearComponentSelection();
            if(componentModeField!=null)componentModeField.SetValueWithoutNotify(componentMode);
            RefreshToolButtons();SelectionChanged();
        }

        private void ToggleObjectEditMode()
        {
            if(componentMode==ComponentMode.Objeto)
            {
                bool hadSelection=selected.Count>0;
                componentMode=editComponentMode;
                ClearComponentSelection();
                if(hadSelection){SelectAllComponents();tool=Tool.Seleccionar;RefreshToolButtons();return;}
            }
            else
            {
                editComponentMode=componentMode;
                bool hadSelection=SelectionCount>0;
                componentMode=ComponentMode.Objeto;
                ClearComponentSelection();
                if(hadSelection&&document!=null)
                    foreach(var face in document.faces)if(face!=null&&!face.hidden&&!face.locked)selected.Add(face.id);
            }
            tool=Tool.Seleccionar;
            if(componentModeField!=null)componentModeField.SetValueWithoutNotify(componentMode);
            RefreshToolButtons();SelectionChanged();
        }

        private void ClearSelection()
        {
            ClearComponentSelection();SelectionChanged();
        }

        private void InvertSelection()
        {
            if(document==null)return;
            if(componentMode==ComponentMode.Objeto||componentMode==ComponentMode.Caras)
            {
                var available=document.faces.Where(face=>face!=null&&!face.hidden&&!face.locked).Select(face=>face.id).Distinct().ToList();
                var old=new HashSet<string>(selected);selected.Clear();foreach(var id in available)if(!old.Contains(id))selected.Add(id);
            }
            else if(componentMode==ComponentMode.Vertices)
            {
                var available=new HashSet<Vector3Int>();foreach(var face in document.faces)if(face!=null&&!face.hidden&&!face.locked)foreach(var point in face.Corners)available.Add(point);
                var old=new HashSet<Vector3Int>(selectedVertices);selectedVertices.Clear();foreach(var point in available)if(!old.Contains(point))selectedVertices.Add(point);
            }
            else
            {
                var available=new HashSet<EdgeKey>();foreach(var face in document.faces)if(face!=null&&!face.hidden&&!face.locked)foreach(var edge in FaceEdges(face))available.Add(edge);
                var old=new HashSet<EdgeKey>(selectedEdges);selectedEdges.Clear();foreach(var edge in available)if(!old.Contains(edge))selectedEdges.Add(edge);
            }
            SelectionChanged();
        }

        private void ShowCommandSearch()
        {
            if(rootVisualElement==null)return;
            EnsureCommandRegistry();
            if(commandSearchOverlay!=null){rootVisualElement.Remove(commandSearchOverlay);commandSearchOverlay=null;}
            var overlay=new VisualElement();commandSearchOverlay=overlay;
            overlay.style.position=Position.Absolute;overlay.style.left=Length.Percent(50);overlay.style.top=Length.Percent(18);overlay.style.width=360;overlay.style.maxHeight=420;
            overlay.style.marginLeft=-180;overlay.style.paddingLeft=12;overlay.style.paddingRight=12;overlay.style.paddingTop=10;overlay.style.paddingBottom=10;
            overlay.style.backgroundColor=new Color(.10f,.13f,.16f,.98f);overlay.style.borderTopLeftRadius=7;overlay.style.borderTopRightRadius=7;overlay.style.borderBottomLeftRadius=7;overlay.style.borderBottomRightRadius=7;
            overlay.style.borderTopWidth=1;overlay.style.borderBottomWidth=1;overlay.style.borderLeftWidth=1;overlay.style.borderRightWidth=1;
            overlay.style.borderTopColor=new Color(.35f,.60f,.56f);overlay.style.borderBottomColor=new Color(.35f,.60f,.56f);overlay.style.borderLeftColor=new Color(.35f,.60f,.56f);overlay.style.borderRightColor=new Color(.35f,.60f,.56f);
            var heading=new Label("BUSCAR COMANDO · F3");heading.style.unityFontStyleAndWeight=FontStyle.Bold;overlay.Add(heading);
            commandSearchField=new TextField();commandSearchField.value="";commandSearchField.style.marginTop=7;overlay.Add(commandSearchField);
            commandSearchResults=new ScrollView();commandSearchResults.style.maxHeight=340;commandSearchResults.style.marginTop=6;overlay.Add(commandSearchResults);
            var footer=new Label("Enter ejecutar · Esc cerrar");footer.style.marginTop=5;footer.style.opacity=.7f;overlay.Add(footer);
            commandSearchField.RegisterValueChangedCallback(_=>UpdateCommandSearchResults());
            commandSearchField.RegisterCallback<KeyDownEvent>(OnCommandSearchKey);
            rootVisualElement.Add(overlay);UpdateCommandSearchResults();
            commandSearchField.schedule.Execute(()=>commandSearchField?.Focus()).ExecuteLater(1);
        }

        private void OnCommandSearchKey(KeyDownEvent e)
        {
            if(e.keyCode==KeyCode.Escape){CloseCommandSearch();e.PreventDefault();e.StopPropagation();}
            else if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)
            {
                var first=GetFilteredCommands().FirstOrDefault();if(first!=null){CloseCommandSearch();first.execute?.Invoke();}e.PreventDefault();e.StopPropagation();
            }
        }

        private List<EditorCommand> GetFilteredCommands()
        {
            EnsureCommandRegistry();string query=commandSearchField!=null?commandSearchField.value:"";
            return commands.Where(item=>string.IsNullOrWhiteSpace(query)||item.label.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||item.id.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)
                .OrderBy(item=>item.label,StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private void UpdateCommandSearchResults()
        {
            if(commandSearchResults==null)return;
            commandSearchResults.Clear();
            foreach(var item in GetFilteredCommands())
            {
                var button=new Button(()=>{CloseCommandSearch();item.execute?.Invoke();}){text=string.IsNullOrEmpty(item.shortcut)?item.label:item.label+"    ·    "+item.shortcut};
                button.style.unityTextAlign=TextAnchor.MiddleLeft;commandSearchResults.Add(button);
            }
        }

        private void CloseCommandSearch()
        {
            if(commandSearchOverlay!=null)rootVisualElement?.Remove(commandSearchOverlay);
            commandSearchOverlay=null;commandSearchField=null;commandSearchResults=null;rootVisualElement?.Focus();
        }
    }
}
