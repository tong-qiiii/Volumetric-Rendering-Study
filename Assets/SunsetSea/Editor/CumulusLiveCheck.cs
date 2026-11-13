using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class CumulusLiveCheck
{
    static int last=-1,warmup;
    static readonly List<float> times=new List<float>();
    static CumulusLiveCheck(){if(File.Exists(".utmp/cumulus-live"))EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!EditorApplication.isPlaying){EditorApplication.isPlaying=true;return;}
        if(Time.frameCount==last)return;last=Time.frameCount;
        if(++warmup<=30)return;
        times.Add(Time.unscaledDeltaTime*1000);
        if(times.Count<90)return;
        times.Sort();var camera=Camera.main;
        File.WriteAllText("Docs/CumulusPerformance/live.txt",string.Format(System.Globalization.CultureInfo.InvariantCulture,"Play mode Editor frame intervals: 30 warmup frames + 90 samples; median={0:F3} ms; p95={1:F3} ms; camera={2}x{3}; GPU={4}; vSyncCount={5}; targetFrameRate={6}. Includes Editor overhead; not standalone build performance; unchanged frame cap settings.",times[45],times[85],camera.pixelWidth,camera.pixelHeight,SystemInfo.graphicsDeviceName,QualitySettings.vSyncCount,Application.targetFrameRate));
        File.Delete(".utmp/cumulus-live");File.WriteAllText(".utmp/play-sunset","run");EditorApplication.update-=Tick;EditorApplication.isPlaying=false;
    }
}
// Final 96/8/3 preset.
