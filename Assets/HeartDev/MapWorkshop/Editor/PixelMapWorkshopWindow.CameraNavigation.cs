// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private float previewPanX;
        private bool previewNavigating,previewCameraInitialized,previewCameraSmoothing;
        private int previewNavigationControl,previewNavigationButton;
        private double previewCameraTime,previewCameraActiveUntil;
        private bool PreviewCameraActive=>previewNavigating||previewCameraSmoothing||EditorApplication.timeSinceStartup<previewCameraActiveUntil;
        private void RequestCameraRepaint(){previewCameraActiveUntil=EditorApplication.timeSinceStartup+.2;RequestPreviewRepaint();}
        private void ReleasePreviewNavigation()
        {
            if(previewNavigating&&GUIUtility.hotControl==previewNavigationControl)GUIUtility.hotControl=0;
            previewNavigating=false;
        }
        private void AddCameraSlider(VisualElement parent,string label,float min,float max,float value,Action<float> setter)
        {
            var slider=new Slider(label,min,max){value=value};
            slider.RegisterValueChangedCallback(e=>
            {
                setter(e.newValue);
                RequestCameraRepaint();
            });
            parent.Add(slider);
        }
        private bool HandleCameraNavigation(Rect rect,int control,Event e)
        {
            bool inside=rect.Contains(e.mousePosition);
            if(e.type==EventType.ScrollWheel&&inside)
            {
                distance=Mathf.Clamp(distance*Mathf.Exp(e.delta.y*.065f),.8f,15);RequestCameraRepaint();e.Use();return true;
            }
            if(e.type==EventType.MouseDown&&inside&&!previewDragging&&(e.button==1||e.button==2||e.button==0&&previewMode==PreviewMode.Navegar))
            {
                previewNavigationControl=control;previewNavigationButton=e.button;previewNavigating=true;GUIUtility.hotControl=control;previewHover=false;RequestCameraRepaint();e.Use();return true;
            }
            if(!previewNavigating)return false;
            if((e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape)||(e.type==EventType.MouseUp&&e.button==previewNavigationButton))
            {
                ReleasePreviewNavigation();RequestCameraRepaint();e.Use();return true;
            }
            if(e.type!=EventType.MouseDrag)return false;
            if(previewNavigationButton==1)
            {
                yaw=Mathf.Repeat(yaw+e.delta.x*.45f+180,360)-180;pitch=Mathf.Clamp(pitch+e.delta.y*.4f,10,85);
            }
            else
            {
                // Screen-space pan projected onto the ground, independent of document length.
                float scale=2*distance*Mathf.Tan(preview.camera.fieldOfView*.5f*Mathf.Deg2Rad)/Mathf.Max(1,rect.height);
                var rotation=Quaternion.Euler(pitch,yaw,0);var right=rotation*Vector3.right;
                var up=Vector3.ProjectOnPlane(rotation*Vector3.up,Vector3.up).normalized;
                var delta=-right*(e.delta.x*scale)+up*(e.delta.y*scale/Mathf.Max(.25f,Mathf.Sin(pitch*Mathf.Deg2Rad)));
                previewPanX=Mathf.Clamp(previewPanX+delta.x,-document.width/(float)document.pixelsPerUnit,document.width/(float)document.pixelsPerUnit);
                viewPosition=Mathf.Clamp01(viewPosition+delta.z*document.pixelsPerUnit/document.height);
            }
            RequestCameraRepaint();e.Use();return true;
        }
        private void ApplyPreviewCamera(Vector3 position,Quaternion rotation)
        {
            double now=EditorApplication.timeSinceStartup;float dt=Mathf.Clamp((float)(now-previewCameraTime),0,.1f);previewCameraTime=now;
            var camera=preview.camera.transform;
            if(!previewCameraInitialized){camera.SetPositionAndRotation(position,rotation);previewCameraInitialized=true;previewCameraSmoothing=false;return;}
            float t=1-Mathf.Exp(-28*dt);var next=Vector3.Lerp(camera.position,position,t);var turn=Quaternion.Slerp(camera.rotation,rotation,t);
            previewCameraSmoothing=(position-next).sqrMagnitude>.000001f||Quaternion.Angle(turn,rotation)>.025f;
            if(!previewCameraSmoothing){next=position;turn=rotation;}
            camera.SetPositionAndRotation(next,turn);if(previewCameraSmoothing)RequestPreviewRepaint();
        }
    }
}
