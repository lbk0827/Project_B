using UnityEngine;
using Game.Core;

namespace Game.Data
{
    /// <summary>
    /// 레벨 로더 - JSON 파일에서 레벨 데이터 로드
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("설정")]
        [SerializeField] private string _levelPath = "Levels/Level_";
        [SerializeField] private int _startLevel = 1;

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 특정 레벨 로드
        /// </summary>
        public LevelData LoadLevel(int levelId)
        {
            string path = _levelPath + levelId.ToString("D3");
            TextAsset jsonFile = Resources.Load<TextAsset>(path);

            if (jsonFile == null)
            {
                Debug.LogError($"[LevelLoader] Level file not found: {path}");
                return null;
            }

            try
            {
                LevelData levelData = JsonUtility.FromJson<LevelData>(jsonFile.text);
                return levelData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LevelLoader] Failed to parse level: {path}\n{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 레벨 로드 및 게임 시작
        /// StageTable.json 기반으로 로드
        /// </summary>
        public void LoadAndStartLevel(int levelId)
        {
            // GameManager.LoadLevel(int)를 호출하여 StageTable.json에서 로드
            GameManager.Instance.LoadLevel(levelId);
        }

        // ========== 유니티 라이프사이클 ==========
        private void Start()
        {
            // 로비에서 선택된 레벨 또는 기본 시작 레벨 로드
            int selectedLevel = PlayerPrefs.GetInt("SelectedLevel", _startLevel);
            Debug.Log($"[LevelLoader] Starting level: {selectedLevel}");
            LoadAndStartLevel(selectedLevel);
        }
    }
}