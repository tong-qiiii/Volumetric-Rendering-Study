using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SunsetWeatherStudy
{
    const string Output = "Docs/SunsetWeather/";
    [InitializeOnLoadMethod] static void Queue()
    {
        if (File.Exists(".utmp/sunset-weather")) EditorApplication.delayCall += Run;
    }
    [MenuItem("Cloud Study/Apply and Capture Sunset Weather")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Run; return; }
        if (EditorApplication.isPlaying) { Debug.LogError("Exit Play mode before the weather study."); return; }
        try
        {
            Directory.CreateDirectory(Output);
            // Extend existing ridge geometry downwards without changing its visible summit.
            for(int i=0;i<6;i++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/SunsetSea/Ridge"+i+".asset");
                if(mesh && mesh.bounds.min.y > -1)
                {
                    var vertices=mesh.vertices;
                    for(int j=0;j<vertices.Length;j++) if(vertices[j].y<0) vertices[j].y*=12;
                    mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                }
            }
            // Keep the existing scene and camera untouched; only the shared cloud material changes.
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SunsetSea/Sea clouds.mat");
            SunsetSeaBuilder.Output = Output + "Baseline/";
            mat.SetFloat("_UseWeatherMap", 0); mat.SetFloat("_Erosion", .10f);
            mat.SetFloat("_NoiseThreshold",.20f);mat.SetFloat("_NoiseScale",5.5f);mat.SetFloat("_Density",.12f);
            mat.SetFloat("_ViewSteps",96);
            SunsetSeaBuilder.Capture();
            var texture = new Texture2D(256, 256, TextureFormat.RGB24, false, true);
            // Elliptical footprints: x/z center, x/z radius, normalized top height.
            Vector4[] cells = { new Vector4(.30f,.30f,.22f,.24f), new Vector4(.64f,.32f,.20f,.20f),
                new Vector4(.39f,.58f,.18f,.22f), new Vector4(.70f,.65f,.23f,.21f),
                new Vector4(.24f,.77f,.20f,.18f) };
            float[] tops = {.88f,.64f,.98f,.76f,.58f};
            for (int y=0; y<256; y++) for (int x=0; x<256; x++)
            {
                float u=x/255f, v=y/255f, coverage=0, top=.5f;
                for(int i=0;i<cells.Length;i++)
                {
                    var c=cells[i]; float dx=(u-c.x)/c.z, dz=(v-c.y)/c.w;
                    float radius=Mathf.Sqrt(dx*dx+dz*dz);
                    float wobble=(Mathf.PerlinNoise(u*19+4,v*19+8)-.5f)*.18f;
                    float value=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((radius+wobble-.15f)/.85f));
                    if(value>coverage){coverage=value;top=tops[i];}
                }
                texture.SetPixel(x,y,new Color(coverage,top,.08f));
            }
            texture.Apply();
            const string path="Assets/SunsetSea/SeaWeather.png";
            File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            mat.SetTexture("_WeatherMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetFloat("_UseWeatherMap",1);
            mat.SetFloat("_NoiseThreshold",.33f);mat.SetFloat("_NoiseScale",7f);mat.SetFloat("_Density",.15f);
            SunsetSeaBuilder.Output=Output+"Weather/";SunsetSeaBuilder.Capture();
            mat.SetFloat("_Erosion",.16f);
            SunsetSeaBuilder.Output=Output+"Final/";SunsetSeaBuilder.Capture();
            EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
            File.WriteAllText(Output+"complete.txt", "Fixed camera/light/frozen noise, 96 view steps in all captures. Baseline: original density shape. Weather: map plus threshold .33, noise scale 7, density .15. Final: erosion .10 to .16. Ridge lower vertices extended in all captures. " + DateTime.Now.ToString("s"));
            File.Delete(".utmp/sunset-weather");
        }
        catch(Exception e){File.WriteAllText(".utmp/sunset-weather-error.txt",e.ToString());Debug.LogException(e);}
        finally{SunsetSeaBuilder.Output="Docs/SunsetSea/";}
    }
}
