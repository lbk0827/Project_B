using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// Hint 버튼 UI - 클릭 시 HintManager.ExecuteHint() 호출
    /// </summary>
    public class HintButtonUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("아이템 참조")]
        [SerializeField] private ItemRefSO _itemRef;

        [Header("UI 참조")]
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private Image _iconImage;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(OnButtonClicked);
        }

        private void Start()
        {
            ApplyItemRef();
            RefreshCount();

            if (HintManager.Instance != null)
                HintManager.Instance.OnItemCountChanged += OnItemCountChanged;
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClicked);

            if (HintManager.Instance != null)
                HintManager.Instance.OnItemCountChanged -= OnItemCountChanged;
        }

        // ========== 내부 유틸리티 ==========
        private void ApplyItemRef()
        {
            if (_itemRef != null && _iconImage != null)
                _iconImage.sprite = _itemRef.GetIcon("Hint");
        }

        private void OnButtonClicked()
        {
            if (HintManager.Instance == null)
                return;

            HintManager.Instance.ExecuteHint();
            RefreshCount();
        }

        private void OnItemCountChanged(int newCount)
        {
            RefreshCount();
        }

        private void RefreshCount()
        {
            if (_countText == null)
                return;

            int count = HintManager.Instance?.ItemCount ?? 0;
            _countText.text = count.ToString();
        }
    }
}
