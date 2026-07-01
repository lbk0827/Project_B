using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 개별 아이템 엔트리 (Key + 아이콘 + 표시 이름)
    /// </summary>
    [Serializable]
    public class ItemEntry
    {
        [Tooltip("아이템 고유 키 (Coin, Heart, Hint, Eraser, ArrowDash)")]
        public string Key;

        [Tooltip("UI에 표시할 이름")]
        public string DisplayName;

        [Tooltip("아이콘 이미지")]
        public Sprite Icon;
    }

    /// <summary>
    /// 아이템/재화 통합 레퍼런스 ScriptableObject
    /// 하나의 .asset 파일에서 모든 아이템의 Key, 아이콘, 표시 이름을 관리
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRef", menuName = "ArrowPopBall/Item Ref")]
    public class ItemRefSO : ScriptableObject
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("아이템 목록")]
        [SerializeField] private List<ItemEntry> _items = new List<ItemEntry>();

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// Key로 아이템 엔트리 조회
        /// </summary>
        public ItemEntry GetItem(string key)
        {
            foreach (var item in _items)
            {
                if (item.Key == key)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Key로 아이콘 Sprite 조회
        /// </summary>
        public Sprite GetIcon(string key)
        {
            return GetItem(key)?.Icon;
        }

        /// <summary>
        /// Key로 표시 이름 조회
        /// </summary>
        public string GetDisplayName(string key)
        {
            return GetItem(key)?.DisplayName;
        }
    }
}
