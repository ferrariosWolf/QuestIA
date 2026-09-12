using System;
using System.IO;
using System.Linq;
using Meta.XR;
using QuestAI.Core;
using QuestAI.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.OpenXR;

public static class QuestAIV03Setup
{
    private const string ScenePath = "Assets/Scenes/QuestAI_Main.unity";
    private const string XriVersion = "3.0.9";

    public static void Configure()
    {
        try
        {
            ImportStarterAssets();
            ConfigurePlayer();
            CreateInteractiveScene();
            ValidateProject();
            Debug.Log("QUESTAI_V03_READY");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    private static void ImportStarterAssets()
    {
        var sample = Sample.FindByPackage("com.unity.xr.interaction.toolkit", XriVersion)
            .FirstOrDefault(candidate => candidate.displayName == "Starter Assets");
        if (string.IsNullOrEmpty(sample.displayName))
            throw new InvalidOperationException("Starter Assets do XR Interaction Toolkit não encontrado.");
        if (!sample.isImported && !sample.Import(Sample.ImportOptions.OverridePreviousImports))
            throw new InvalidOperationException("Falha ao importar Starter Assets oficial do XRI.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "Quest AI";
        PlayerSettings.productName = "Quest AI Assistant";
        PlayerSettings.bundleVersion = "0.5.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.questai.assistant");
        PlayerSettings.Android.bundleVersionCode = 5;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
        AssetDatabase.SaveAssets();
    }

    private static void CreateInteractiveScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.27f, 0.34f);

        var rigPrefabPath = $"Assets/Samples/XR Interaction Toolkit/{XriVersion}/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rigPrefabPath);
        if (rigPrefab == null) throw new FileNotFoundException("Prefab oficial XR Origin não encontrado após importação.", rigPrefabPath);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
        rig.name = "XR Origin - Touch Controllers";

        var eventSystemObject = new GameObject("XR Event System");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<XRUIInputModule>();

        var stateObject = new GameObject("Assistant State");
        var stateController = stateObject.AddComponent<AssistantStateController>();

        var canvasObject = new GameObject("Quest AI Interactive Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.position = new Vector3(0f, 1.42f, 1.75f);
        // O Canvas fica à frente da câmera XR (+Z). A frente de um World Space
        // Canvas já aponta para a câmera com rotação identidade; Y=180 o fazia
        // aparecer pelo verso/espelhado no Quest.
        canvasRect.rotation = Quaternion.identity;
        canvasRect.sizeDelta = new Vector2(900f, 600f);
        canvasRect.localScale = Vector3.one * 0.0018f;

        var background = canvasObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.08f, 0.13f, 0.98f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var heading = CreateText(canvasRect, "Heading", "QUEST AI", font, 58, new Vector2(0f, 220f), FontStyle.Bold, Color.white);
        var mode = CreateText(canvasRect, "Mode", "Assistente XR", font, 38, new Vector2(0f, 135f), FontStyle.Normal, new Color(0.72f, 0.9f, 1f));
        var version = CreateText(canvasRect, "Version", "Versão 0.5", font, 28, new Vector2(0f, 78f), FontStyle.Normal, Color.white);
        var state = CreateText(canvasRect, "State", "INATIVO", font, 30, new Vector2(0f, 12f), FontStyle.Bold, new Color(0.72f, 0.78f, 0.84f));
        var detail = CreateText(canvasRect, "Detail", "Pronto para iniciar", font, 24, new Vector2(0f, -52f), FontStyle.Normal, new Color(0.78f, 0.84f, 0.9f));

        var activate = CreateButton(canvasRect, "Activate Button", "ATIVAR ASSISTENTE", font, new Vector2(0f, -160f), new Color(0.05f, 0.48f, 0.68f));
        var deactivate = CreateButton(canvasRect, "Deactivate Button", "DESATIVAR", font, new Vector2(0f, -160f), new Color(0.55f, 0.15f, 0.18f));

        var ui = canvasObject.AddComponent<QuestAssistantUI>();
        ui.Configure(stateController, heading, mode, version, state, detail, activate, deactivate);
        deactivate.gameObject.SetActive(false);

        var lightObject = new GameObject("Soft Key Light");
        lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.05f;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Comfort Floor";
        floor.transform.localScale = new Vector3(2f, 1f, 2f);
        floor.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("FloorMaterial", new Color(0.07f, 0.11f, 0.17f));

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    private static Text CreateText(RectTransform parent, string name, string value, Font font, int fontSize, Vector2 position, FontStyle style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(800f, 70f);
        rect.anchoredPosition = position;
        var text = go.GetComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(RectTransform parent, string name, string label, Font font, Vector2 position, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(570f, 105f);
        rect.anchoredPosition = position;
        var image = go.GetComponent<Image>();
        image.color = color;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.22f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var text = labelObject.GetComponent<Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 31;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        var path = $"Assets/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        return material;
    }

    private static void ValidateProject()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Android não está ativo.");
        if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            throw new InvalidOperationException("Arquitetura não está configurada exclusivamente como ARM64.");
        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            throw new InvalidOperationException("IL2CPP não está ativo.");
        if (!EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == ScenePath))
            throw new InvalidOperationException("QuestAI_Main não está incluída no build.");
        var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var metaFeature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, MetaXRFeature.featureId);
        if (openXR == null || metaFeature == null || !metaFeature.enabled)
            throw new InvalidOperationException("OpenXR/Meta XR Feature não está configurado para Android.");
        Debug.Log("QUESTAI_V03_AUDIT Android=active ARM64=true IL2CPP=true OpenXR=true MetaXR=true Scene=true Version=0.3.0 VersionCode=3");
    }
}
