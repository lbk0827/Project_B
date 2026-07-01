using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using Game.Data;
using Game.Grid;
using Game.UI;
using Game.Utilities;

namespace Game.Arrow
{
    /// <summary>
    /// HomingArrow 생성 및 전환 연출 담당
    /// GameManager에서 분리됨
    /// </summary>
    public class HomingArrowSpawner : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("프리팹")]
        [SerializeField] private GameObject _homingArrowPrefab;

        [Header("전환 연출")]
        [SerializeField] private float _transitionDelay = 0.15f;
        [SerializeField] private float _fadeOutDuration = 0.25f;
        [SerializeField] private float _spawnDelay = 0.05f;
        [SerializeField] private float _scaleUpDuration = 0.15f;

        // ========== 참조 ==========
        private TargetAreaUI _targetAreaUI;

        // ========== 이벤트 ==========
        public event Action<HomingArrow, GameColor> OnHomingHitPosition;

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(TargetAreaUI targetAreaUI)
        {
            _targetAreaUI = targetAreaUI;
        }

        /// <summary>
        /// Arrow 탈출 시작 시 호출 - 딜레이 후 HomingArrow로 전환
        /// </summary>
        public void HandleArrowExtractionStarted(ArrowController arrow, Vector2 headPosition, ArrowDirection exitDir)
        {
            Debug.Log($"[HomingArrowSpawner] HandleArrowExtractionStarted: color={arrow.Color}, headPos={headPosition}, dir={exitDir}");
            StartCoroutine(DelayedArrowTransition(arrow, exitDir));
        }

        /// <summary>
        /// 탈출 방향의 반대쪽에서 HomingArrow 생성 (Legacy 방식)
        /// </summary>
        public void SpawnFromOpposite(GameColor color, ArrowDirection escapeDirection)
        {
            if (_targetAreaUI != null && _homingArrowPrefab != null)
            {
                StartCoroutine(SpawnHomingArrowFromOpposite(color, escapeDirection));
            }
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// 딜레이 후 Arrow → HomingArrow 전환
        /// </summary>
        private IEnumerator DelayedArrowTransition(ArrowController arrow, ArrowDirection exitDir)
        {
            // Arrow 정보 미리 저장
            GameColor color = arrow.Color;
            int arrowLength = arrow.TotalLength;
            Vector2 exitDirection = GridSystem.Instance.GetDirectionVector(exitDir);

            // 딜레이 대기 (Arrow는 계속 탈출 방향으로 이동 중)
            yield return WaitForSecondsCache.Get(_transitionDelay);

            // Arrow가 아직 존재하면 현재 위치에서 전환
            if (arrow != null && arrow.gameObject != null)
            {
                // 현재 Arrow Head의 월드 위치 (이동 후 위치)
                Vector2 currentHeadPos = arrow.GetHeadWorldPosition();

                Debug.Log($"[HomingArrowSpawner] DelayedArrowTransition: color={color}, currentPos={currentHeadPos}");

                // Arrow 페이드 아웃 시작
                arrow.StartFadeOutTransition(_fadeOutDuration, () =>
                {
                    if (arrow != null && arrow.gameObject != null)
                    {
                        Destroy(arrow.gameObject);
                    }
                });

                // HomingArrow를 현재 Arrow 위치에서 생성
                if (_targetAreaUI != null && _homingArrowPrefab != null)
                {
                    StartCoroutine(SpawnHomingArrowAtPosition(currentHeadPos, exitDirection, color, arrowLength));
                }
            }
        }

        /// <summary>
        /// Arrow 위치에서 HomingArrow 생성 (전환 연출)
        /// </summary>
        private IEnumerator SpawnHomingArrowAtPosition(
            Vector2 position,
            Vector2 exitDirection,
            GameColor color,
            int arrowLength)
        {
            // 짧은 딜레이 (Arrow 페이드 아웃과 겹치게)
            yield return WaitForSecondsCache.Get(_spawnDelay);

            Vector3 targetPos = _targetAreaUI.GetBalloonWorldPosition(color);
            if (targetPos == Vector3.zero)
            {
                Debug.LogWarning($"[HomingArrowSpawner] No balloon found for color: {color}");
                yield break;
            }

            // HomingArrow 생성
            var homingObj = Instantiate(_homingArrowPrefab, position, Quaternion.identity);
            var homingArrow = homingObj.GetComponent<HomingArrow>();

            if (homingArrow != null)
            {
                // 페이드 인 + 스케일 업 애니메이션
                homingArrow.transform.localScale = Vector3.zero;
                homingArrow.transform.DOScale(1f, _scaleUpDuration).SetEase(Ease.OutBack);

                // Arrow 위치에서 바로 호밍 시작
                homingArrow.StartHomingFromArrowPosition(
                    position,
                    exitDirection,
                    _targetAreaUI,
                    color,
                    arrowLength
                );
                homingArrow.OnHitPosition += HandleHomingHitPosition;

                Debug.Log($"[HomingArrowSpawner] HomingArrow spawned at arrow position: {position}, length: {arrowLength}, color: {color}");
            }
            else
            {
                Debug.LogError($"[HomingArrowSpawner] HomingArrow component not found on prefab!");
            }
        }

        /// <summary>
        /// 탈출 방향의 반대쪽 화면 가장자리에서 호밍 화살표 생성
        /// </summary>
        private IEnumerator SpawnHomingArrowFromOpposite(GameColor color, ArrowDirection escapeDirection)
        {
            // 짧은 딜레이 (화살표가 완전히 사라진 느낌)
            yield return WaitForSecondsCache.Get(_transitionDelay);

            Vector3 targetPos = _targetAreaUI.GetBalloonWorldPosition(color);
            Debug.Log($"[HomingArrowSpawner] Target balloon position: {targetPos}");

            if (targetPos == Vector3.zero)
            {
                Debug.LogWarning($"[HomingArrowSpawner] No balloon found for color: {color}");
                yield break;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[HomingArrowSpawner] Main camera not found!");
                yield break;
            }

            // 탈출 방향의 반대쪽에서 시작 위치 계산
            Vector3 screenStartPos = CalculateOppositeScreenPosition(targetPos, escapeDirection, mainCamera);

            // 스크린 좌표 → 월드 좌표 변환
            float cameraDistance = Mathf.Abs(mainCamera.transform.position.z);
            screenStartPos.z = cameraDistance;
            Vector3 startWorldPos = mainCamera.ScreenToWorldPoint(screenStartPos);
            startWorldPos.z = 0f;

            Debug.Log($"[HomingArrowSpawner] Homing arrow start pos: {startWorldPos}, target: {targetPos}, escape dir: {escapeDirection}");

            // 호밍 화살표 생성
            var homingObj = Instantiate(_homingArrowPrefab, startWorldPos, Quaternion.identity);
            var homingArrow = homingObj.GetComponent<HomingArrow>();

            if (homingArrow != null)
            {
                // TargetAreaUI 참조를 전달하여 카메라 이동 시에도 풍선 위치 추적
                homingArrow.StartHomingToPosition(startWorldPos, _targetAreaUI, color);
                homingArrow.OnHitPosition += HandleHomingHitPosition;
                Debug.Log($"[HomingArrowSpawner] HomingArrow started from opposite of {escapeDirection}: {color} -> {targetPos}");
            }
            else
            {
                Debug.LogError($"[HomingArrowSpawner] HomingArrow component not found on prefab!");
            }
        }

        /// <summary>
        /// 탈출 방향의 반대쪽 화면 가장자리 스크린 좌표 계산
        /// </summary>
        private Vector3 CalculateOppositeScreenPosition(Vector3 targetWorldPos, ArrowDirection escapeDir, Camera camera)
        {
            Vector3 targetScreenPos = camera.WorldToScreenPoint(targetWorldPos);
            float margin = 50f; // 화면 밖으로 약간 벗어나는 여백

            return escapeDir switch
            {
                // 왼쪽으로 탈출 → 오른쪽에서 생성
                ArrowDirection.Left => new Vector3(Screen.width + margin, targetScreenPos.y, 0),

                // 오른쪽으로 탈출 → 왼쪽에서 생성
                ArrowDirection.Right => new Vector3(-margin, targetScreenPos.y, 0),

                // 아래쪽으로 탈출 → 아래쪽에서 생성 (탈출 방향과 동일)
                ArrowDirection.Down => new Vector3(targetScreenPos.x, -margin, 0),

                // 위쪽으로 탈출 → 아래쪽에서 생성
                ArrowDirection.Up => new Vector3(targetScreenPos.x, -margin, 0),

                _ => new Vector3(targetScreenPos.x, -margin, 0)
            };
        }

        private void HandleHomingHitPosition(HomingArrow homing, GameColor color)
        {
            OnHomingHitPosition?.Invoke(homing, color);
        }
    }
}