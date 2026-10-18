// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private bool recoveryEnabled=true;
        [SerializeField] private int recoverySeconds=120;
        [SerializeField] private List<string> operations=new List<string>();
        private double nextSafetyTick,nextRecovery,lastOperationTime;
        private string lastOperation="";
        private Label recoveryInfo,dirtyInfo;
        private VisualElement historyList;
        private const string RecoveryFolder="Assets/Editor/PixelMapWorkshopRecovery";
        private void RecordOperation(string title)
        {
            double now=EditorApplication.timeSinceStartup;
            if(lastOperation==title&&now-lastOperationTime<1){lastOperationTime=now;return;}
            lastOperation=title;lastOperationTime=now;operations.Insert(0,DateTime.Now.ToString("HH:mm:ss")+" · "+title);if(operations.Count>60)operations.RemoveAt(60);RefreshHistory();
        }
        private void RefreshHistory(){if(historyList==null)return;historyList.Clear();foreach(var item in operations){var label=new Label(item);label.AddToClassList("history-entry");historyList.Add(label);}}
        private void BuildSafetyControls(VisualElement parent)
        {
            var fold=new Foldout{text="Historial y recuperación",value=false};parent.Add(fold);
            var enabled=new Toggle("Copia automática de recuperación"){value=recoveryEnabled};enabled.RegisterValueChangedCallback(e=>recoveryEnabled=e.newValue);fold.Add(enabled);
            var interval=new SliderInt("Cada · segundos",30,600){value=recoverySeconds,showInputField=true};interval.RegisterValueChangedCallback(e=>{recoverySeconds=e.newValue;nextRecovery=EditorApplication.timeSinceStartup+recoverySeconds;});fold.Add(interval);
            recoveryInfo=Caption("Las copias no sustituyen tu guardado manual.","hint");fold.Add(recoveryInfo);
            fold.Add(Button("Crear copia ahora",CreateRecovery));fold.Add(Button("Restaurar última copia",RestoreRecovery));
            fold.Add(Caption("Copias en Assets/Editor/PixelMapWorkshopRecovery. Solo Editor. Restaurar admite Ctrl+Z.","hint"));
            var row=Row();fold.Add(row);row.Add(Button("Deshacer",()=>Undo.PerformUndo()));row.Add(Button("Rehacer",()=>Undo.PerformRedo()));
            var scroll=new ScrollView();scroll.style.height=130;fold.Add(scroll);historyList=new VisualElement();scroll.Add(historyList);RefreshHistory();
        }
        private void UpdateSafety()
        {
            if(EditorApplication.timeSinceStartup<nextSafetyTick)return;nextSafetyTick=EditorApplication.timeSinceStartup+1;
            bool dirty=document!=null&&EditorUtility.IsDirty(document);if(dirtyInfo!=null)dirtyInfo.text=document==null?"Sin documento":dirty?"● Sin guardar":"✓ Guardado";
            if(recoveryEnabled&&dirty&&!painting&&!lineDragging&&!selecting&&!LayerInteractionActive&&EditorApplication.timeSinceStartup>=nextRecovery){nextRecovery=EditorApplication.timeSinceStartup+Mathf.Max(30,recoverySeconds);CreateRecovery();}
        }
        private string RecoveryPath()
        {
            if(document==null)return null;string source=AssetDatabase.GetAssetPath(document);if(string.IsNullOrEmpty(source)||source.StartsWith(RecoveryFolder,StringComparison.Ordinal))return null;
            return RecoveryFolder+"/"+AssetDatabase.AssetPathToGUID(source)+".asset";
        }
        private void CreateRecovery()
        {
            string path=RecoveryPath();if(path==null)return;
            try
            {
                if(!AssetDatabase.IsValidFolder("Assets/Editor"))AssetDatabase.CreateFolder("Assets","Editor");if(!AssetDatabase.IsValidFolder(RecoveryFolder))AssetDatabase.CreateFolder("Assets/Editor","PixelMapWorkshopRecovery");
                var copy=AssetDatabase.LoadAssetAtPath<PixelMapDocument>(path);
                if(copy==null){copy=CreateInstance<PixelMapDocument>();EditorUtility.CopySerialized(document,copy);copy.name=document.name+" · Recuperación";AssetDatabase.CreateAsset(copy,path);}
                else{EditorUtility.CopySerialized(document,copy);copy.name=document.name+" · Recuperación";EditorUtility.SetDirty(copy);}
                AssetDatabase.SaveAssetIfDirty(copy);if(recoveryInfo!=null)recoveryInfo.text="Última copia: "+DateTime.Now.ToString("HH:mm:ss");nextRecovery=EditorApplication.timeSinceStartup+Mathf.Max(30,recoverySeconds);
            }
            catch(Exception e){if(recoveryInfo!=null)recoveryInfo.text="No se pudo crear la copia: "+e.Message;nextRecovery=EditorApplication.timeSinceStartup+60;}
        }
        private void RestoreRecovery()
        {
            string path=RecoveryPath();var copy=path==null?null:AssetDatabase.LoadAssetAtPath<PixelMapDocument>(path);if(copy==null){if(recoveryInfo!=null)recoveryInfo.text="Este mapa todavía no tiene una copia.";return;}
            EndStroke();string title=document.name;Undo.RegisterCompleteObjectUndo(document,"Restaurar copia de recuperación");EditorUtility.CopySerialized(copy,document);document.name=title;EditorUtility.SetDirty(document);ClearSelection();PixelMapPatterns.Clear();RefreshPaint();RefreshLayers();InvalidatePreview();RecordOperation("Restaurar copia de recuperación");
        }
    }
}
