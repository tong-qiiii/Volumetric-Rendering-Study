using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class CumulusPerformance
{
    const string Folder="Docs/CumulusPerformance/";
    [InitializeOnLoadMethod] static void Queue(){if(File.Exists(".utmp/cumulus-perf"))EditorApplication.delayCall+=Run;}
    [MenuItem("Cloud Study/Cache and Benchmark Cumulus")]
    public static void Run()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Run;return;}
        if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;EditorApplication.delayCall+=Run;return;}
        try{Bake();Benchmark(1280,720,Folder);Benchmark(2560,1440,Folder+"QHD/");SunsetSeaBuilder.Output=Folder+"Validation/";try{SunsetSeaBuilder.Capture();}finally{SunsetSeaBuilder.Output="Docs/SunsetSea/";}File.Delete(".utmp/cumulus-perf");}
        catch(Exception e){File.WriteAllText(".utmp/cumulus-perf-error.txt",e.ToString());UnityEngine.Debug.LogException(e);}
    }
    static void Bake()
    {
        var compute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/BakeCumulus.compute");
        var rt=new RenderTexture(256,128,0,RenderTextureFormat.RHalf){dimension=TextureDimension.Tex3D,volumeDepth=256,enableRandomWrite=true};rt.Create();
        try
        {
            int kernel=compute.FindKernel("Bake");compute.SetTexture(kernel,"Result",rt);compute.Dispatch(kernel,32,16,32);
            var data=AsyncGPUReadback.Request(rt,0);data.WaitForCompletion();if(data.hasError)throw new Exception("Density cache readback failed");
            var tex=new Texture3D(256,128,256,TextureFormat.RHalf,false){name="Cached cumulus density",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var voxels=new ushort[256*128*256];int offset=0;
            for(int layer=0;layer<data.layerCount;layer++)
            {
                var slice=data.GetData<ushort>(layer).ToArray();Array.Copy(slice,0,voxels,offset,slice.Length);offset+=slice.Length;
            }
            if(offset!=voxels.Length)throw new Exception("Unexpected density cache size: "+offset);
            tex.SetPixelData(voxels,0);tex.Apply(false,true);
            const string path="Assets/SunsetSea/CumulusDensity.asset";
            var old=AssetDatabase.LoadAssetAtPath<Texture3D>(path);
            if(old){EditorUtility.CopySerialized(tex,old);UnityEngine.Object.DestroyImmediate(tex);}else{AssetDatabase.CreateAsset(tex,path);old=tex;}
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/SunsetSea/Sea clouds.mat");mat.SetTexture("_CumulusDensityTex",old);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
        }
        finally{rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    static void Benchmark(int width,int height,string folder)
    {
        Directory.CreateDirectory(folder);
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/SunsetSea.unity");
        var roots=scene.GetRootGameObjects();var camera=roots.First(x=>x.name=="Main Camera").GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
        var lighting=roots.First(x=>x.name=="Sunset stages").GetComponent<SunsetSeaLighting>();
        var originalSky=RenderSettings.skybox;var ambient=RenderSettings.ambientLight;
        var source=lighting.clouds.sharedMaterial;var mat=new Material(source);lighting.clouds.sharedMaterial=mat;mat.SetFloat("_NoiseSpeed",0);
        var sky=new Material(lighting.sky);lighting.sky=sky;RenderSettings.skybox=sky;lighting.SetPhase(1);
        var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var active=RenderTexture.active;var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var rows=new System.Text.StringBuilder("preset,view,sun,sky,median_ms,min_ms,max_ms\n");
        try
        {
            string[] names={"procedural","cache_same","balanced","fast","cloud_off"};int[] views={192,192,96,64,96},suns={8,8,8,6,8},skies={3,3,3,2,3};
            for(int k=0;k<names.Length;k++)
            {
                mat.SetFloat("_UseBakedDensity",k==0?0:1);mat.SetFloat("_ViewSteps",views[k]);mat.SetFloat("_LightSteps",suns[k]);mat.SetFloat("_SkySteps",skies[k]);lighting.clouds.enabled=k!=4;
                var times=new double[5];
                for(int n=-2;n<5;n++)
                {
                    var clock=Stopwatch.StartNew();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();clock.Stop();if(n>=0)times[n]=clock.Elapsed.TotalMilliseconds;
                }
                File.WriteAllBytes(folder+names[k]+".png",pixels.EncodeToPNG());Array.Sort(times);
                rows.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4:F3},{5:F3},{6:F3}",names[k],views[k],suns[k],skies[k],times[2],times[0],times[4]));File.WriteAllText(folder+"timings.csv",rows.ToString());
            }
            if(ShaderUtil.ShaderHasError(mat.shader))throw new Exception("Cloud shader error");
            source.SetFloat("_UseBakedDensity",1);source.SetFloat("_ViewSteps",96);source.SetFloat("_LightSteps",8);source.SetFloat("_SkySteps",3);EditorUtility.SetDirty(source);AssetDatabase.SaveAssets();
            File.WriteAllText(folder+"method.txt",Application.unityVersion+" / "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+"\n"+width+"x"+height+", fixed sunset camera, frozen wind. 2 warmups + 5 samples per preset. Median wall time includes render submission, GPU completion and synchronous readback. Not gameplay FPS or GPU-only time. 256x128x256 RHalf density cache (16 MiB raw). Same source density function baked at voxel centers. Linear filtering; no mipmaps. Default changed to cached 96/8/3. Shader check passed.");
        }
        finally{ShaderUtil.allowAsyncCompilation=async;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(mat);UnityEngine.Object.DestroyImmediate(sky);EditorSceneManager.ClosePreviewScene(scene);RenderSettings.skybox=originalSky;RenderSettings.ambientLight=ambient;}
    }
}
