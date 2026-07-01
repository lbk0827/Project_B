using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Game.UI
{
    /// <summary>
    /// 최상단 UI - 뒤로가기, 다시하기, 하트(Life), 레벨 표시
    /// </summary>
    public class TopBarUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("버튼")]
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _restartButton;

        [Header("하트 UI")]
        [SerializeField] private Transform _heartContainer;
        [SerializeField] private GameObject _heartPrefab;

        [Header("레벨 표시")]
        [SerializeField] private TextMeshProUGUI _levelText;

        [Header("애니메이션 설정")]
        [SerializeField] private float _heartLoseDuration = 0.3f;
        [SerializeField] private float _heartShakeDuration = 0.2f;

        // ========== 내부 상태 변수 ==========
        private List<GameObject> _heartObjects = new List<GameObject>();
        private int _maxLives;
        private int _currentLives;

        // ========== 이벤트 ==========
        public event Action OnBackClicked;
        public event Action OnRestartClicked;

        // ========== 프로퍼티 ==========
        public int CurrentLives => _currentLives;
        public int MaxLives => _maxLives;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            // 버튼 이벤트 연결
            if (_backButton != null)
            {
                _backButton.onClick.AddListener(HandleBackClick);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(HandleRestartClick);
            }
        }

        private void OnDestroy()
        {
            // 버튼 이벤트 해제
            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(HandleBackClick);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(HandleRestartClick);
            }
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// TopBarUI 초기화
        /// </summary>
        public void Initialize(int levelId, int maxLives)
        {
            _maxLives = maxLives;
            _currentLives = maxLives;

            // 레벨 텍스트 설정
            SetLevelText(levelId);

            // 하트 UI 생성
            CreateHearts(maxLives);

            Debug.Log($"[TopBarUI] Initialized: Level {levelId}, Lives {maxLives}");
        }

        /// <summary>
        /// 레벨 텍스트 설정
        /// </summary>
        public void SetLevelText(int levelId)
        {
            if (_levelText != null)
            {
                _levelText.text = $"Level {levelId}";
            }
        }

        /// <summary>
        /// 현재 Life 개수 설정 (즉시 반영)
        /// </summary>
        public void SetLives(int current)
        {
            int previousLives = _currentLives;
            _currentLives = Mathf.Clamp(current, 0, _maxLives);

            // 하트 감소 애니메이션
            if (_currentLives < previousLives)
            {
                for (int i = previousLives - 1; i >= _currentLives; i--)
                {
                    if (i >= 0 && i < _heartObjects.Count)
                    {
                        AnimateHeartLoss(_heartObjects[i], i == _currentLives);
                    }
                }
            }
            // 하트 증가 (다시하기 등)
            else if (_currentLives > previousLives)
            {
                RefreshHeartVisuals();
            }
        }

        /// <summary>
        /// 하트 1개 감소 (애니메이션 포함)
        /// </summary>
        public void LoseLife()
        {
            if (_currentLives > 0)
            {
                SetLives(_currentLives - 1);
            }
        }

        /// <summary>
        /// 하트 UI 리셋 (레벨 재시작 시)
        /// </summary>
        public void ResetHearts()
        {
            _currentLives = _maxLives;
            RefreshHeartVisuals();
        }

        // ========== 내부 유틸리티 ==========
        private void HandleBackClick()
        {
            Debug.Log("[TopBarUI] Back button clicked");
            OnBackClicked?.Invoke();
        }

        private void HandleRestartClick()
        {
            Debug.Log("[TopBarUI] Restart button clicked");
            OnRestartClicked?.Invoke();
        }

        /// <summary>
        /// 하트 UI 오브젝트 생성
        /// </summary>
        private void CreateHearts(int count)
        {
            // 기존 하트 제거
            ClearHearts();

            if (_heartPrefab == null || _heartContainer == null)
            {
                Debug.LogWarning("[TopBarUI] Heart prefab or container not assigned");
                return;
            }

            // HeartContainer의 기존 자식도 모두 제거 (수동 추가된 것 포함)
            for (int i = _heartContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_heartContainer.GetChild(i).gameObject);
            }

            // 새 하트 생성
            for (int i = 0; i < count; i++)
            {
                var heartObj = Instantiate(_heartPrefab, _heartContainer);
                heartObj.SetActive(true);
                _heartObjects.Add(heartObj);
            }

            Debug.Log($"[TopBarUI] CreateHearts: requested={count}, created={_heartObjects.Count}");
        }

        /// <summary>
        /// 기존 하트 모두 제거
        /// </summary>
        private void ClearHearts()
        {
            foreach (var heart in _heartObjects)
            {
                if (heart != null)
                {
                    Destroy(heart);
                }
            }
            _heartObjects.Clear();
        }

        /// <summary>
        /// 하트 비주얼 새로고침
        /// </summary>
        private void RefreshHeartVisuals()
        {
            for (int i = 0; i < _heartObjects.Count; i++)
            {
                if (_heartObjects[i] != null)
                {
                    bool isActive = i < _currentLives;
                    _heartObjects[i].SetActive(true);

                    // 이미지 알파 및 스케일 복원
                    var image = _heartObjects[i].GetComponent<Image>();
                    if (image != null)
                    {
                        var color = image.color;
                        color.a = isActive ? 1f : 0.3f;
                        image.color = color;
                    }

                    _heartObjects[i].transform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// 하트 감소 애니메이션
        /// </summary>
        private void AnimateHeartLoss(GameObject heartObj, bool isLast)
        {
            if (heartObj == null) return;

            var rectTransform = heartObj.GetComponent<RectTransform>();
            var image = heartObj.GetComponent<Image>();

            if (rectTransform == null) return;

            // 기존 트윈 제거
            DOTween.Kill(rectTransform);

            // 시퀀스: 흔들림 → 축소 + 페이드
            var sequence = DOTween.Sequence();

            // 1. 흔들림 효과
            sequence.Append(rectTransform.DOShakeScale(_heartShakeDuration, 0.3f, 10));

            // 2. 축소 + 페이드 아웃
            sequence.Append(rectTransform.DOScale(0f, _heartLoseDuration).SetEase(Ease.InBack));

            if (image != null)
            {
                sequence.Join(image.DOFade(0f, _heartLoseDuration));
            }

            // 완료 후 비활성화 대신 투명하게 유지 (슬롯 유지)
            sequence.OnComplete(() =>
            {
                if (image != null)
                {
                    var color = image.color;
                    color.a = 0.3f;  // 반투명으로 표시 (잃어버린 하트)
                    image.color = color;
                }
                rectTransform.localScale = Vector3.one;
            });
        }
    }
}
