using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// 씬 전환 유틸리티
    /// </summary>
    public static class SceneLoader
    {
        // ========== 씬 이름 상수 ==========
        public const string LOBBY_SCENE = "LobbyScene";
        public const string GAME_SCENE = "GameScene";

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 로비 씬으로 이동
        /// </summary>
        public static void LoadLobby()
        {
            Debug.Log("[SceneLoader] Loading Lobby Scene...");
            SceneManager.LoadScene(LOBBY_SCENE);
        }

        /// <summary>
        /// 게임 씬으로 이동
        /// </summary>
        public static void LoadGame()
        {
            Debug.Log("[SceneLoader] Loading Game Scene...");
            SceneManager.LoadScene(GAME_SCENE);
        }

        /// <summary>
        /// 현재 씬 다시 로드
        /// </summary>
        public static void ReloadCurrentScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            Debug.Log($"[SceneLoader] Reloading scene: {currentScene}");
            SceneManager.LoadScene(currentScene);
        }

        /// <summary>
        /// 특정 씬으로 이동
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            Debug.Log($"[SceneLoader] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// 현재 씬 이름 반환
        /// </summary>
        public static string GetCurrentSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

        /// <summary>
        /// 로비 씬인지 확인
        /// </summary>
        public static bool IsLobbyScene()
        {
            return GetCurrentSceneName() == LOBBY_SCENE;
        }

        /// <summary>
        /// 게임 씬인지 확인
        /// </summary>
        public static bool IsGameScene()
        {
            return GetCurrentSceneName() == GAME_SCENE;
        }
    }
}
