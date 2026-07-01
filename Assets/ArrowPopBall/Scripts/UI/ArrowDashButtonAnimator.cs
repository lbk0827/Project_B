using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// Arrow Dash 방향 버튼 펼침/접힘 + TopPanel 슬라이드 애니메이션
    /// 4방향 버튼은 이 컴포넌트가 붙은 오브젝트(ArrowDashButton)의 자식이어야 함
    /// → 자식이므로 시작 위치는 항상 (0, 0) (부모 중심)
    /// </summary>
    public class ArrowDashButtonAnimator : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("방향 버튼 (ArrowDashButton의 자식 오브젝트)")]
        [SerializeField] private Button _northButton;
        [SerializeField] private Button _southButton;
        [SerializeField] private Button _eastButton;
        [SerializeField] private Button _westButton;

        [Header("상단 패널 (ArrowDashUI)")]
        [SerializeField] private GameObject _topPanel;

        [Header("버튼 펼침 애니메이션")]
        [SerializeField] private float _buttonExpandDuration = 0.15f;
        [SerializeField] private float _buttonExpandDelay = 0.05f;
        [SerializeField] private Ease _buttonExpandEase = Ease.OutBack;

        [Header("TopPanel 슬라이드 애니메이션")]
        [SerializeField] private float _topPanelSlideDistance = 200f;
        [SerializeField] private float _topPanelSlideDuration = 0.25f;
        [SerializeField] private Ease _topPanelSlideEase = Ease.OutCubic;

        // ========== 내부 상태 변수 ==========
        private bool _isAnimating;

        // 버튼 원래 위치 저장 (부모 기준 로컬 좌표)
        private Vector2 _northOriginalPos;
        private Vector2 _southOriginalPos;
        private Vector2 _eastOriginalPos;
        private Vector2 _westOriginalPos;
        private bool _originalPosInitialized;

        // TopPanel 원래 위치 저장
        private Vector2 _topPanelOriginalPos;
        private bool _topPanelPosInitialized;

        // ========== 프로퍼티 ==========
        public bool IsAnimating => _isAnimating;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            HideDirectionButtons();
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 버튼 펼침 + TopPanel 슬라이드 다운 애니메이션 실행
        /// </summary>
        public void PlayExpand()
        {
            SaveOriginalButtonPositions();
            SaveTopPanelOriginalPosition();
            PlayTopPanelSlideDown();
            PlayButtonExpandAnimation();
        }

        /// <summary>
        /// 버튼 접힘 + TopPanel 슬라이드 업 애니메이션 실행
        /// </summary>
        /// <returns>전체 애니메이션 소요 시간</returns>
        public float PlayCollapse()
        {
            PlayTopPanelSlideUp();
            return PlayButtonCollapseAnimation();
        }

        /// <summary>
        /// TopPanel 위치를 원래대로 복원
        /// </summary>
        public void ResetTopPanel()
        {
            ResetTopPanelPosition();
        }

        // ========== 내부 유틸리티 - TopPanel ==========

        private void SaveTopPanelOriginalPosition()
        {
            if (_topPanelPosInitialized || _topPanel == null)
                return;

            var rectTransform = _topPanel.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                _topPanelOriginalPos = rectTransform.anchoredPosition;
                _topPanelPosInitialized = true;
            }
        }

        private void PlayTopPanelSlideDown()
        {
            if (_topPanel == null)
                return;

            var topPanelRect = _topPanel.GetComponent<RectTransform>();
            if (topPanelRect == null)
                return;

            Vector2 startPos = _topPanelOriginalPos + new Vector2(0, _topPanelSlideDistance);
            topPanelRect.anchoredPosition = startPos;

            topPanelRect.DOAnchorPos(_topPanelOriginalPos, _topPanelSlideDuration)
                .SetEase(_topPanelSlideEase);
        }

        private void PlayTopPanelSlideUp()
        {
            if (_topPanel == null)
                return;

            var topPanelRect = _topPanel.GetComponent<RectTransform>();
            if (topPanelRect == null)
                return;

            Vector2 targetPos = _topPanelOriginalPos + new Vector2(0, _topPanelSlideDistance);

            topPanelRect.DOAnchorPos(targetPos, _topPanelSlideDuration)
                .SetEase(Ease.InCubic);
        }

        private void ResetTopPanelPosition()
        {
            if (_topPanel == null || !_topPanelPosInitialized)
                return;

            var topPanelRect = _topPanel.GetComponent<RectTransform>();
            if (topPanelRect != null)
            {
                topPanelRect.anchoredPosition = _topPanelOriginalPos;
            }
        }

        // ========== 내부 유틸리티 - 버튼 위치 ==========

        private void SaveOriginalButtonPositions()
        {
            if (_originalPosInitialized)
                return;

            if (_northButton != null)
                _northOriginalPos = ((RectTransform)_northButton.transform).anchoredPosition;
            if (_southButton != null)
                _southOriginalPos = ((RectTransform)_southButton.transform).anchoredPosition;
            if (_eastButton != null)
                _eastOriginalPos = ((RectTransform)_eastButton.transform).anchoredPosition;
            if (_westButton != null)
                _westOriginalPos = ((RectTransform)_westButton.transform).anchoredPosition;

            _originalPosInitialized = true;
        }

        // ========== 내부 유틸리티 - 초기화 ==========

        private void HideDirectionButtons()
        {
            var buttons = new Button[] { _northButton, _southButton, _eastButton, _westButton };
            foreach (var button in buttons)
            {
                if (button == null)
                    continue;
                var rt = (RectTransform)button.transform;
                rt.localScale = Vector3.zero;
            }
        }

        // ========== 내부 유틸리티 - 펼침/접힘 애니메이션 ==========

        private void PlayButtonExpandAnimation()
        {
            _isAnimating = true;

            var buttons = new (Button button, Vector2 targetPos)[]
            {
                (_northButton, _northOriginalPos),
                (_eastButton,  _eastOriginalPos),
                (_southButton, _southOriginalPos),
                (_westButton,  _westOriginalPos)
            };

            int validButtonCount = 0;
            int lastValidIndex = -1;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].button != null)
                {
                    validButtonCount++;
                    lastValidIndex = i;
                }
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                var (button, targetPos) = buttons[i];
                if (button == null)
                    continue;

                var rectTransform = (RectTransform)button.transform;

                // 시작 위치: 부모(ArrowDashButton) 중심 = (0, 0)
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.localScale = Vector3.zero;

                float delay = i * _buttonExpandDelay;

                rectTransform.DOAnchorPos(targetPos, _buttonExpandDuration)
                    .SetDelay(delay)
                    .SetEase(_buttonExpandEase);

                var scaleTween = rectTransform.DOScale(Vector3.one, _buttonExpandDuration)
                    .SetDelay(delay)
                    .SetEase(_buttonExpandEase);

                if (i == lastValidIndex)
                {
                    scaleTween.OnComplete(() => _isAnimating = false);
                }
            }

            if (validButtonCount == 0)
            {
                _isAnimating = false;
            }
        }

        private float PlayButtonCollapseAnimation()
        {
            _isAnimating = true;

            var buttons = new Button[]
            {
                _westButton,
                _southButton,
                _eastButton,
                _northButton
            };

            int validButtonCount = 0;
            int lastValidIndex = -1;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    validButtonCount++;
                    lastValidIndex = i;
                }
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                    continue;

                var rectTransform = (RectTransform)button.transform;
                float delay = i * _buttonExpandDelay;

                // 목적지: 부모(ArrowDashButton) 중심 = (0, 0)
                rectTransform.DOAnchorPos(Vector2.zero, _buttonExpandDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.InBack);

                var scaleTween = rectTransform.DOScale(Vector3.zero, _buttonExpandDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.InBack);

                if (i == lastValidIndex)
                {
                    scaleTween.OnComplete(() => _isAnimating = false);
                }
            }

            if (validButtonCount == 0)
            {
                _isAnimating = false;
            }

            return (validButtonCount - 1) * _buttonExpandDelay + _buttonExpandDuration;
        }
    }
}
