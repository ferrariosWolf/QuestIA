using UnityEngine;
namespace DrumSaber { public static class Bootstrap { [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Init(){ var go=new GameObject("DrumSaberRuntime"); go.AddComponent<DrumSaberRuntime>(); Object.DontDestroyOnLoad(go); } } }
