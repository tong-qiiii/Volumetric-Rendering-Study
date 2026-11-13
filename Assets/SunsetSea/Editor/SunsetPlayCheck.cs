using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class SunsetPlayCheck
{
    static double next;
    static int step;
    static int inputFrame;
    static ForestExplorer explorer;
    static CharacterController motor;
    static Vector3 initial;
    static float ground;
    static float jumpRise, jumpDelta;
    static bool walked,jumped,firstPerson;
    static SunsetPlayCheck(){if(File.Exists(".utmp/play-sunset"))EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        if(!EditorApplication.isPlaying){EditorApplication.isPlaying=true;return;}
        if(EditorApplication.timeSinceStartup<next)return;
        if(step>1 && Time.frameCount<=inputFrame)return;
        explorer=UnityEngine.Object.FindFirstObjectByType<ForestExplorer>();if(!explorer)return;
        var field=typeof(ForestExplorer).GetField("motor",BindingFlags.Instance|BindingFlags.NonPublic);
        motor=(CharacterController)field.GetValue(explorer);if(!motor)return;
        try
        {
            if(step==0){next=EditorApplication.timeSinceStartup+1;step++;return;}
            typeof(ForestExplorer).GetField("captured",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(explorer,true);
            if(step==1){initial=motor.transform.position;ground=initial.y;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.A));next=EditorApplication.timeSinceStartup+.5;}
            else if(step==2){walked=Vector3.Distance(initial,motor.transform.position)>.4f;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Space));next=EditorApplication.timeSinceStartup+.18;}
            else if(step==3){jumpRise=motor.transform.position.y-ground;jumpDelta=Time.deltaTime;jumped=jumpRise>.15f;firstPerson=Vector3.Distance(explorer.transform.position,motor.transform.position+Vector3.up*1.65f)<.05f;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Digit3));next=EditorApplication.timeSinceStartup+.2;}
            else
            {
                var lighting=UnityEngine.Object.FindFirstObjectByType<SunsetSeaLighting>();bool phase=lighting.sun.intensity<.1f;
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
                File.WriteAllText("Docs/SunsetCumulus/play-check.txt","Play mode input test: A movement="+walked+"; Space jump="+jumped+"; eye-height camera="+firstPerson+"; key 3 after-sunset="+phase+"; rise="+jumpRise+"; frame dt="+jumpDelta+". InputSystem queued keyboard states through normal Update. Mouse feel not measured.");
                File.Delete(".utmp/play-sunset");EditorApplication.update-=Tick;EditorApplication.isPlaying=false;return;
            }
            inputFrame=Time.frameCount;step++;
        }
        catch(Exception e){File.WriteAllText(".utmp/play-sunset-error.txt",e.ToString());File.Delete(".utmp/play-sunset");EditorApplication.update-=Tick;EditorApplication.isPlaying=false;}
    }
}

// Cumulus scene regression.
