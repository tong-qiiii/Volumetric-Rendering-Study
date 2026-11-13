using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the saved showcase camera. The edit-mode composition stays unchanged.
public sealed class ForestExplorer : MonoBehaviour
{
    public Vector3 spawnPosition = new Vector3(.1f, 1.2f, -7.5f);
    public float respawnHeight = -8;
    public string volumeObjectPrefix = "Forest volume";
    CharacterController motor;

    Vector3 spawn;
    float yaw, pitch, verticalSpeed;
    bool captured;
    Renderer fogRenderer;
    Material originalFog, runtimeFog;
    int quality = 1;
    float frameSeconds;
    readonly string[] qualityNames = {"High", "Balanced", "Fast"};

    void Start()
    {
        // Use the baked floor and stone meshes for walking. Canopies and ferns are visual only.
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var go = filter.gameObject;
            if (go.name == "Twisted trunk")
            {
                var trunk = go.AddComponent<CapsuleCollider>();
                trunk.radius = .8f; trunk.height = 9; trunk.center = Vector3.up * 4.5f;
            }
            else if (go.name == "Uneven moss floor" || go.name == "Sunken stepping stone" ||
                go.name == "Mossy rock" || go.name == "Moss stair" || go.name == "Old gate block" ||
                go.name == "Memory stone" || go.name == "Shrine pedestal")
            {
                if(go.name == "Uneven moss floor") go.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                else {var box=go.AddComponent<BoxCollider>();box.center=filter.sharedMesh.bounds.center;box.size=filter.sharedMesh.bounds.size;}
            }
        }
        var player = new GameObject("Explorer - WASD to walk");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(player, gameObject.scene);
        player.layer = 2; // Excluded by camera obstruction queries, still collides with the world.
        spawn = spawnPosition; player.transform.position = spawn;
        motor = player.AddComponent<CharacterController>();
        motor.height = 1.8f; motor.radius = .29f; motor.center = Vector3.up * .9f;
        motor.stepOffset = .32f; motor.slopeLimit = 48; motor.skinWidth = .035f;
        yaw = transform.eulerAngles.y;
        foreach(var root in gameObject.scene.GetRootGameObjects())
            if(root.name.StartsWith(volumeObjectPrefix))fogRenderer=root.GetComponent<Renderer>();
        if(fogRenderer){originalFog=fogRenderer.sharedMaterial;runtimeFog=new Material(originalFog);fogRenderer.sharedMaterial=runtimeFog;ApplyQuality();}
    }
    void ApplyQuality()
    {
        if(!runtimeFog)return;
        runtimeFog.SetFloat("_ViewSteps",quality==0?128:quality==1?Mathf.Clamp(originalFog.GetFloat("_ViewSteps"),48,128):32);
        runtimeFog.SetFloat("_LightSteps",quality==0?6:quality==1?3:2);
        runtimeFog.SetFloat("_SkySteps",quality==0?3:1);
    }
    void Capture(bool value)
    {
        captured=value;Cursor.lockState=value?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!value;
    }
    void Update()
    {
        if (!motor || Keyboard.current == null || Mouse.current == null) return;
        frameSeconds=Mathf.Lerp(frameSeconds,Time.unscaledDeltaTime,.05f);
        var keys=Keyboard.current;var mouse=Mouse.current;
        if(keys.f2Key.wasPressedThisFrame){quality=(quality+1)%3;ApplyQuality();}
        if(keys.escapeKey.wasPressedThisFrame)Capture(false);
        else if(mouse.leftButton.wasPressedThisFrame && Application.isFocused)Capture(true);
        Vector2 input=Vector2.zero;
        if(captured)
        {
            var delta=mouse.delta.ReadValue();yaw+=delta.x*.12f;pitch=Mathf.Clamp(pitch-delta.y*.12f,-85,85);
            input=new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
            input=Vector2.ClampMagnitude(input,1);
            if(keys.rKey.wasPressedThisFrame)
            {motor.enabled=false;motor.transform.position=spawn;motor.enabled=true;verticalSpeed=0;yaw=0;pitch=0;}
        }
        Vector3 movement=Quaternion.Euler(0,yaw,0)*new Vector3(input.x,0,input.y);
        if(motor.isGrounded && verticalSpeed<0)verticalSpeed=-2;
        if(captured && motor.isGrounded && keys.spaceKey.wasPressedThisFrame)verticalSpeed=Mathf.Sqrt(2*18*1.1f);
        verticalSpeed-=18*Time.deltaTime;
        float speed=captured && keys.leftShiftKey.isPressed?5:2.5f;
        motor.Move((movement*speed+Vector3.up*verticalSpeed)*Time.deltaTime);
        if(motor.transform.position.y < respawnHeight){motor.enabled=false;motor.transform.position=spawn;motor.enabled=true;verticalSpeed=0;}
    }
    void LateUpdate()
    {
        if(!motor)return;
        transform.SetPositionAndRotation(motor.transform.position+Vector3.up*1.65f, Quaternion.Euler(pitch,yaw,0));
    }
    void OnGUI()
    {
        GUI.Label(new Rect(16,16,420,25),$"F2 Quality: {qualityNames[quality]}   Frame: {frameSeconds*1000:F1} ms");
        GUI.Box(new Rect(16,Screen.height-54,640,36),captured?
            "WASD  Move     Shift  Run     Space  Jump     Mouse  Look     R  Reset     Esc  Cursor":
            "Click Game view to explore   |   WASD Move   Shift Run   Space Jump   Esc Release cursor");
    }
    void OnApplicationFocus(bool focus){if(!focus)Capture(false);}
    void OnDisable(){Capture(false);}
    void OnDestroy(){if(motor)Destroy(motor.gameObject);if(fogRenderer && originalFog)fogRenderer.sharedMaterial=originalFog;if(runtimeFog)Destroy(runtimeFog);}
}
