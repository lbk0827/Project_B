using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Arrow;
using Game.Data;

namespace Game.GuideLine
{
    /// <summary>
    /// 가이드 라인 매니저 - 모든 화살표의 가이드 라인 관리 (LineRenderer 기반)
    /// </summary>
    public class GuideLineManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        private static GuideLineManager _instance;
        public static GuideLineManager Instance => _instance;

        // ========== 인스펙터 노출 변수 ==========
        [Header("라인 설정")]
        [SerializeField] private Color _lineColor = new Color(0.53f, 0.53f, 0.53f, 0.6f);
        [SerializeField] private float _lineWidth = 0.05f;
        [SerializeField] private string _sortingLayerName = "Background";
        [SerializeField] private int _sortingOrder = 0;

        [Header("애니메이션")]
        [SerializeField] private float _fadeInDuration = 0.2f;
        [SerializeField] private float _fadeOutDuration = 0.15f;

        // ========== 내부 상태 변수 ==========
        private bool _isEnabled = false;
        private Dictionary<int, GuideLineRenderer> _guideLines = new Dictionary<int, GuideLineRenderer>();
        private List<ArrowController> _arrows = new List<ArrowController>();

        // ========== 프로퍼티 ==========
        public bool IsEnabled => _isEnabled;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            ClearAll();
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 가이드 라인 On/Off 토글
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (_isEnabled == enabled)
                return;

            _isEnabled = enabled;
            Debug.Log($"[GuideLineManager] SetEnabled: {enabled}");

            if (_isEnabled)
            {
                ShowAllLines();
            }
            else
            {
                HideAllLines();
            }
        }

        /// <summary>
        /// 레벨 시작 시 초기화
        /// </summary>
        public void InitializeForLevel(List<ArrowController> arrows)
        {
            ClearAll();

            _arrows = new List<ArrowController>(arrows);

            foreach (var arrow in _arrows)
            {
                CreateGuideLineForArrow(arrow);
                SubscribeArrowEvents(arrow);
            }

            Debug.Log($"[GuideLineManager] Initialized for {arrows.Count} arrows");

            // 초기화 시 가이드라인 비활성화 (기본값 Off)
            _isEnabled = false;
            HideAllLines();
        }

        /// <summary>
        /// 레벨 종료 시 정리
        /// </summary>
        public void ClearAll()
        {
            foreach (var arrow in _arrows)
            {
                UnsubscribeArrowEvents(arrow);
            }

            foreach (var kvp in _guideLines)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }

            _guideLines.Clear();
            _arrows.Clear();
            _isEnabled = false;
        }

        // ========== 내부 유틸리티 ==========
        private void CreateGuideLineForArrow(ArrowController arrow)
        {
            if (arrow == null || _guideLines.ContainsKey(arrow.Id))
                return;

            // LineRenderer 기반 GuideLine 생성
            GameObject lineObj = new GameObject($"GuideLine_{arrow.Id}");
            lineObj.transform.SetParent(transform);

            // LineRenderer 추가
            LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();

            // GuideLineRenderer 추가 및 초기화
            GuideLineRenderer renderer = lineObj.AddComponent<GuideLineRenderer>();
            renderer.Initialize(arrow, _lineColor, _lineWidth, _sortingLayerName, _sortingOrder);

            _guideLines[arrow.Id] = renderer;
        }

        private void SubscribeArrowEvents(ArrowController arrow)
        {
            if (arrow == null)
                return;

            arrow.OnMoveStarted += HandleArrowMoveStarted;
            arrow.OnStopped += HandleArrowStopped;
            arrow.OnExtracted += HandleArrowExtracted;
        }

        private void UnsubscribeArrowEvents(ArrowController arrow)
        {
            if (arrow == null)
                return;

            arrow.OnMoveStarted -= HandleArrowMoveStarted;
            arrow.OnStopped -= HandleArrowStopped;
            arrow.OnExtracted -= HandleArrowExtracted;
        }

        private void HandleArrowMoveStarted(ArrowController arrow)
        {
            if (!_isEnabled)
                return;

            if (_guideLines.TryGetValue(arrow.Id, out var renderer))
            {
                renderer.HideImmediate();
            }
        }

        private void HandleArrowStopped(ArrowController arrow)
        {
            if (!_isEnabled)
                return;

            if (_guideLines.TryGetValue(arrow.Id, out var renderer))
            {
                renderer.UpdateLine();
                renderer.FadeIn(_fadeInDuration);
            }
        }

        private void HandleArrowExtracted(ArrowController arrow)
        {
            if (_guideLines.TryGetValue(arrow.Id, out var renderer))
            {
                if (renderer != null)
                    Destroy(renderer.gameObject);

                _guideLines.Remove(arrow.Id);
            }

            _arrows.Remove(arrow);
        }

        private void ShowAllLines()
        {
            foreach (var kvp in _guideLines)
            {
                if (kvp.Value != null && kvp.Value.Arrow != null)
                {
                    // Idle 상태인 화살표만 라인 표시
                    if (kvp.Value.Arrow.State == ArrowState.Idle)
                    {
                        kvp.Value.UpdateLine();
                        kvp.Value.FadeIn(_fadeInDuration);
                    }
                }
            }
        }

        private void HideAllLines()
        {
            foreach (var kvp in _guideLines)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.FadeOut(_fadeOutDuration);
                }
            }
        }
    }
}
