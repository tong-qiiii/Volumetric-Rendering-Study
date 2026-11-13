using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class SunsetCumulusStudy
{
    [InitializeOnLoadMethod] static void Queue(){if(File.Exists(".utmp/cumulus"))EditorApplication.delayCall+=Run;}
    [MenuItem("Cloud Study/Apply Rounded Cumulus")]
    public static void Run()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Run;return;}
        if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;EditorApplication.delayCall+=Run;return;}
        try
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/SunsetSea.unity")throw new Exception("Open SunsetSea first.");
            if(scene.isDirty)EditorSceneManager.SaveScene(scene,".utmp/SunsetSea-before-cumulus.unity",true);
            var volume=scene.GetRootGameObjects().First(x=>x.name=="Cloud sea");
            var mat=volume.GetComponent<Renderer>().sharedMaterial;
            if(!File.Exists("Docs/SunsetCumulus/Before/02-sunset.png"))
            {
                SunsetSeaBuilder.Output="Docs/SunsetCumulus/Before/";SunsetSeaBuilder.Capture();
            }
            volume.transform.position=new Vector3(0,0,130);volume.transform.localScale=new Vector3(330,120,300);
            mat.SetFloat("_CumulusMode",1);mat.SetFloat("_Density",.095f);
            mat.SetFloat("_ViewSteps",192);mat.SetFloat("_LightSteps",8);mat.SetFloat("_SkySteps",3);
            mat.SetFloat("_LightExtinction",1);mat.SetFloat("_AmbientStrength",.07f);mat.SetFloat("_CloudBounce",.4f);
            EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            SunsetSeaBuilder.Output="Docs/SunsetCumulus/After/";SunsetSeaBuilder.Capture();
            mat.SetFloat("_ViewSteps",256);SunsetSeaBuilder.Output="Docs/SunsetCumulus/Reference/";SunsetSeaBuilder.Capture();
            mat.SetFloat("_ViewSteps",192);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
            File.WriteAllText("Docs/SunsetCumulus/complete.txt","Rounded world-metric lobes; 192 view samples with fixed spatial jitter; 8 near-biased sun samples, 3 sky samples. Same camera and phase lighting as before. "+DateTime.Now.ToString("s"));
            File.Delete(".utmp/cumulus");
        }
        catch(Exception e){File.WriteAllText(".utmp/cumulus-error.txt",e.ToString());Debug.LogException(e);}
        finally{SunsetSeaBuilder.Output="Docs/SunsetSea/";}
    }
}
