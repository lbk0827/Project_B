using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 가이드 라인 토글 버튼 UI
    /// </summary>
    public class GuideLineButton : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("필수 참조")]
        [SerializeField] private Button _button;
        [SerializeField] private Image _iconImage;

        [Header("아이콘 스프라이트")]
        [SerializeField] private Sprite _iconOn;   // 눈 열림 (가이드 라인 표시)
        [SerializeField] private Sprite _iconOff;  // 눈 감김 (가이드 라인 숨김)

        // ========== 내부 상태 변수 ==========
        private bool _isOn = false;

        // ========== 이벤트 ==========
        public event Action<bool> OnToggleChanged;

        // ========== 프로퍼티 ==========
        public bool IsOn => _isOn;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();
        }

        private void Start()
        {
            if (_button != null)
                _button.onClick.AddListener(OnButtonClicked);

            // 초기 상태: Off
            SetState(false, notify: false);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClicked);
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 버튼 상태 설정
        /// </summary>
        public void SetState(bool isOn, bool notify = true)
        {
            _isOn = isOn;
            UpdateVisual();

            if (notify)
            {
                OnToggleChanged?.Invoke(_isOn);
            }
        }

        /// <summary>
        /// 상태 리셋 (레벨 재시작 시)
        /// </summary>
        public void ResetState()
        {
            SetState(false, notify: true);
        }

        // ========== 내부 유틸리티 ==========
        private void OnButtonClicked()
        {
            SetState(!_isOn, notify: true);
        }

        private void UpdateVisual()
        {
            if (_iconImage == null)
                return;

            if (_isOn && _iconOn != null)
            {
                _iconImage.sprite = _iconOn;
            }
            else if (!_isOn && _iconOff != null)
            {
                _iconImage.sprite = _iconOff;
            }
        }
    }
}
