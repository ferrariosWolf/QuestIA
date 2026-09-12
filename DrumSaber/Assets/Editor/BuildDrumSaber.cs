using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

namespace DrumSaber.Editor
{
    public static class BuildDrumSaber
    {
        public static void CreateSceneAndBuild()
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("Não foi possível ativar o alvo Android.");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/DrumSaber.unity");
            Directory.CreateDirectory("Builds");
            PlayerSettings.productName = "DrumSaber";
            PlayerSettings.companyName = "DrumSaber";
            PlayerSettings.applicationIdentifier = "com.drumsaber.prototype";
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.buildAppBundle = false;
            BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/DrumSaber.unity" }, "Builds/DrumSaber-v0.1.apk", BuildTarget.Android, BuildOptions.None);
        }
    }
}
