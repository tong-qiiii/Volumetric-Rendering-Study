using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ForestPerformanceCheck
{
    [InitializeOnLoadMethod] static void Queue(){if(File.Exists(".utmp/forest-benchmark"))EditorApplication.update+=Pending;}
    static void Pending(){if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}EditorApplication.update-=Pending;File.Delete(".utmp/forest-benchmark");try{Run();}catch(Exception e){File.WriteAllText(".utmp/benchmark-error.txt",e.ToString());}}
    [MenuItem("Cloud Study/Benchmark Forest Sampling")]
    public static void Run()
    {
        const string folder="Docs/MistForest/Performance/";Directory.CreateDirectory(folder);
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/MistForest.unity");
        var camera=scene.GetRootGameObjects().First(x=>x.name=="Main Camera").GetComponent<Camera>();
        camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
        // Eye-level pose shared by every measured configuration.
        camera.transform.position=new Vector3(.1f,1.85f,-7.5f);camera.transform.rotation=Quaternion.identity;
        var renderer=scene.GetRootGameObjects().First(x=>x.name.StartsWith("Forest volume")).GetComponent<Renderer>();
        var mat=new Material(renderer.sharedMaterial);renderer.sharedMaterial=mat;mat.SetFloat("_NoiseSpeed",0);
        var target=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var readback=new Texture2D(1280,720,TextureFormat.RGB24,false);var active=RenderTexture.active;
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var rows=new System.Text.StringBuilder("preset,view_steps,sun_steps,sky_steps,median_ms,min_ms,max_ms\n");
        try
        {
            string[] names={"baseline","view48","balanced","fog_off"};int[] views={128,48,48,48};int[] suns={6,6,3,3};int[] skies={3,3,1,1};
            for(int k=0;k<4;k++)
            {
                renderer.enabled=k!=3;mat.SetFloat("_ViewSteps",views[k]);mat.SetFloat("_LightSteps",suns[k]);mat.SetFloat("_SkySteps",skies[k]);
                var times=new double[8];
                for(int n=-3;n<8;n++)
                {
                    var timer=Stopwatch.StartNew();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,1280,720),0,0);readback.Apply();timer.Stop();
                    if(n>=0)times[n]=timer.Elapsed.TotalMilliseconds;
                }
                File.WriteAllBytes(folder+names[k]+".png",readback.EncodeToPNG());Array.Sort(times);
                rows.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4:F3},{5:F3},{6:F3}",names[k],views[k],suns[k],skies[k],(times[3]+times[4])*.5,times[0],times[7]));
            }
            if(ShaderUtil.ShaderHasError(mat.shader))throw new Exception("Volume shader has errors");
            File.WriteAllText(folder+"timings.csv",rows.ToString());
            File.WriteAllText(folder+"method.txt","Unity "+Application.unityVersion+"; "+SystemInfo.graphicsDeviceName+"; "+SystemInfo.graphicsDeviceType+"\n1280x720. Preview scene, fixed eye-level camera, frozen noise. Three warmups then eight measured renders per preset. Wall time includes CPU render submission, GPU completion and synchronous ReadPixels/Apply. NOT GPU-only time or live gameplay FPS. Presets measured sequentially. Shader error check passed.");
        }
        finally{ShaderUtil.allowAsyncCompilation=async;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(mat);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
