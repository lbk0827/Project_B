using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// StageTable JSON 레코드
    /// Excel/JSON으로 관리하기 쉬운 단순 구조 (PascalCase)
    /// </summary>
    [Serializable]
    public class StageTableRecord
    {
        public int Level;           // 레벨 번호 (1, 2, 3...)
        public int StageIdx;        // Stage 에셋 번호 (Stage_001 → 1)
        public int Difficulty;      // 1: Normal, 2: Hard, 3: Nightmare
        public int Lives;           // 하트 개수

        /// <summary>
        /// Difficulty enum 변환
        /// </summary>
        public Data.Difficulty GetDifficulty()
        {
            // 1=Normal, 2=Hard, 3=Nightmare → 0, 1, 2로 변환
            return (Data.Difficulty)Mathf.Clamp(Difficulty - 1, 0, 2);
        }

        /// <summary>
        /// Stage 에셋 경로 반환
        /// </summary>
        public string GetStageAssetPath()
        {
            return $"Stages/Stage_{StageIdx:D3}";
        }
    }
}