using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The scene is baked into ordinary meshes and materials; no generator is needed in a player.
public static class MistForestBuilder
{
    const string Root = "Assets/MistForest/";
    const string ScenePath = "Assets/Scenes/MistForest.unity";
    const string Request = ".utmp/build-mist-forest";
    const string Output = "Docs/MistForest/";
    static System.Random rng;
    static Mesh meshContainer;
    static Scene scene;
    static Material bark, moss, stone, leaf, fern, amber, rune;
    static Mesh boulder, crown;
    static float R(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
    static float PathX(float z) => 1.7f * Mathf.Sin((z + 5) * .105f);
    static float Height(float x, float z) => .12f * Mathf.Sin(z * .23f) + .10f * Mathf.Cos(x * .42f)
        + .95f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3, 13, Mathf.Abs(x - PathX(z)))) * Mathf.PerlinNoise(x * .08f + 20, z * .08f + 10);

    [InitializeOnLoadMethod] static void Queue()
    {
        if (!File.Exists(Request)) return;
        EditorApplication.delayCall += RunRequested;
    }
    static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += RunRequested; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.playModeStateChanged += OnEditMode; return; }
        if (!File.Exists(Request)) return;
        string operation=File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if(operation=="capture") CaptureSaved(); else Build(); File.WriteAllText(".utmp/forest-complete.txt", "Built and rendered: " + DateTime.Now + "\n" + SystemInfo.graphicsDeviceName); }
        catch (Exception e) { File.WriteAllText(".utmp/forest-error.txt", e.ToString()); Debug.LogException(e); }
    }
    static void OnEditMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnEditMode;
        EditorApplication.delayCall += RunRequested;
    }

    [MenuItem("Cloud Study/Build Mist Forest")]
    public static void Build()
    {
        var existingScene = SceneManager.GetSceneByPath(ScenePath);
        if (existingScene.IsValid() && existingScene.isLoaded && existingScene.isDirty)
            throw new InvalidOperationException("Save your forest scene edits before rebuilding.");
        Directory.CreateDirectory(Root + "Materials"); Directory.CreateDirectory(Output);
        rng = new System.Random(251107); treeIndex = 0;
        var original = SceneManager.GetActiveScene();
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            meshContainer = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "ForestGeometry.asset");
            if (!meshContainer) { meshContainer = new Mesh { name = "Forest geometry" }; AssetDatabase.CreateAsset(meshContainer, Root + "ForestGeometry.asset"); }
            // Rebuild only the named generated assets, retaining their GUIDs.
            bark = Mat("Old bark", new Color(.14f,.19f,.15f));
            moss = Mat("Velvet moss", new Color(.22f,.32f,.12f));
            stone = Mat("Weathered stone", new Color(.32f,.37f,.30f));
            leaf = Mat("Canopy", new Color(.10f,.21f,.12f));
            fern = Mat("Fern", new Color(.26f,.39f,.15f)); fern.SetFloat("_Cull", 0);
            amber = Mat("Lantern amber", new Color(1,.5f,.12f), new Color(2.8f,1.2f,.26f));
            rune = Mat("Old turquoise", new Color(.28f,.68f,.53f), new Color(.3f,1.0f,.64f));
            boulder = RockMesh("Rock", 12, 8, .15f);
            crown = RockMesh("Crown", 14, 9, .24f);
            BarkTexture();
            Ground();
            var trees = Group("Ancient trees");
            Tree(new Vector3(-5.3f,0,-5), 15, 1.2f, trees);
            Tree(new Vector3(5.7f,0,-1), 17, 1.1f, trees);
            Tree(new Vector3(-6,0,9), 18, 1.05f, trees);
            Tree(new Vector3(6.5f,0,14), 20, 1.3f, trees);
            for(int i=0;i<58;i++)
            {
                float z=R(-7,62), x=R(-25,25);
                if(Mathf.Abs(x-PathX(z))<4.6f || (Mathf.Abs(x)<4 && z>14 && z<26)) continue;
                Tree(new Vector3(x,0,z), R(12,23), R(.44f,.94f), trees);
            }
            var undergrowth = Group("Moss rocks and ferns");
            for(int i=0;i<82;i++)
            {
                float z=R(-9,46),x=R(-19,19); if(Mathf.Abs(x-PathX(z))<2.7f)continue;
                var p=new Vector3(x,Height(x,z),z); float size=R(.3f,1.3f);
                var rock=MeshObject("Mossy rock",boulder,stone,p,new Vector3(size*1.5f,size*.7f,size),undergrowth);
                rock.transform.rotation=Quaternion.Euler(R(-12,12),R(0,360),R(-14,14));
                MeshObject("Moss cap",boulder,moss,p+Vector3.up*size*.31f,new Vector3(size*1.38f,size*.25f,size*.93f),undergrowth);
            }
            Ferns(undergrowth);
            var trail=Group("Broken pilgrim path");
            for(int i=0;i<31;i++)
            {
                float z=-8+i*1.04f, x=PathX(z)+R(-.35f,.35f);
                var slab=MeshObject("Sunken stepping stone",boulder,stone,new Vector3(x,Height(x,z)+.055f,z),new Vector3(R(.65f,1.2f),R(.06f,.12f),R(.37f,.60f)),trail);
                slab.transform.rotation=Quaternion.Euler(0,R(-25,25),0);
            }
            Shrine();
            Lantern(new Vector3(-2.65f,0,-1.5f)); Lantern(new Vector3(3.7f,0,8));
            Lantern(new Vector3(-2.7f,0,20));
            Mushrooms(undergrowth);
            var cameraObject=Group("Main Camera"); cameraObject.tag="MainCamera";
            var camera=cameraObject.AddComponent<Camera>(); camera.transform.position=new Vector3(.1f,2.55f,-11.5f);
            camera.transform.rotation=Quaternion.LookRotation(new Vector3(.0f,3.35f,20)-camera.transform.position);
            camera.fieldOfView=62; camera.nearClipPlane=.1f; camera.farClipPlane=95;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.21f,.31f,.27f);
            camera.allowHDR=true; camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ForestExplorer>();
            var sunObject=Group("Sun through canopy"); var sun=sunObject.AddComponent<Light>(); sun.type=LightType.Directional;
            sun.color=new Color(.91f,.96f,1f); sun.intensity=1.35f; sun.shadows=LightShadows.Soft;
            sun.shadowBias=.03f; sun.shadowNormalBias=.25f; sunObject.transform.rotation=Quaternion.Euler(48,-32,0);
            var fill=Group("Soft canopy fill").AddComponent<Light>();fill.type=LightType.Directional;fill.color=new Color(.48f,.65f,.60f);fill.intensity=.4f;fill.shadows=LightShadows.None;fill.transform.rotation=Quaternion.Euler(25,165,0);
            RenderSettings.sun=sun; RenderSettings.skybox=null; RenderSettings.fog=false;
            RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.24f,.34f,.29f);
            RenderSettings.ambientEquatorColor=new Color(.13f,.19f,.145f); RenderSettings.ambientGroundColor=new Color(.06f,.08f,.055f);
            RenderSettings.ambientIntensity=1; RenderSettings.reflectionIntensity=.1f;
            PostProcess();
            var fog=Fog();
            AssetDatabase.SaveAssets();
            if(original.IsValid() && original.isLoaded && original.path==ScenePath)
            {
                if(original.isDirty)throw new InvalidOperationException("Save your forest scene edits before rebuilding.");
                EditorSceneManager.CloseScene(original,true);
            }
            if (!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Could not save forest scene");
            CaptureSaved();
            // Leave the new saved scene active, keeping any previously open user scene in memory.
            if(original.IsValid() && original.isLoaded && !original.isDirty) EditorSceneManager.CloseScene(original,true);
            SceneManager.SetActiveScene(scene);
            Selection.activeObject=cameraObject;
            Debug.Log("MIST_FOREST_COMPLETE " + SystemInfo.graphicsDeviceName);
        }
        catch { if(original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original); throw; }
    }

    static GameObject Group(string name, GameObject parent=null)
    {
        var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,scene);
        if(parent)go.transform.SetParent(parent.transform,false); return go;
    }
    static Material Mat(string name, Color color, Color emission=default)
    {
        string path=Root+"Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.08f);
        if(emission.maxColorComponent>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",emission);}
        EditorUtility.SetDirty(mat);return mat;
    }
    static Mesh Store(Mesh mesh)
    {
        foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(Root+"ForestGeometry.asset"))
            if(asset is Mesh existing && existing.name==mesh.name && existing!=meshContainer)
            { EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing; }
        AssetDatabase.AddObjectToAsset(mesh,meshContainer);return mesh;
    }
    static GameObject MeshObject(string name,Mesh mesh,Material mat,Vector3 pos,Vector3 scale,GameObject parent=null)
    {
        var go=Group(name,parent);go.transform.position=pos;go.transform.localScale=scale;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go;
    }
    static GameObject Primitive(string name,PrimitiveType type,Material mat,Vector3 pos,Vector3 scale,GameObject parent=null)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;SceneManager.MoveGameObjectToScene(go,scene);
        if(parent)go.transform.SetParent(parent.transform,false);go.transform.position=pos;go.transform.localScale=scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go;
    }
    static Mesh RockMesh(string name,int sides,int rings,float rough)
    {
        var v=new List<Vector3>();var t=new List<int>();
        for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++)
        {
            float a=i*2*Mathf.PI/sides,b=j*Mathf.PI/rings;
            var d=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));
            float n=Mathf.PerlinNoise(d.x*3+13,d.y*3+d.z*2+17);
            v.Add(d*(1+rough*(n-.5f)*2));
            if(j<rings&&i<sides){int k=j*(sides+1)+i;t.AddRange(new[]{k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1});}
        }
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return Store(mesh);
    }
    static void BarkTexture()
    {
        const int w=256,h=512;var tex=new Texture2D(w,h,TextureFormat.RGB24,false,true);var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float n=Mathf.PerlinNoise(x*.10f+10,y*.007f+10);
            float furrow=Mathf.Pow(Mathf.Abs(Mathf.Sin(x*.32f+n*4)),.35f);
            float fine=Mathf.PerlinNoise(x*.55f,y*.16f)*.16f;
            pixels[y*w+x]=Color.Lerp(new Color(.32f,.38f,.30f),new Color(.9f,.95f,.8f),furrow*.75f+fine);
        }
        tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(Root+"OldBark.png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(Root+"OldBark.png");var imp=(TextureImporter)AssetImporter.GetAtPath(Root+"OldBark.png");imp.sRGBTexture=false;imp.SaveAndReimport();
        bark.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"OldBark.png"));
    }
    static void Ground()
    {
        const int nx=70,nz=90; var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
        for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++)
        {
            float x=i-35,z=j-18;v.Add(new Vector3(x,Height(x,z)-.09f,z));uv.Add(new Vector2(i/(float)nx,j/(float)nz));
            if(i<nx&&j<nz){int k=j*(nx+1)+i;t.AddRange(new[]{k,k+nx+1,k+1,k+1,k+nx+1,k+nx+2});}
        }
        var mesh=new Mesh{name="Forest floor"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var mat=Mat("Forest floor",Color.white);
        const int n=512;var tex=new Texture2D(n,n,TextureFormat.RGB24,false,true);var pixels=new Color[n*n];
        for(int j=0;j<n;j++)for(int i=0;i<n;i++)
        {
            float x=i/(float)(n-1)*70-35,z=j/(float)(n-1)*90-18;
            float noise=Mathf.PerlinNoise(x*1.3f+100,z*1.3f+100)*.6f+Mathf.PerlinNoise(x*5+100,z*5+100)*.4f;
            float path=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,2.8f,Mathf.Abs(x-PathX(z))+(noise-.5f)*.85f));
            Color grass=Color.Lerp(new Color(.065f,.12f,.045f),new Color(.16f,.24f,.085f),noise);
            pixels[j*n+i]=Color.Lerp(grass,Color.Lerp(new Color(.16f,.145f,.09f),new Color(.28f,.25f,.16f),noise),path);
        }
        tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(Root+"ForestFloor.png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(Root+"ForestFloor.png");
        var imp=(TextureImporter)AssetImporter.GetAtPath(Root+"ForestFloor.png");imp.sRGBTexture=false;imp.wrapMode=TextureWrapMode.Clamp;imp.SaveAndReimport();
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"ForestFloor.png"));
        MeshObject("Uneven moss floor",Store(mesh),mat,Vector3.zero,Vector3.one);
    }
    static void Tube(List<Vector3> v,List<int> t,Vector3[] points,float start,float end)
    {
        const int sides=9;int offset=v.Count;
        for(int j=0;j<points.Length;j++)
        {
            var tangent=(points[Mathf.Min(j+1,points.Length-1)]-points[Mathf.Max(0,j-1)]).normalized;
            var right=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.95f?Vector3.forward:Vector3.up).normalized;
            var forward=Vector3.Cross(right,tangent);float radius=Mathf.Lerp(start,end,j/(float)(points.Length-1));
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides;float ridge=1+.13f*Mathf.Sin(i*2.6f+j*.7f);
                v.Add(points[j]+radius*ridge*(right*Mathf.Cos(a)+forward*Mathf.Sin(a)));
                if(j<points.Length-1){int k=offset+j*sides+i,k1=offset+j*sides+(i+1)%sides;
                    t.AddRange(new[]{k,k+sides,k1,k1,k+sides,k1+sides});}
            }
        }
    }
    static int treeIndex;
    static void Tree(Vector3 pos,float height,float radius,GameObject parent)
    {
        pos.y=Height(pos.x,pos.z);var v=new List<Vector3>();var t=new List<int>();
        Vector3 bend=new Vector3(R(-1.7f,1.7f),0,R(-1.2f,1.2f));
        var points=new Vector3[6];for(int j=0;j<6;j++){float f=j/5f;points[j]=Vector3.up*height*f+bend*f*f;}
        Tube(v,t,points,radius,.16f);
        for(int j=0;j<6;j++)
        {
            float a=j*Mathf.PI/3+R(-.3f,.3f);var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Tube(v,t,new[]{Vector3.up*radius,dir*radius*1.5f+Vector3.up*.25f,dir*radius*3.3f-Vector3.up*.08f},radius*.48f,.025f);
        }
        for(int j=0;j<5;j++)
        {
            float a=j*2.4f+R(-.5f,.5f);var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Vector3 origin=Vector3.up*height*R(.40f,.75f),end=origin+dir*R(3,6)+Vector3.up*R(1.5f,4);
            Tube(v,t,new[]{origin,origin+dir*2+Vector3.up*.6f,end},radius*.43f,.055f);
            var twigEnd=end+dir*1.5f+Vector3.up*1.3f;
            Tube(v,t,new[]{Vector3.Lerp(origin,end,.7f),end+Vector3.up*1.0f,twigEnd},radius*.18f,.018f);
            MeshObject("Leaf cluster",crown,leaf,pos+end+Vector3.up*1.5f,new Vector3(R(2.8f,4.3f),R(1.1f,2),R(2.2f,3.6f)),parent);
        }
        var mesh=new Mesh{name="Tree "+treeIndex++};mesh.SetVertices(v);mesh.SetTriangles(t,0);
        var barkUV=new List<Vector2>();foreach(var point in v)barkUV.Add(new Vector2(Mathf.Atan2(point.z,point.x)/6.283f,point.y*.17f));mesh.SetUVs(0,barkUV);mesh.RecalculateNormals();mesh.RecalculateBounds();
        MeshObject("Twisted trunk",Store(mesh),bark,pos,Vector3.one,parent);
        MeshObject("High crown",crown,leaf,pos+points[5],new Vector3(4,2,3.8f),parent);
    }
    static void Ferns(GameObject parent)
    {
        var v=new List<Vector3>();var t=new List<int>();
        for(int n=0;n<190;n++)
        {
            float x=R(-17,17),z=R(-9,40);if(Mathf.Abs(x-PathX(z))<2.3f)continue;
            Vector3 p=new Vector3(x,Height(x,z),z);float size=R(.5f,1.2f);
            for(int f=0;f<7;f++)
            {
                float a=f*6.283f/7+R(-.2f,.2f);var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(dir,Vector3.up);
                for(int j=1;j<8;j++)
                {
                    float h=j/8f;var c=p+dir*h*size+Vector3.up*(Mathf.Sin(h*2.2f)*.65f*size);
                    float w=(1-h)*size*.28f;
                    for(int s=-1;s<=1;s+=2)
                    {
                        int k=v.Count;v.Add(c-dir*.09f*size);v.Add(c+side*s*w+dir*.15f*size);v.Add(c+dir*.12f*size);v.Add(c+side*s*w*.48f+Vector3.up*.045f);
                        t.AddRange(new[]{k,k+1,k+3,k+3,k+1,k+2});
                    }
                }
            }
        }
        var mesh=new Mesh{name="Fern beds",indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        MeshObject("Fern beds",Store(mesh),fern,Vector3.zero,Vector3.one,parent);
    }
    static void Shrine()
    {
        var g=Group("Forgotten forest gate");float z=22;
        for(int j=0;j<3;j++)Primitive("Moss stair",PrimitiveType.Cube,stone,new Vector3(0,.12f+j*.18f,z-2+j*.8f),new Vector3(5.5f-j*.5f,.3f,2),g);
        for(int s=-1;s<=1;s+=2)
        {
            for(int j=0;j<5;j++)
            {
                var block=Primitive("Old gate block",PrimitiveType.Cube,stone,new Vector3(s*2.2f,.7f+j*.7f,z),new Vector3(.85f,.66f,1.05f),g);
                block.transform.rotation=Quaternion.Euler(0,R(-5,5),R(-2,2));
            }
            MeshObject("Gate moss",boulder,moss,new Vector3(s*2.2f,4.1f,z),new Vector3(.8f,.22f,.8f),g);
        }
        for(int j=0;j<5;j++)
        {
            float x=-1.8f+j*.9f;
            Primitive("Arch lintel",PrimitiveType.Cube,stone,new Vector3(x,4.2f+.45f*(1-Mathf.Abs(x)/2),z),new Vector3(.88f,.6f,1.1f),g);
        }
        Primitive("Shrine pedestal",PrimitiveType.Cylinder,stone,new Vector3(0,.9f,z+2),new Vector3(1.7f,.55f,1.7f),g);
        var monolith=MeshObject("Memory stone",boulder,stone,new Vector3(0,2.25f,z+2),new Vector3(.65f,1.25f,.38f),g);
        for(int j=0;j<4;j++)Primitive("Faint carved mark",PrimitiveType.Cube,rune,new Vector3((j%2==0?-.12f:.12f),2+j*.22f,z+1.61f),new Vector3(.12f,.06f,.015f),g);
    }
    static void Lantern(Vector3 p)
    {
        p.y=Height(p.x,p.z);var g=Group("Wayside lantern");
        Primitive("Foot",PrimitiveType.Cylinder,stone,p+Vector3.up*.2f,new Vector3(.65f,.2f,.65f),g);
        Primitive("Stem",PrimitiveType.Cylinder,stone,p+Vector3.up*.8f,new Vector3(.25f,.5f,.25f),g);
        Primitive("Light chamber",PrimitiveType.Cube,amber,p+Vector3.up*1.5f,new Vector3(.28f,.4f,.28f),g);
        for(int j=0;j<4;j++){float a=j*Mathf.PI/2+Mathf.PI/4;Primitive("Cage corner",PrimitiveType.Cube,bark,p+new Vector3(Mathf.Cos(a)*.22f,1.5f,Mathf.Sin(a)*.22f),new Vector3(.07f,.6f,.07f),g);}
        MeshObject("Moss roof",boulder,stone,p+Vector3.up*1.87f,new Vector3(.62f,.18f,.62f),g);
        var light=Group("Warm pool",g).AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.50f,.15f);light.intensity=5;light.range=5;light.transform.position=p+Vector3.up*1.6f;
    }
    static void Mushrooms(GameObject parent)
    {
        for(int j=0;j<18;j++)
        {
            float x=R(-8,8),z=R(-5,20);if(Mathf.Abs(x-PathX(z))<2.5f)continue;
            var p=new Vector3(x,Height(x,z),z);
            Primitive("Mushroom stalk",PrimitiveType.Cylinder,stone,p+Vector3.up*.13f,new Vector3(.055f,.13f,.055f),parent);
            MeshObject("Mushroom cap",boulder,rune,p+Vector3.up*.28f,new Vector3(.16f,.06f,.15f),parent);
        }
    }
    static Renderer Fog()
    {
        string path=Root+"Materials/Forest mist.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Custom/VolumetricFog"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetFloat("_ForestFog",1);mat.SetFloat("_SceneShadows",1);mat.SetFloat("_Density",.115f);mat.SetFloat("_Coverage",1);
        mat.SetFloat("_FogHeightFalloff",.27f);mat.SetFloat("_StepSize",.1f);mat.SetFloat("_NoiseSpeed",.012f);mat.SetFloat("_Erosion",.16f);mat.SetFloat("_DetailScale",3.1f);
        mat.SetFloat("_Anisotropy",.46f);mat.SetFloat("_SecondaryAnisotropy",-.18f);mat.SetFloat("_PhaseBlend",.28f);
        mat.SetFloat("_ViewSteps",48);mat.SetFloat("_SkySteps",1);mat.SetFloat("_LightSteps",3);mat.SetFloat("_LightExtinction",.8f);mat.SetFloat("_CloudBounce",.14f);
        mat.SetFloat("_AmbientStrength",.14f);mat.SetFloat("_SkyFillStrength",.46f);mat.SetColor("_SkyFillColor",new Color(.53f,.70f,.72f));
        mat.SetColor("_FogColor",new Color(.59f,.72f,.70f));mat.SetVector("_WindDirection",new Vector4(1,0,.3f,0));EditorUtility.SetDirty(mat);
        var go=Primitive("Forest volume - select material to tune fog",PrimitiveType.Cube,mat,new Vector3(0,9,24),new Vector3(76,24,100));
        var renderer=go.GetComponent<Renderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
    }
    static void PostProcess()
    {
        string path=Root+"Forest grading.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
        if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>();AssetDatabase.AddObjectToAsset(tone,profile);}tone.mode.Override(TonemappingMode.ACES);
        if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>();AssetDatabase.AddObjectToAsset(bloom,profile);}bloom.intensity.Override(.28f);bloom.threshold.Override(1.05f);bloom.scatter.Override(.6f);
        if(!profile.TryGet<Vignette>(out var vignette)){vignette=profile.Add<Vignette>();AssetDatabase.AddObjectToAsset(vignette,profile);}vignette.intensity.Override(.18f);vignette.smoothness.Override(.65f);
        EditorUtility.SetDirty(profile);var volume=Group("Forest colour and lantern bloom").AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=profile;
    }
    [MenuItem("Cloud Study/Capture Mist Forest Comparisons")]
    public static void CaptureSaved()
    {
        var previousScene=scene; scene=EditorSceneManager.OpenPreviewScene(ScenePath);
        try {
            var roots=scene.GetRootGameObjects();
            var camera=Array.Find(roots,o=>o.name=="Main Camera").GetComponent<Camera>();
            var fog=Array.Find(roots,o=>o.name.StartsWith("Forest volume")).GetComponent<Renderer>();
            camera.scene=scene; Capture(camera,fog);
        } finally {EditorSceneManager.ClosePreviewScene(scene);scene=previousScene;}
    }
    static void Capture(Camera camera,Renderer renderer)
    {
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var saved=renderer.sharedMaterial;var mat=new Material(saved);renderer.sharedMaterial=mat;
        ulong mask=camera.overrideSceneCullingMask;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
        try
        {
            mat.SetFloat("_NoiseSpeed",0);
            Render(camera,"01-forest-final");
            renderer.enabled=false;Render(camera,"02-fog-off");renderer.enabled=true;
            mat.SetFloat("_SceneShadows",0);Render(camera,"03-tree-shadows-off");mat.SetFloat("_SceneShadows",1);
            mat.SetFloat("_SkyFillStrength",0);Render(camera,"04-sky-fill-off");mat.SetFloat("_SkyFillStrength",saved.GetFloat("_SkyFillStrength"));
            mat.EnableKeyword("FOG_VALIDATE_IGNORE_DEPTH");Render(camera,"06-depth-clip-disabled");mat.DisableKeyword("FOG_VALIDATE_IGNORE_DEPTH");
            var position=camera.transform.position;var rotation=camera.transform.rotation;
            camera.transform.position=new Vector3(-2.0f,2.1f,5);camera.transform.rotation=Quaternion.LookRotation(new Vector3(.3f,2.1f,23)-camera.transform.position);
            Render(camera,"05-path-detail");camera.transform.SetPositionAndRotation(position,rotation);
            if(!mat.shader.isSupported||ShaderUtil.ShaderHasError(mat.shader))throw new Exception("Forest shader compile error");
            File.WriteAllText(Output+"verification.txt","Unity "+Application.unityVersion+"\n"+SystemInfo.graphicsDeviceName+"\n"+SystemInfo.graphicsDeviceType+"\n1280 x 720; same camera/light for 01-04 and 06; Noise Speed = 0; RenderSettings.fog = false\nShader supported and no compile errors.\nScene roots: "+scene.GetRootGameObjects().Length+" objects\n");
        }
        finally{renderer.enabled=true;renderer.sharedMaterial=saved;Object.DestroyImmediate(mat);camera.overrideSceneCullingMask=mask;ShaderUtil.allowAsyncCompilation=async;}
    }
    static void Render(Camera camera,string name)
    {
        var target=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var old=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Output+name+".png",texture.EncodeToPNG());}
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
    }
}
