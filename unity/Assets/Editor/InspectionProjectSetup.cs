using System.IO;
using Inspection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class InspectionProjectSetup
{
    [MenuItem("Inspection/Rebuild demo scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Materials");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.65f,.7f,.72f);
        var floor = Material("Floor", new Color(.34f,.39f,.4f));
        var wall = Material("Wall", new Color(.72f,.77f,.73f));
        var cabinet = Material("Cabinet", new Color(.12f,.23f,.28f));
        var detail = Material("Panel", new Color(.24f,.37f,.4f));
        var pipe = Material("Pipe", new Color(.7f,.46f,.16f));
        Cube("Ground",new Vector3(0,-.15f,0),new Vector3(14,.3f,12),floor);
        Cube("Back wall",new Vector3(0,2,6),new Vector3(14,4,.3f),wall);
        Cube("Left wall",new Vector3(-7,2,0),new Vector3(.3f,4,12),wall);
        Cube("Right wall",new Vector3(7,2,0),new Vector3(.3f,4,12),wall);
        Cube("Front wall",new Vector3(0,2,-6),new Vector3(14,4,.3f),wall);
        for (int index=0; index<2; index++)
        {
            float x = index==0 ? -2.6f : 1f;
            Cube("Cabinet "+(index+1),new Vector3(x,1.3f,2.8f),new Vector3(1.8f,2.6f,1.1f),cabinet);
            Cube("Door "+(index+1),new Vector3(x,1.3f,2.22f),new Vector3(1.55f,2.3f,.08f),detail);
            for (int slot=0; slot<5; slot++) Cube("Vent",new Vector3(x,1.1f+slot*.22f,2.16f),new Vector3(1.2f,.055f,.035f),cabinet);
            Sign("CABINET 0"+(index+1),new Vector3(x-.67f,2.35f,2.12f),.11f);
        }
        Cube("Pump unit",new Vector3(4.3f,.6f,3.7f),new Vector3(1.6f,1.2f,1.5f),detail);
        var pipeObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipeObj.name = "Overhead pipeline"; pipeObj.layer = 8;
        pipeObj.transform.position = new Vector3(0,3.15f,4.5f);
        pipeObj.transform.rotation = Quaternion.Euler(0,0,90);
        pipeObj.transform.localScale = new Vector3(.25f,5.6f,.25f);
        pipeObj.GetComponent<Renderer>().sharedMaterial = pipe;
        Cube("Walkway",new Vector3(0,.006f,-1.2f),new Vector3(2.1f,.01f,5.5f),Material("Walkway",new Color(.55f,.58f,.32f)));
        Sign("INSPECTION / EQUIPMENT ROOM",new Vector3(-3.6f,3.7f,5.78f),.17f);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.1f; sun.transform.rotation = Quaternion.Euler(50,-30,0);
        sun.shadows = LightShadows.Soft;
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0,.03f,-4.3f);
        var character = player.AddComponent<CharacterController>();
        character.height = 1.8f; character.radius = .3f; character.center = new Vector3(0,.9f,0); character.stepOffset = .25f;
        var cameraObject = new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
        cameraObject.tag = "MainCamera"; cameraObject.transform.SetParent(player.transform);
        cameraObject.transform.localPosition = new Vector3(0,1.65f,0);
        var camera = cameraObject.GetComponent<Camera>();
        camera.fieldOfView = 65; camera.nearClipPlane = .05f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.45f,.58f,.65f);
        var system = new GameObject("Inspection System");
        var api = system.AddComponent<InspectionApi>();
        var controller = system.AddComponent<InspectionController>(); controller.api = api; controller.viewCamera = camera;
        var movement = player.AddComponent<PlayerMovement>(); movement.viewCamera = camera; movement.inspection = controller;

        PlayerSettings.companyName = "InspectionPractice";
        PlayerSettings.productName = "Virtual Inspection";
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/InspectionSite.unity");
        EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene("Assets/Scenes/InspectionSite.unity",true)};
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("INSPECTION_SCENE_READY");
    }

    [MenuItem("Inspection/Build Windows demo")]
    public static void BuildWindows()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] {"Assets/Scenes/InspectionSite.unity"},
            locationPathName = "Builds/Windows/Inspection.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("Unity build failed: " + report.summary.result);
        Debug.Log("INSPECTION_BUILD_READY");
    }

    static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/"+name+".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material==null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material,path); }
        material.color = color; return material;
    }
    static void Cube(string name,Vector3 position,Vector3 scale,Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name; cube.layer = 8;
        cube.transform.position = position; cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
    }
    static void Sign(string caption,Vector3 position,float scale)
    {
        var sign = new GameObject(caption).AddComponent<TextMesh>();
        sign.text = caption; sign.fontSize = 48; sign.characterSize = scale; sign.color = new Color(.85f,.93f,.87f);
        sign.transform.position = position;
    }
}
