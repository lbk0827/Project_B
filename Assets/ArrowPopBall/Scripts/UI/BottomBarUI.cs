using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// 하단 바 UI - Arrow Dash 버튼 등 인게임 하단 UI 관리
    /// </summary>
    public class BottomBarUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("아이템 참조")]
        [SerializeField] private ItemRefSO _itemRef;

        [Header("Arrow Dash")]
        [SerializeField] private Button _arrowDashButton;
        [SerializeField] private TextMeshProUGUI _arrowDashCountText;
        [SerializeField] private Image _arrowDashIconImage;

        [Header("Arrow Dash UI 참조")]
        [SerializeField] private ArrowDashUI _arrowDashUI;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (_arrowDashButton != null)
            {
                _arrowDashButton.onClick.AddListener(OnArrowDashButtonClicked);
            }
        }

        private void Start()
        {
            ApplyItemRef();
            UpdateArrowDashCount();

            // Start에서 이벤트 구독 (ArrowDashManager가 Awake에서 초기화된 후)
            SubscribeToEvents();
        }

        private void OnEnable()
        {
            // OnEnable은 Start보다 먼저 호출될 수 있어 Instance가 null일 수 있음
            // Start에서 재시도하므로 여기서는 null 체크만 수행
            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            if (ArrowDashManager.Instance != null)
            {
                // 중복 구독 방지를 위해 먼저 해제
                ArrowDashManager.Instance.OnItemCountChanged -= OnItemCountChanged;
                ArrowDashManager.Instance.OnUIStateChanged -= OnArrowDashUIStateChanged;

                // 이벤트 구독
                ArrowDashManager.Instance.OnItemCountChanged += OnItemCountChanged;
                ArrowDashManager.Instance.OnUIStateChanged += OnArrowDashUIStateChanged;

                Debug.Log("[BottomBarUI] Subscribed to ArrowDashManager events");
            }
        }

        private void OnDisable()
        {
            if (ArrowDashManager.Instance != null)
            {
                ArrowDashManager.Instance.OnItemCountChanged -= OnItemCountChanged;
                ArrowDashManager.Instance.OnUIStateChanged -= OnArrowDashUIStateChanged;
            }
        }

        // ========== 버튼 콜백 ==========
        private void OnArrowDashButtonClicked()
        {
            // 애니메이션 진행 중에는 클릭 무시
            if (_arrowDashUI != null && _arrowDashUI.IsAnimating)
            {
                Debug.Log("[BottomBarUI] Arrow Dash button clicked - ignored (animation in progress)");
                return;
            }

            Debug.Log("[BottomBarUI] Arrow Dash button clicked");

            if (ArrowDashManager.Instance != null)
            {
                // 토글 기능: UI가 열려있으면 닫고, 닫혀있으면 열기
                if (ArrowDashManager.Instance.IsActive)
                {
                    // UI 닫기
                    ArrowDashManager.Instance.CloseUI();
                }
                else
                {
                    // UI 열기
                    ArrowDashManager.Instance.OpenUI();

                    // UI 표시
                    if (_arrowDashUI != null)
                    {
                        _arrowDashUI.Show();
                    }
                }
            }
            else
            {
                Debug.LogWarning("[BottomBarUI] ArrowDashManager.Instance is null!");
            }
        }

        // ========== 이벤트 핸들러 ==========
        private void OnItemCountChanged(int newCount)
        {
            Debug.Log($"[BottomBarUI] OnItemCountChanged: {newCount}");
            UpdateArrowDashCount();
        }

        private void OnArrowDashUIStateChanged(bool isOpen)
        {
            // UI 열림/닫힘에 따른 처리 (필요 시)
        }

        // ========== 내부 유틸리티 ==========
        private void ApplyItemRef()
        {
            if (_itemRef != null && _arrowDashIconImage != null)
                _arrowDashIconImage.sprite = _itemRef.GetIcon("ArrowDash");
        }

        private void UpdateArrowDashCount()
        {
            if (_arrowDashCountText != null)
            {
                int count = ArrowDashData.GetCount();
                _arrowDashCountText.text = count.ToString();
            }
        }
    }
}