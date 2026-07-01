using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 범용 부스터 아이템 데이터 관리
    /// PlayerPrefs 기반 저장/로드, BoosterTable에서 초기값 참조
    /// </summary>
    public static class BoosterData
    {
        // ========== 상수 ==========
        private const string KEY_PREFIX = "Booster_";
        private const int FALLBACK_INITIAL_COUNT = 0;

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 현재 보유 개수 조회
        /// </summary>
        public static int GetCount(string boosterId)
        {
            string key = KEY_PREFIX + boosterId;
            if (!PlayerPrefs.HasKey(key))
            {
                int initialCount = GetInitialCount(boosterId);
                PlayerPrefs.SetInt(key, initialCount);
                PlayerPrefs.Save();
            }
            return PlayerPrefs.GetInt(key);
        }

        /// <summary>
        /// 아이템 1개 사용
        /// </summary>
        /// <returns>사용 성공 여부</returns>
        public static bool UseItem(string boosterId)
        {
            int count = GetCount(boosterId);
            if (count <= 0)
            {
                Debug.Log($"[BoosterData] {boosterId}: No items available");
                return false;
            }

            string key = KEY_PREFIX + boosterId;
            PlayerPrefs.SetInt(key, count - 1);
            PlayerPrefs.Save();
            Debug.Log($"[BoosterData] {boosterId}: Used 1 item, remaining: {count - 1}");
            return true;
        }

        /// <summary>
        /// 아이템 추가
        /// </summary>
        public static void AddItem(string boosterId, int amount)
        {
            if (amount <= 0)
                return;

            int newCount = GetCount(boosterId) + amount;
            string key = KEY_PREFIX + boosterId;
            PlayerPrefs.SetInt(key, newCount);
            PlayerPrefs.Save();
            Debug.Log($"[BoosterData] {boosterId}: Added {amount} items, total: {newCount}");
        }

        /// <summary>
        /// 아이템 개수 직접 설정 (테스트/치트용)
        /// </summary>
        public static void SetCount(string boosterId, int count)
        {
            string key = KEY_PREFIX + boosterId;
            PlayerPrefs.SetInt(key, Mathf.Max(0, count));
            PlayerPrefs.Save();
            Debug.Log($"[BoosterData] {boosterId}: Set count to: {count}");
        }

        /// <summary>
        /// 특정 부스터 데이터 초기화 (리셋)
        /// </summary>
        public static void Reset(string boosterId)
        {
            int initialCount = GetInitialCount(boosterId);
            string key = KEY_PREFIX + boosterId;
            PlayerPrefs.SetInt(key, initialCount);
            PlayerPrefs.Save();
            Debug.Log($"[BoosterData] {boosterId}: Reset to initial: {initialCount}");
        }

        /// <summary>
        /// 아이템 보유 여부
        /// </summary>
        public static bool HasItem(string boosterId)
        {
            return GetCount(boosterId) > 0;
        }

        /// <summary>
        /// 모든 부스터 데이터 초기화
        /// </summary>
        public static void ResetAll()
        {
            var records = BoosterTableLoader.Load();
            if (records == null)
                return;

            foreach (var record in records)
            {
                Reset(record.BoosterId);
            }
            Debug.Log("[BoosterData] All boosters reset to initial values");
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// BoosterTable에서 초기 보유 개수 조회
        /// </summary>
        private static int GetInitialCount(string boosterId)
        {
            var record = BoosterTableLoader.GetRecord(boosterId);
            if (record != null)
                return record.InitialCount;

            Debug.LogWarning($"[BoosterData] {boosterId}: Not found in BoosterTable, using fallback: {FALLBACK_INITIAL_COUNT}");
            return FALLBACK_INITIAL_COUNT;
        }
    }
}
