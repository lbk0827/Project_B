using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Game.Data
{
    /// <summary>
    /// BoosterTable JSON 로더
    /// Resources/Tables/BoosterTable.json 파일 로드
    /// JSON 형식: 배열 [...] (records 래퍼 없음)
    /// </summary>
    public static class BoosterTableLoader
    {
        private const string TABLE_PATH = "Tables/BoosterTable";

        private static List<BoosterTableRecord> _cachedRecords;

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 Play Mode 진입 시 캐시 자동 클리어
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnPlayModeEnter()
        {
            _cachedRecords = null;
            Debug.Log("[BoosterTableLoader] Cache cleared on Play Mode enter");
        }
#endif

        /// <summary>
        /// BoosterTable 로드 (캐싱)
        /// </summary>
        public static List<BoosterTableRecord> Load()
        {
            if (_cachedRecords != null)
                return _cachedRecords;

            var textAsset = Resources.Load<TextAsset>(TABLE_PATH);
            if (textAsset == null)
            {
                Debug.LogError($"[BoosterTableLoader] Failed to load: Resources/{TABLE_PATH}.json");
                return null;
            }

            _cachedRecords = JsonConvert.DeserializeObject<List<BoosterTableRecord>>(textAsset.text);

            if (_cachedRecords == null)
            {
                Debug.LogError("[BoosterTableLoader] Failed to parse BoosterTable JSON");
                return null;
            }

            Debug.Log($"[BoosterTableLoader] Loaded {_cachedRecords.Count} boosters");
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
        /// BoosterId로 레코드 가져오기
        /// </summary>
        public static BoosterTableRecord GetRecord(string boosterId)
        {
            var records = Load();
            if (records == null) return null;

            foreach (var record in records)
            {
                if (record.BoosterId == boosterId)
                    return record;
            }
            return null;
        }
    }
}
