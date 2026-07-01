using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace Game.Editor
{
    /// <summary>
    /// Build Settings 씬 설정 유틸리티
    /// </summary>
    public static class BuildSettingsSetup
    {
        [MenuItem("Tools/Arrow Pop/Setup Build Scenes")]
        public static void SetupBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                // LobbyScene이 첫 번째 (시작 씬)
                new EditorBuildSettingsScene("Assets/Scenes/LobbyScene.unity", true),
                // GameScene이 두 번째
                new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true)
            };

            EditorBuildSettings.scenes = scenes.ToArray();

            UnityEngine.Debug.Log("[BuildSettingsSetup] Build scenes configured:");
            UnityEngine.Debug.Log("  0: LobbyScene (Start Scene)");
            UnityEngine.Debug.Log("  1: GameScene");
        }
    }
}
