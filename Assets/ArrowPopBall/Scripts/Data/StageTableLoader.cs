using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Game.Data
{
    /// <summary>
    /// StageTable JSON 로더
    /// Resources/Tables/StageTable.json 파일 로드
    /// JSON 형식: 배열 [...] (records 래퍼 없음)
    /// </summary>
    public static class StageTableLoader
    {
        private const string TABLE_PATH = "Tables/StageTable";

        private static List<StageTableRecord> _cachedRecords;

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 Play Mode 진입 시 캐시 자동 클리어
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnPlayModeEnter()
        {
            _cachedRecords = null;
            Debug.Log("[StageTableLoader] Cache cleared on Play Mode enter");
        }
#endif

        /// <summary>
        /// StageTable 로드 (캐싱)
        /// </summary>
        public static List<StageTableRecord> Load()
        {
            if (_cachedRecords != null)
                return _cachedRecords;

            var textAsset = Resources.Load<TextAsset>(TABLE_PATH);
            if (textAsset == null)
            {
                Debug.LogError($"[StageTableLoader] Failed to load: Resources/{TABLE_PATH}.json");
                return null;
            }

            // JSON 배열 직접 파싱
            _cachedRecords = JsonConvert.DeserializeObject<List<StageTableRecord>>(textAsset.text);

            if (_cachedRecords == null)
            {
                Debug.LogError("[StageTableLoader] Failed to parse StageTable JSON");
                return null;
            }

            Debug.Log($"[StageTableLoader] Loaded {_cachedRecords.Count} levels");
            return _cachedRecords;
        }

        /// <summary>
        /// 캐시 클리어 (에디터에서 JSON 수정 후 리로드용)
        /// </summary>
        public static void ClearCache()
        {
            _cachedRecords = null;
        }

        /// <summary>
        /// 레벨 번호로 레코드 가져오기
        /// </summary>
        public static StageTableRecord GetRecord(int level)
        {
            var records = Load();
            if (records == null) return null;

            foreach (var record in records)
            {
                if (record.Level == level)
                    return record;
            }
            return null;
        }

        /// <summary>
        /// 레벨 번호로 StageData 로드
        /// </summary>
        public static StageData LoadStageData(int level)
        {
            var record = GetRecord(level);
            if (record == null)
            {
                Debug.LogWarning($"[StageTableLoader] Level {level} not found in StageTable");
                return null;
            }

            string path = record.GetStageAssetPath();
            var stageData = Resources.Load<StageData>(path);

            if (stageData == null)
            {
                Debug.LogWarning($"[StageTableLoader] StageData not found: Resources/{path}");
            }

            return stageData;
        }

        /// <summary>
        /// 레벨 번호로 LevelData 생성 (기존 시스템 호환)
        /// </summary>
        public static LevelData LoadLevelData(int level)
        {
            var record = GetRecord(level);
            if (record == null)
            {
                Debug.LogError($"[StageTableLoader] LoadLevelData failed: Level {level} not found in StageTable.json");
                return null;
            }

            var stageData = LoadStageData(level);
            if (stageData == null)
            {
                Debug.LogError($"[StageTableLoader] LoadLevelData failed: StageData not found for Level {level} (Stage_{record.StageIdx:D3})");
                return null;
            }

            Debug.Log($"[StageTableLoader] LoadLevelData: Level {level} → Stage_{record.StageIdx:D3}, Lives={record.Lives}");

            // StageData + StageTableRecord → LevelData 변환
            return stageData.ToLevelData(
                level,
                record.Lives,
                new int[] { 3, 2, 1 }  // 기본 별점 기준
            );
        }

        /// <summary>
        /// 전체 레벨 수
        /// </summary>
        public static int GetLevelCount()
        {
            var records = Load();
            return records?.Count ?? 0;
        }

        /// <summary>
        /// 최대 레벨 번호
        /// </summary>
        public static int GetMaxLevel()
        {
            var records = Load();
            if (records == null || records.Count == 0) return 0;

            int max = 0;
            foreach (var record in records)
            {
                if (record.Level > max)
                    max = record.Level;
            }
            return max;
        }
    }
}