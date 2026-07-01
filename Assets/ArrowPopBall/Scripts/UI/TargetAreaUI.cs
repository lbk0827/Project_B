using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Game.Data;
using Game.Arrow;

namespace Game.UI
{
    /// <summary>
    /// 타겟 영역 UI - 상단 고정 풍선 UI 컨테이너
    /// 레이아웃 로직은 BalloonLayoutManager로 분리됨
    /// </summary>
    public class TargetAreaUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("참조")]
        [SerializeField] private Transform _balloonContainer;
        [SerializeField] private GameObject _balloonUIPrefab;
        [SerializeField] private BalloonLayoutManager _layoutManager;

        [Header("남은 풍선 카운터")]
        [SerializeField] private GameObject _counterContainer;
        [SerializeField] private Image _counterIcon;
        [SerializeField] private TextMeshProUGUI _counterText;
        [SerializeField] private float _counterPunchScale = 0.2f;

        // ========== 내부 상태 변수 ==========
        private List<BalloonUIElement> _balloonElements = new List<BalloonUIElement>();
        private RectTransform _targetAreaRect;
        private Canvas _cachedCanvas;
        private Camera _mainCamera;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            _targetAreaRect = GetComponent<RectTransform>();
            _cachedCanvas = GetComponentInParent<Canvas>();
            _mainCamera = Camera.main;

            // LayoutManager 초기화
            if (_layoutManager != null && _balloonContainer != null)
            {
                _layoutManager.Initialize(_balloonContainer);
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 화살표 목록 기반으로 풍선 UI 생성
        /// </summary>
        public void InitializeBalloons(List<ArrowController> arrows)
        {
            ClearBalloons();

            if (arrows == null || arrows.Count == 0)
                return;

            // 색상별 개수 집계
            var colorCounts = new Dictionary<GameColor, int>();
            foreach (var arrow in arrows)
            {
                if (!colorCounts.ContainsKey(arrow.Color))
                    colorCounts[arrow.Color] = 0;
                colorCounts[arrow.Color]++;
            }

            // 총 풍선 개수
            int totalBalloons = arrows.Count;

            // 풍선 크기 계산
            float balloonSize = _layoutManager != null
                ? _layoutManager.CalculateBalloonSize(totalBalloons)
                : 50f;

            // 색상별 풍선 UI 생성
            foreach (var kvp in colorCounts)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    var balloonUI = CreateBalloonUI(kvp.Key, balloonSize);
                    if (balloonUI != null)
                    {
                        _balloonElements.Add(balloonUI);
                    }
                }
            }

            // 풍선 순서 랜덤 셔플
            ShuffleBalloons();

            // 레이아웃 적용
            if (_layoutManager != null)
            {
                _layoutManager.ArrangeBalloons(_balloonElements, balloonSize);
            }

            // 카운터 UI 초기화
            UpdateCounterUI();

            Debug.Log($"[TargetAreaUI] Created {_balloonElements.Count} balloon UI elements, size: {balloonSize:F1}");
        }

        /// <summary>
        /// 특정 색상의 풍선 하나 터뜨리기
        /// </summary>
        public void PopBalloon(GameColor color)
        {
            BalloonUIElement balloon = null;
            for (int i = 0; i < _balloonElements.Count; i++)
            {
                if (_balloonElements[i].Color == color && !_balloonElements[i].IsPopped)
                {
                    balloon = _balloonElements[i];
                    break;
                }
            }
            if (balloon != null)
            {
                // 풍선 팝 애니메이션 실행 후 제거 및 재배치
                balloon.Pop(() =>
                {
                    // 리스트에서 제거
                    _balloonElements.Remove(balloon);

                    // 오브젝트 파괴
                    if (balloon != null && balloon.gameObject != null)
                    {
                        Destroy(balloon.gameObject);
                    }

                    // 카운터 UI 업데이트 (애니메이션 포함)
                    UpdateCounterUI(true);

                    // 남은 풍선들 재배치 (애니메이션 포함)
                    if (_layoutManager != null && _layoutManager.UsePackedLayout && _balloonElements.Count > 0)
                    {
                        _layoutManager.RearrangeBalloonsAnimated(_balloonElements, _layoutManager.CurrentBalloonSize);
                    }
                });

                Debug.Log($"[TargetAreaUI] Popped {color} balloon");
            }
            else
            {
                Debug.LogWarning($"[TargetAreaUI] No available {color} balloon to pop");
            }
        }

        /// <summary>
        /// 특정 색상의 남은 풍선 개수 반환
        /// </summary>
        public int GetRemainingCount(GameColor color)
        {
            int count = 0;
            for (int i = 0; i < _balloonElements.Count; i++)
            {
                if (_balloonElements[i].Color == color && !_balloonElements[i].IsPopped)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 전체 남은 풍선 개수 반환
        /// </summary>
        public int GetTotalRemainingCount()
        {
            int count = 0;
            for (int i = 0; i < _balloonElements.Count; i++)
            {
                if (!_balloonElements[i].IsPopped)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 특정 색상의 풍선 UI 위치 반환 (호밍 타겟용)
        /// </summary>
        public Vector3 GetBalloonWorldPosition(GameColor color)
        {
            BalloonUIElement balloon = null;
            for (int i = 0; i < _balloonElements.Count; i++)
            {
                if (_balloonElements[i].Color == color && !_balloonElements[i].IsPopped)
                {
                    balloon = _balloonElements[i];
                    break;
                }
            }

            if (balloon != null)
            {
                if (_cachedCanvas != null && _cachedCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    RectTransform rectTransform = balloon.CachedRectTransform;
                    if (rectTransform != null && _mainCamera != null)
                    {
                        Vector3 screenPos = rectTransform.position;
                        float cameraDistance = Mathf.Abs(_mainCamera.transform.position.z);
                        screenPos.z = cameraDistance;

                        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(screenPos);
                        worldPos.z = 0f;

                        return worldPos;
                    }
                }

                return balloon.transform.position;
            }

            Debug.LogWarning($"[TargetAreaUI] No balloon found for color: {color}");
            return Vector3.zero;
        }

        /// <summary>
        /// 모든 풍선 UI 제거
        /// </summary>
        public void ClearBalloons()
        {
            foreach (var balloon in _balloonElements)
            {
                if (balloon != null)
                {
                    Destroy(balloon.gameObject);
                }
            }
            _balloonElements.Clear();
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// 풍선 리스트 셔플 (Fisher-Yates 알고리즘)
        /// </summary>
        private void ShuffleBalloons()
        {
            int n = _balloonElements.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = _balloonElements[i];
                _balloonElements[i] = _balloonElements[j];
                _balloonElements[j] = temp;
            }
        }

        private BalloonUIElement CreateBalloonUI(GameColor color, float size)
        {
            if (_balloonUIPrefab == null || _balloonContainer == null)
            {
                Debug.LogError("[TargetAreaUI] Missing prefab or container reference");
                return null;
            }

            GameObject balloonObj = Instantiate(_balloonUIPrefab, _balloonContainer);
            BalloonUIElement element = balloonObj.GetComponent<BalloonUIElement>();

            if (element == null)
            {
                element = balloonObj.AddComponent<BalloonUIElement>();
            }

            element.Initialize(color, size);

            return element;
        }

        /// <summary>
        /// 남은 풍선 카운터 UI 업데이트
        /// </summary>
        private void UpdateCounterUI(bool animate = false)
        {
            int remaining = GetTotalRemainingCount();

            // 카운터 텍스트가 할당되지 않았으면 자동 생성 시도
            if (_counterText == null && _counterContainer == null)
            {
                CreateCounterUI();
            }

            // 텍스트 업데이트
            if (_counterText != null)
            {
                _counterText.text = remaining.ToString();

                // 펀치 스케일 애니메이션
                if (animate && _counterText.transform != null)
                {
                    _counterText.transform.DOKill();
                    _counterText.transform.localScale = Vector3.one;
                    _counterText.transform.DOPunchScale(Vector3.one * _counterPunchScale, 0.2f, 1, 0.5f);
                }
            }
        }

        /// <summary>
        /// 카운터 UI 자동 생성
        /// </summary>
        private void CreateCounterUI()
        {
            if (_targetAreaRect == null)
                return;

            // 카운터 컨테이너 생성
            GameObject containerObj = new GameObject("BalloonCounter");
            containerObj.transform.SetParent(_targetAreaRect, false);

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 1);
            containerRect.anchorMax = new Vector2(0, 1);
            containerRect.pivot = new Vector2(0, 1);
            containerRect.anchoredPosition = new Vector2(10, -10);
            containerRect.sizeDelta = new Vector2(100, 40);

            // Horizontal Layout Group 추가
            HorizontalLayoutGroup layout = containerObj.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 5;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // 풍선 아이콘
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(containerObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(30, 30);
            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.color = new Color(1f, 0.4f, 0.4f);

            // 텍스트
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(containerObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(60, 40);
            _counterText = textObj.AddComponent<TextMeshProUGUI>();
            _counterText.fontSize = 24;
            _counterText.fontStyle = FontStyles.Bold;
            _counterText.color = Color.white;
            _counterText.alignment = TextAlignmentOptions.Left;
            _counterText.text = "0";

            _counterContainer = containerObj;
            _counterIcon = iconImage;

            Debug.Log("[TargetAreaUI] Counter UI auto-created");
        }
    }
}