using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Thumbnails use real model geometry, never the Figma sample's blue cube for unrelated assets.
public sealed class SkillSyncModelThumbnails : IDisposable
{
    readonly Dictionary<int,RenderTexture> cache=new();
    public RenderTexture Get(GameObject model)
    {
        if(model==null) return null;
        int key=model.GetInstanceID();if(cache.TryGetValue(key,out var existing)) return existing;
        var texture=new RenderTexture(256,160,24,RenderTextureFormat.ARGB32) {name="Model thumbnail",hideFlags=HideFlags.DontSave};
        SkillSyncPlacementGhost preview=null;
        var cameraObject=new GameObject("ThumbnailCamera",typeof(Camera));cameraObject.hideFlags=HideFlags.DontSave;
        try
        {
            preview=new SkillSyncPlacementGhost(new PrefabEntry {typeId="thumbnail",prefab=model},false);
            preview.Transform.position=new Vector3(10000,10000,10000);
            foreach(var t in preview.Transform.GetComponentsInChildren<Transform>()) t.gameObject.layer=31;
            if(!PlacedObjectGrounding.TryGetRendererBounds(preview.Transform,out var bounds)) {UnityEngine.Object.Destroy(texture);return null;}
            var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline!=null) for(int i=0;i<pipeline.rendererDataList.Length;i++)
                if(pipeline.rendererDataList[i] is UniversalRendererData && pipeline.rendererDataList[i].name==ViewportLightingController.RendererName)
                {camera.GetUniversalAdditionalCameraData().SetRenderer(i);break;}
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.orthographic=true;
            float radius=Mathf.Max(.01f,bounds.extents.magnitude);
            camera.orthographicSize=radius*1.1f;camera.aspect=1.6f;camera.nearClipPlane=.001f;camera.farClipPlane=radius*12;
            camera.transform.position=bounds.center+new Vector3(1,.75f,-1).normalized*radius*4;
            camera.transform.LookAt(bounds.center);
            var extent=Vector2.zero;
            for(int i=0;i<8;i++) {
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var local=camera.transform.InverseTransformPoint(corner);
                extent=Vector2.Max(extent,new Vector2(Mathf.Abs(local.x),Mathf.Abs(local.y)));
            }
            camera.orthographicSize=Mathf.Max(extent.y,extent.x/camera.aspect)*1.04f;
            var request=new UniversalRenderPipeline.SingleCameraRequest {destination=texture};
            if(RenderPipeline.SupportsRenderRequest(camera,request)) RenderPipeline.SubmitRenderRequest(camera,request);
            else {camera.targetTexture=texture;camera.Render();}
            cache[key]=texture;return texture;
        }
        catch(Exception e) {UnityEngine.Object.Destroy(texture);Debug.LogWarning("[SkillSync] Thumbnail: "+e.Message);return null;}
        finally {preview?.Dispose();cameraObject.SetActive(false);UnityEngine.Object.Destroy(cameraObject);}
    }
    public void Dispose() {foreach(var texture in cache.Values) {texture.Release();UnityEngine.Object.Destroy(texture);}cache.Clear();}
}
