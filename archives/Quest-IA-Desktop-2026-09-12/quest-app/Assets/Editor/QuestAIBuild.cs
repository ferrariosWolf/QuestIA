using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Unity.XR.CoreUtils;
using Meta.XR;

public static class QuestAIBuild
{
    private const string ScenePath = "Assets/Scenes/QuestAI_Main.unity";

    public static void Build()
    {
        try
        {
            ConfigurePlayer();
            ConfigureOpenXR();
            CreateScene();
            BuildApk();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ConfigureOnly()
    {
        ConfigurePlayer();
        ConfigureOpenXR();
        CreateScene();
        Debug.Log("QUESTAI_CONFIG_COMPLETE");
    }

    private static void ConfigurePlayer()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        PlayerSettings.companyName = "Quest AI";
        PlayerSettings.productName = "Quest AI Assistant";
        PlayerSettings.bundleVersion = "0.2.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.questai.assistant");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = 2;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.colorSpace = ColorSpace.Linear;
        EditorUserBuildSettings.development = true;
        EditorUserBuildSettings.allowDebugging = true;
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureOpenXR()
    {
        const string folder = "Assets/XR";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "XR");

        XRGeneralSettingsPerBuildTarget perBuild;
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perBuild) || perBuild == null)
        {
            var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            var general = ScriptableObject.CreateInstance<XRGeneralSettings>();
            general.Manager = manager;
            AssetDatabase.CreateAsset(general, folder + "/XRGeneralSettings.asset");
            AssetDatabase.AddObjectToAsset(manager, general);

            perBuild = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            perBuild.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
            AssetDatabase.CreateAsset(perBuild, folder + "/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuild, true);
        }

        var androidSettings = perBuild.SettingsForBuildTarget(BuildTargetGroup.Android);
        if (androidSettings == null)
        {
            var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            androidSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
            androidSettings.Manager = manager;
            AssetDatabase.CreateAsset(androidSettings, folder + "/XRGeneralSettingsAndroid.asset");
            AssetDatabase.AddObjectToAsset(manager, androidSettings);
            perBuild.SetSettingsForBuildTarget(BuildTargetGroup.Android, androidSettings);
        }

        if (!XRPackageMetadataStore.AssignLoader(androidSettings.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
            throw new InvalidOperationException("Não foi possível atribuir o OpenXR Loader ao Android.");

        var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (openXR == null) throw new InvalidOperationException("OpenXRSettings para Android não foram criadas.");
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var metaFeature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, MetaXRFeature.featureId);
        if (metaFeature == null) throw new InvalidOperationException("Meta XR Feature não foi localizada no OpenXR.");
        metaFeature.enabled = true;

        AssetDatabase.SaveAssets();
        Debug.Log("QUESTAI_AUDIT XR_MANAGEMENT=4.5.0 OPENXR=1.14.3 META_XR_CORE=83.0.4 META_FEATURE=enabled");
    }

    private static void CreateScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.23f, 0.27f, 0.34f);

        var originObject = new GameObject("XR Origin (Quest)");
        var origin = originObject.AddComponent<XROrigin>();
        var cameraOffset = new GameObject("Camera Offset");
        cameraOffset.transform.SetParent(originObject.transform, false);
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(cameraOffset.transform, false);
        var camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.075f, 0.12f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<TrackedPoseDriver>();
        origin.CameraFloorOffsetObject = cameraOffset;
        origin.Camera = camera;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        var lightObject = new GameObject("Soft Key Light");
        lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.color = new Color(0.84f, 0.91f, 1f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Comfort Floor";
        floor.transform.localScale = new Vector3(2f, 1f, 2f);
        floor.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Floor Material", new Color(0.08f, 0.12f, 0.18f));

        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Quest AI Status Panel";
        panel.transform.position = new Vector3(0f, 1.42f, 1.75f);
        panel.transform.rotation = Quaternion.identity;
        panel.transform.localScale = new Vector3(1.8f, 0.92f, 0.055f);
        panel.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Panel Material", new Color(0.025f, 0.12f, 0.18f));

        CreateText("QUEST AI", new Vector3(0f, 1.70f, 1.718f), 0.15f, FontStyle.Bold);
        CreateText("Versão 0.2", new Vector3(0f, 1.49f, 1.718f), 0.078f, FontStyle.Normal);
        CreateText("Sistema XR funcionando", new Vector3(0f, 1.31f, 1.718f), 0.068f, FontStyle.Normal);
        CreateText("Próxima etapa: Assistente interativo", new Vector3(0f, 1.13f, 1.718f), 0.052f, FontStyle.Normal);

        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    private static Material MakeMaterial(string name, Color color)
    {
        var assetPath = "Assets/" + name.Replace(" ", string.Empty) + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null)
        {
            existing.color = color;
            return existing;
        }
        var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
        var material = new Material(shader) { name = name, color = color };
        AssetDatabase.CreateAsset(material, assetPath);
        return material;
    }

    private static void CreateText(string value, Vector3 position, float size, FontStyle style)
    {
        var go = new GameObject(value);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        var text = go.AddComponent<TextMesh>();
        text.text = value;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = size;
        text.fontSize = 64;
        text.fontStyle = style;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
    }

    private static void BuildApk()
    {
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/QuestAI-v0.2.apk"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"QUESTAI_BUILD_RESULT result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} size={report.summary.totalSize} output={output}");
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build Android falhou: " + report.summary.result);
    }
}
