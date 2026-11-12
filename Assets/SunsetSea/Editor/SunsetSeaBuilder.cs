using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
public static class SunsetSeaBuilder
{
    const string Root="Assets/SunsetSea/", Path="Assets/Scenes/SunsetSea.unity", Output="Docs/SunsetSea/";
    [InitializeOnLoadMethod] static void Queue(){if(File.Exists(".utmp/build-sunset"))EditorApplication.update+=Pending;}
    static void Pending()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
        EditorApplication.update-=Pending;File.Delete(".utmp/build-sunset");
        try{Build();}catch(Exception e){File.WriteAllText(".utmp/sunset-error.txt",e.ToString());}
    }
    static Material Mat(string name,Color color)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,Root+name+".mat");}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.1f);EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(string name,Vector3 p,Vector3 scale,Material m)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=m;return go;
    }
    [MenuItem("Cloud Study/Build Sunset Sea")]
    public static void Build()
    {
        var previous=SceneManager.GetActiveScene();
        if(previous.path==Path && previous.isDirty)EditorSceneManager.SaveScene(previous,".utmp/SunsetSea-before-rebuild.unity",true);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var rock=Mat("Ridge stone",new Color(.21f,.23f,.27f));var deck=Mat("Platform stone",new Color(.38f,.36f,.32f));var wood=Mat("Rail timber",new Color(.19f,.13f,.09f));
        Box("Observation deck",new Vector3(0,37,-12),new Vector3(20,1,20),deck);
        for(int side=-1;side<=1;side+=2)
        {
            Box("Side rail",new Vector3(side*9.5f,39,-12),new Vector3(.2f,.2f,20),wood);
            for(int j=0;j<6;j++)Box("Post",new Vector3(side*9.5f,38.3f,-21+j*3.6f),new Vector3(.3f,1.8f,.3f),wood);
        }
        // Low front parapet leaves an unobstructed view over the cloud sea.
        Box("Front parapet",new Vector3(0,37.9f,-2.5f),new Vector3(20,.8f,.45f),deck);
        Box("Rear wall",new Vector3(0,38,-21.5f),new Vector3(20,1,.5f),deck);
        var rng=new System.Random(36);
        for(int i=0;i<6;i++)
        {
            float x=i<2?(i==0?-62:68):(float)rng.NextDouble()*300-150;
            float z=i<2?45:140+(float)rng.NextDouble()*140;
            float height=i<2?54:28+(float)rng.NextDouble()*25;
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Mountain ridge";go.transform.position=new Vector3(x,-14,z);go.transform.localScale=new Vector3(42,height*2,48);go.GetComponent<Renderer>().sharedMaterial=rock;
            var source=go.GetComponent<MeshFilter>().sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Ridge "+i;
            var vertices=mesh.vertices;for(int j=0;j<vertices.Length;j++){var v=vertices[j];float n=Mathf.PerlinNoise(v.x*5+i,v.z*5+3);v.y*=.65f+n*.7f;vertices[j]=v;}
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            string mp=Root+"Ridge"+i+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(mp);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,mp);go.GetComponent<MeshFilter>().sharedMesh=mesh;
        }
        var cloud=AssetDatabase.LoadAssetAtPath<Material>(Root+"Sea clouds.mat");
        if(!cloud){cloud=new Material(Shader.Find("Custom/VolumetricFog"));AssetDatabase.CreateAsset(cloud,Root+"Sea clouds.mat");}
        cloud.SetFloat("_Density",.12f);cloud.SetFloat("_Coverage",1f);cloud.SetFloat("_NoiseThreshold",.20f);cloud.SetFloat("_NoiseScale",5.5f);cloud.SetFloat("_CloudBillow",.7f);cloud.SetFloat("_CloudBase",.06f);cloud.SetFloat("_CloudTop",.9f);cloud.SetFloat("_Erosion",.10f);cloud.SetFloat("_DetailScale",3.5f);cloud.SetFloat("_ViewSteps",48);cloud.SetFloat("_LightSteps",3);cloud.SetFloat("_SkySteps",1);cloud.SetFloat("_StepSize",.1f);cloud.SetFloat("_Anisotropy",.45f);cloud.SetFloat("_SecondaryAnisotropy",-.2f);cloud.SetFloat("_PhaseBlend",.25f);cloud.SetFloat("_AmbientStrength",.09f);cloud.SetFloat("_LightExtinction",1.1f);cloud.SetFloat("_CloudBounce",.35f);cloud.SetFloat("_NoiseSpeed",.003f);cloud.SetColor("_FogColor",new Color(.9f,.92f,1));
        var volume=Box("Cloud sea",new Vector3(0,20,130),new Vector3(330,44,300),cloud);UnityEngine.Object.DestroyImmediate(volume.GetComponent<Collider>());volume.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(0,39.2f,-4);camera.transform.rotation=Quaternion.Euler(8,0,0);camera.fieldOfView=62;camera.farClipPlane=650;camera.nearClipPlane=.1f;camera.clearFlags=CameraClearFlags.Skybox;camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;cameraObject.AddComponent<AudioListener>();var walker=cameraObject.AddComponent<ForestExplorer>();walker.spawnPosition=new Vector3(0,37.7f,-4);walker.respawnHeight=25;walker.volumeObjectPrefix="Cloud sea";
        var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.Soft;
        var sky=AssetDatabase.LoadAssetAtPath<Material>(Root+"Sunset sky.mat");if(!sky){sky=new Material(Shader.Find("Custom/SunsetSeaSky"));AssetDatabase.CreateAsset(sky,Root+"Sunset sky.mat");}sky.shader=Shader.Find("Custom/SunsetSeaSky");sky.SetFloat("_SunSize",.045f);sky.SetFloat("_AtmosphereThickness",1.1f);
        RenderSettings.skybox=sky;RenderSettings.sun=light;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;
        var lighting=new GameObject("Sunset stages").AddComponent<SunsetSeaLighting>();lighting.sun=light;lighting.clouds=volume.GetComponent<Renderer>();lighting.sky=sky;lighting.SetPhase(1);EditorUtility.SetDirty(sky);EditorUtility.SetDirty(cloud);
        AssetDatabase.SaveAssets();if(previous.path==Path)EditorSceneManager.CloseScene(previous,true);EditorSceneManager.SaveScene(scene,Path);
        CheckWalking();Capture();SceneManager.SetActiveScene(scene);lighting.SetPhase(1);RenderSettings.skybox=sky;EditorSceneManager.SaveScene(scene,Path);
        if(previous.IsValid()&&previous.isLoaded&&!previous.isDirty)EditorSceneManager.CloseScene(previous,true);
    }
    static void CheckWalking()
    {
        var probe=new GameObject("Temporary walking verification");
        var controller=probe.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.29f;controller.center=Vector3.up*.9f;controller.stepOffset=.32f;
        probe.transform.position=new Vector3(0,37.7f,-4);Physics.SyncTransforms();
        try
        {
            for(int i=0;i<40;i++)controller.Move(Vector3.down*.05f);
            bool grounded=controller.isGrounded;
            float startZ=probe.transform.position.z;
            for(int i=0;i<80;i++)controller.Move(new Vector3(0,-.03f,.05f));
            bool frontStopped=probe.transform.position.z < -2.7f && probe.transform.position.z>startZ;
            float startX=probe.transform.position.x;
            for(int i=0;i<20;i++)controller.Move(new Vector3(.05f,-.03f,0));
            bool lateral=probe.transform.position.x>startX+.7f;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"walking-check.txt", "Controller grounded="+grounded+"; front parapet stops movement="+frontStopped+"; lateral walking="+lateral+". Editor-driven CharacterController.Move checks. Keyboard/mouse feel not measured.");
            if(!grounded || !frontStopped || !lateral)throw new Exception("Platform walking collision check failed");
        }
        finally{UnityEngine.Object.DestroyImmediate(probe);}
    }
    [MenuItem("Cloud Study/Capture Sunset Sea")]
    public static void Capture()
    {
        var originalSky=RenderSettings.skybox;var originalAmbient=RenderSettings.ambientLight;
        Directory.CreateDirectory(Output);var scene=EditorSceneManager.OpenPreviewScene(Path);
        var roots=scene.GetRootGameObjects();var camera=roots.First(x=>x.name=="Main Camera").GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
        var lighting=roots.First(x=>x.name=="Sunset stages").GetComponent<SunsetSeaLighting>();
        var mat=new Material(lighting.clouds.sharedMaterial);lighting.clouds.sharedMaterial=mat;mat.SetFloat("_NoiseSpeed",0);var sky=new Material(lighting.sky);lighting.sky=sky;RenderSettings.skybox=sky;
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try{
            for(int i=0;i<3;i++){lighting.SetPhase(i);Render(camera,new[]{"01-before","02-sunset","03-after"}[i]);}
            lighting.SetPhase(1);mat.EnableKeyword("FOG_VALIDATE_IGNORE_DEPTH");Render(camera,"04-depth-disabled");mat.DisableKeyword("FOG_VALIDATE_IGNORE_DEPTH");lighting.clouds.enabled=false;Render(camera,"05-cloud-off");
            if(ShaderUtil.ShaderHasError(mat.shader))throw new Exception("Cloud shader compilation failed");
            File.WriteAllText(Output+"verification.txt","Unity "+Application.unityVersion+" / "+SystemInfo.graphicsDeviceName+"\n1280x720 fixed camera (0,39.2,-4), pitch 8, frozen noise. Sun elevations 20,4,-6. Shader supported: "+mat.shader.isSupported);
        }finally{ShaderUtil.allowAsyncCompilation=async;UnityEngine.Object.DestroyImmediate(mat);UnityEngine.Object.DestroyImmediate(sky);EditorSceneManager.ClosePreviewScene(scene);RenderSettings.skybox=originalSky;RenderSettings.ambientLight=originalAmbient;}
    }
    static void Render(Camera camera,string name)
    {
        var target=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var old=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Output+name+".png",texture.EncodeToPNG());}
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(texture);}
    }
}
