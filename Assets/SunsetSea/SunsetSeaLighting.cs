using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SunsetSeaLighting : MonoBehaviour
{
    public Light sun;
    public Renderer clouds;
    public Material sky;
    Material cloudInstance, skyInstance;
    int phase = 1;
    public void SetPhase(int index)
    {
        phase = Mathf.Clamp(index, 0, 2);
        float[] elevation = {20, 4, -6};
        Color[] sunlight = {new Color(1,.87f,.67f), new Color(1,.45f,.20f), new Color(.53f,.45f,.72f)};
        float[] energy = {.65f,.55f,.08f};
        sun.transform.rotation = Quaternion.Euler(elevation[phase], -165, 0);
        sun.color = sunlight[phase]; sun.intensity = energy[phase];
        var material = clouds.sharedMaterial;
        material.SetColor("_SkyFillColor", phase==2 ? new Color(.32f,.38f,.69f) : new Color(.55f,.66f,.9f));
        material.SetFloat("_SkyFillStrength", phase==2 ? .24f : .38f);
        sky.SetVector("_SunDirection", -sun.transform.forward);
        sky.SetColor("_Horizon", phase==0 ? new Color(.72f,.65f,.48f) : phase==1 ? new Color(.72f,.38f,.22f) : new Color(.22f,.16f,.30f));
        sky.SetColor("_Zenith", phase==0 ? new Color(.12f,.32f,.57f) : phase==1 ? new Color(.16f,.18f,.38f) : new Color(.025f,.035f,.10f));
        sky.SetColor("_SunColor", phase==2 ? Color.black : new Color(2.5f,1.3f,.55f));
        RenderSettings.ambientLight = phase==2 ? new Color(.08f,.10f,.19f) : new Color(.22f,.25f,.32f);
    }
    void Start()
    {
        cloudInstance = new Material(clouds.sharedMaterial); clouds.sharedMaterial = cloudInstance;
        skyInstance = new Material(sky); sky=skyInstance; RenderSettings.skybox=sky;
        SetPhase(1);
    }
    void Update()
    {
        var keys=Keyboard.current;if(keys==null)return;
        if(keys.digit1Key.wasPressedThisFrame)SetPhase(0);
        if(keys.digit2Key.wasPressedThisFrame)SetPhase(1);
        if(keys.digit3Key.wasPressedThisFrame)SetPhase(2);
    }
    void OnGUI(){GUI.Label(new Rect(16,45,650,26),"1 Before sunset    2 Sunset    3 After sunset");}
    void OnDestroy(){if(cloudInstance)Destroy(cloudInstance);if(skyInstance)Destroy(skyInstance);}
}
