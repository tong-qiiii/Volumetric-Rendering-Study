using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the saved showcase camera. The edit-mode composition stays unchanged.
public sealed class ForestExplorer : MonoBehaviour
{
    CharacterController motor;
    Transform avatar, leftLeg, rightLeg;
    Vector3 spawn;
    float yaw, pitch = 16f, verticalSpeed, gait;
    bool captured;
    Camera view;
    readonly System.Collections.Generic.List<Material> materials = new();

    void Start()
    {
        view = GetComponent<Camera>();
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
                go.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
        var player = new GameObject("Explorer - WASD to walk");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(player, gameObject.scene);
        player.layer = 2; // Excluded by camera obstruction queries, still collides with the world.
        spawn = new Vector3(.1f, 1.2f, -7.5f); player.transform.position = spawn;
        motor = player.AddComponent<CharacterController>();
        motor.height = 1.8f; motor.radius = .29f; motor.center = Vector3.up * .9f;
        motor.stepOffset = .32f; motor.slopeLimit = 48; motor.skinWidth = .035f;
        avatar = new GameObject("Explorer model").transform; avatar.SetParent(player.transform, false);
        var coat = Material(new Color(.22f,.36f,.26f));
        var boots = Material(new Color(.12f,.16f,.13f));
        var skin = Material(new Color(.66f,.47f,.29f));
        var pack = Material(new Color(.39f,.25f,.12f));
        Part("Coat", PrimitiveType.Capsule, new Vector3(0,1.05f,0), new Vector3(.65f,.43f,.42f), coat);
        Part("Hood", PrimitiveType.Sphere, new Vector3(0,1.62f,0), Vector3.one*.48f, coat);
        Part("Face", PrimitiveType.Sphere, new Vector3(0,1.62f,.16f),new Vector3(.30f,.33f,.16f),skin);
        Part("Backpack",PrimitiveType.Cube,new Vector3(0,1.12f,-.29f),new Vector3(.45f,.52f,.23f),pack);
        leftLeg=Part("Left boot",PrimitiveType.Capsule,new Vector3(-.17f,.37f,0),new Vector3(.22f,.32f,.24f),boots);
        rightLeg=Part("Right boot",PrimitiveType.Capsule,new Vector3(.17f,.37f,0),new Vector3(.22f,.32f,.24f),boots);
        Part("Left sleeve",PrimitiveType.Capsule,new Vector3(-.39f,1.04f,0),new Vector3(.18f,.3f,.21f),coat);
        Part("Right sleeve",PrimitiveType.Capsule,new Vector3(.39f,1.04f,0),new Vector3(.18f,.3f,.21f),coat);
        yaw = transform.eulerAngles.y;
    }
    Material Material(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color); materials.Add(mat); return mat;
    }
    Transform Part(string name,PrimitiveType kind,Vector3 pos,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(kind); go.name=name;go.layer=2;
        go.transform.SetParent(avatar,false);go.transform.localPosition=pos;go.transform.localScale=scale;
        Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
    }
    void Capture(bool value)
    {
        captured=value;Cursor.lockState=value?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!value;
    }
    void Update()
    {
        if (!motor || Keyboard.current == null || Mouse.current == null) return;
        var keys=Keyboard.current;var mouse=Mouse.current;
        if(keys.escapeKey.wasPressedThisFrame)Capture(false);
        else if(mouse.leftButton.wasPressedThisFrame && Application.isFocused)Capture(true);
        Vector2 input=Vector2.zero;
        if(captured)
        {
            var delta=mouse.delta.ReadValue();yaw+=delta.x*.12f;pitch=Mathf.Clamp(pitch-delta.y*.12f,-12,65);
            input=new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
            input=Vector2.ClampMagnitude(input,1);
            if(keys.rKey.wasPressedThisFrame)
            {motor.enabled=false;motor.transform.position=spawn;motor.enabled=true;verticalSpeed=0;yaw=0;pitch=16;}
        }
        Vector3 movement=Quaternion.Euler(0,yaw,0)*new Vector3(input.x,0,input.y);
        if(motor.isGrounded && verticalSpeed<0)verticalSpeed=-2;
        if(captured && motor.isGrounded && keys.spaceKey.wasPressedThisFrame)verticalSpeed=Mathf.Sqrt(2*18*1.1f);
        verticalSpeed-=18*Time.deltaTime;
        float speed=captured && keys.leftShiftKey.isPressed?5:2.5f;
        motor.Move((movement*speed+Vector3.up*verticalSpeed)*Time.deltaTime);
        if(movement.sqrMagnitude>.01f)avatar.rotation=Quaternion.Slerp(avatar.rotation,Quaternion.LookRotation(movement),12*Time.deltaTime);
        gait+=Time.deltaTime*speed*3;float swing=Mathf.Sin(gait)*25*input.magnitude;
        leftLeg.localRotation=Quaternion.Euler(swing,0,0);rightLeg.localRotation=Quaternion.Euler(-swing,0,0);
        if(motor.transform.position.y < -8){motor.enabled=false;motor.transform.position=spawn;motor.enabled=true;verticalSpeed=0;}
    }
    void LateUpdate()
    {
        if(!motor)return;
        Vector3 target=motor.transform.position+Vector3.up*1.45f;
        Quaternion rotation=Quaternion.Euler(pitch,yaw,0);
        Vector3 back=rotation*Vector3.back;float distance=4;
        if(Physics.SphereCast(target,.22f,back,out var hit,distance,~(1<<2),QueryTriggerInteraction.Ignore))distance=Mathf.Max(.3f,hit.distance-.1f);
        transform.SetPositionAndRotation(target+back*distance,rotation);
    }
    void OnGUI()
    {
        GUI.Box(new Rect(16,Screen.height-54,640,36),captured?
            "WASD  Move     Shift  Run     Space  Jump     Mouse  Look     R  Reset     Esc  Cursor":
            "Click Game view to explore   |   WASD Move   Shift Run   Space Jump   Esc Release cursor");
    }
    void OnApplicationFocus(bool focus){if(!focus)Capture(false);}
    void OnDisable(){Capture(false);}
    void OnDestroy(){if(motor)Destroy(motor.gameObject);foreach(var mat in materials)Destroy(mat);}
}
