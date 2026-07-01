using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// Level Editor에서 사용하는 화살표 데이터 클래스
    /// </summary>
    public class EditorArrow
    {
        public int id;
        public List<Vector2Int> cells = new List<Vector2Int>();
        public ArrowDirection headDirection;
        public GameColor color;
    }
}
