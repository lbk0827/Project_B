using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 레벨 설정 테이블 (ScriptableObject)
    /// 레벨 번호와 스테이지 맵을 매핑하고, 난이도/하트 등 설정 관리
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "ArrowPopBall/Level Config")]
    public class LevelConfig : ScriptableObject
    {
        [Header("레벨 목록")]
        [SerializeField] private List<LevelEntry> _levels = new List<LevelEntry>();

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 전체 레벨 수
        /// </summary>
        public int LevelCount => _levels?.Count ?? 0;

        /// <summary>
        /// 레벨 번호로 LevelEntry 가져오기
        /// </summary>
        public LevelEntry GetLevel(int levelNumber)
        {
            if (_levels == null) return null;

            foreach (var entry in _levels)
            {
                if (entry.level == levelNumber)
                    return entry;
            }

            Debug.LogWarning($"[LevelConfig] Level {levelNumber} not found!");
            return null;
        }

        /// <summary>
        /// 레벨 번호로 StageData 가져오기
        /// </summary>
        public StageData GetStageData(int levelNumber)
        {
            var entry = GetLevel(levelNumber);
            return entry?.stageData;
        }

        /// <summary>
        /// 특정 난이도의 레벨 목록 가져오기
        /// </summary>
        public List<LevelEntry> GetLevelsByDifficulty(Difficulty difficulty)
        {
            var result = new List<LevelEntry>();
            if (_levels == null) return result;

            foreach (var entry in _levels)
            {
                if (entry.difficulty == difficulty)
                    result.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// 레벨 존재 여부 확인
        /// </summary>
        public bool HasLevel(int levelNumber)
        {
            return GetLevel(levelNumber) != null;
        }

        /// <summary>
        /// 마지막 레벨 번호
        /// </summary>
        public int MaxLevel
        {
            get
            {
                if (_levels == null || _levels.Count == 0) return 0;

                int max = 0;
                foreach (var entry in _levels)
                {
                    if (entry.level > max)
                        max = entry.level;
                }
                return max;
            }
        }

        /// <summary>
        /// LevelData로 변환 (기존 시스템 호환용)
        /// </summary>
        public LevelData GetLevelData(int levelNumber)
        {
            var entry = GetLevel(levelNumber);
            if (entry == null || entry.stageData == null)
            {
                Debug.LogWarning($"[LevelConfig] Cannot get LevelData for level {levelNumber}");
                return null;
            }

            return entry.stageData.ToLevelData(
                levelNumber,
                entry.lives,
                entry.starThresholds
            );
        }

        // ========== 에디터 유틸리티 ==========

#if UNITY_EDITOR
        /// <summary>
        /// 새 레벨 엔트리 추가 (에디터용)
        /// </summary>
        public void AddLevel(LevelEntry entry)
        {
            if (_levels == null)
                _levels = new List<LevelEntry>();

            // 중복 체크
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].level == entry.level)
                {
                    _levels[i] = entry;  // 덮어쓰기
                    return;
                }
            }

            _levels.Add(entry);
            _levels.Sort((a, b) => a.level.CompareTo(b.level));
        }

        /// <summary>
        /// 레벨 제거 (에디터용)
        /// </summary>
        public void RemoveLevel(int levelNumber)
        {
            if (_levels == null) return;

            _levels.RemoveAll(e => e.level == levelNumber);
        }

        /// <summary>
        /// 전체 레벨 목록 반환 (에디터용)
        /// </summary>
        public List<LevelEntry> GetAllLevels()
        {
            return _levels ?? new List<LevelEntry>();
        }
#endif
    }

    /// <summary>
    /// 레벨 엔트리 - 레벨 번호와 스테이지/설정 매핑
    /// </summary>
    [Serializable]
    public class LevelEntry
    {
        [Header("레벨 정보")]
        [Tooltip("레벨 번호 (1, 2, 3...)")]
        public int level;

        [Tooltip("사용할 스테이지 맵")]
        public StageData stageData;

        [Header("난이도 설정")]
        [Tooltip("레벨 난이도")]
        public Difficulty difficulty = Difficulty.Normal;

        [Tooltip("하트(목숨) 개수")]
        [Range(1, 10)]
        public int lives = 3;

        [Header("별점 기준")]
        [Tooltip("별점 기준 [3성, 2성, 1성] - 남은 하트 수 기준")]
        public int[] starThresholds = new int[] { 3, 2, 1 };

        [Header("추가 설정")]
        [Tooltip("시간 제한 (0 = 무제한)")]
        public float timeLimit = 0f;

        [Tooltip("특수 규칙 (추후 확장용)")]
        [TextArea(1, 2)]
        public string specialRules;
    }
}