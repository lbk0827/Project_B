using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// Eraser 버튼 UI - 클릭 시 선택 모드 토글
    /// 선택 모드에서 화살표를 탭하면 강제 탈출 (InputHandler 처리)
    /// </summary>
    public class EraserButtonUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("아이템 참조")]
        [SerializeField] private ItemRefSO _itemRef;

        [Header("UI 참조")]
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private Image _buttonImage;
        [SerializeField] private Image _iconImage;

        [Header("선택 모드 색상")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _selectModeColor = new Color(1f, 0.8f, 0.3f, 1f);

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

            if (EraserManager.Instance != null)
            {
                EraserManager.Instance.OnItemCountChanged += OnItemCountChanged;
                EraserManager.Instance.OnSelectModeChanged += OnSelectModeChanged;
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClicked);

            if (EraserManager.Instance != null)
            {
                EraserManager.Instance.OnItemCountChanged -= OnItemCountChanged;
                EraserManager.Instance.OnSelectModeChanged -= OnSelectModeChanged;
            }
        }

        // ========== 내부 유틸리티 ==========
        private void ApplyItemRef()
        {
            if (_itemRef != null && _iconImage != null)
                _iconImage.sprite = _itemRef.GetIcon("Eraser");
        }

        private void OnButtonClicked()
        {
            EraserManager.Instance?.EnterSelectMode();
        }

        private void OnItemCountChanged(int newCount)
        {
            RefreshCount();
        }

        private void OnSelectModeChanged(bool isSelectMode)
        {
            if (_buttonImage != null)
                _buttonImage.color = isSelectMode ? _selectModeColor : _normalColor;
        }

        private void RefreshCount()
        {
            if (_countText == null)
                return;

            int count = EraserManager.Instance?.ItemCount ?? 0;
            _countText.text = count.ToString();
        }
    }
}
