using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Balloon;
using Game.UI;
using Game.Utilities;
using DG.Tweening;

namespace Game.Arrow
{
    /// <summary>
    /// 호밍 화살표 - 탈출 후 풍선을 향해 날아가는 화살표
    /// </summary>
    public class HomingArrow : MonoBehaviour
    {
        // ========== 파티클 풀 (static) ==========
        private static readonly Dictionary<EntityId, List<ParticleSystem>> _particlePool = new Dictionary<EntityId, List<ParticleSystem>>();

        private static ParticleSystem GetPooledParticle(ParticleSystem prefab, Vector3 position)
        {
            EntityId prefabId = prefab.GetEntityId();
            if (_particlePool.TryGetValue(prefabId, out var pool) && pool.Count > 0)
            {
                var particle = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                if (particle != null)
                {
                    particle.transform.position = position;
                    particle.gameObject.SetActive(true);
                    return particle;
                }
            }
            return Instantiate(prefab, position, Quaternion.identity);
        }

        private static void ReturnParticleToPool(ParticleSystem prefab, ParticleSystem instance)
        {
            if (instance == null) return;
            EntityId prefabId = prefab.GetEntityId();
            if (!_particlePool.TryGetValue(prefabId, out var pool))
            {
                pool = new List<ParticleSystem>();
                _particlePool[prefabId] = pool;
            }
            instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            instance.gameObject.SetActive(false);
            pool.Add(instance);
        }

        // ========== 인스펙터 노출 변수 ==========
        [Header("설정값")]
        [SerializeField] private float _homingSpeed = 15f;
        [SerializeField] private float _curveStrength = 2f;
        [SerializeField] private float _arrivalThreshold = 0.2f;

        [Header("비주얼")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private int _sortingOrder = 100;  // 풍선 UI보다 높은 레이어

        [Header("파티클")]
        [SerializeField] private ParticleSystem _launchParticlePrefab;
        [SerializeField] private ParticleSystem _hitParticlePrefab;

        [Header("애니메이션")]
        [SerializeField] private float _launchScaleDuration = 0.2f;
        [SerializeField] private float _launchScaleMultiplier = 0.5f;  // 시작 시 원래 크기의 50%

        [Header("Arrow 길이 반영")]
        [SerializeField] private float _trailTimePerCell = 0.08f;  // 셀당 Trail 시간
        [SerializeField] private float _baseTrailTime = 0.15f;     // 기본 Trail 시간

        // 원래 스케일 저장
        private Vector3 _originalScale;
        private int _originalArrowLength = 3;  // 원본 Arrow 길이

        // ========== 내부 상태 변수 ==========
        private BalloonController _target;
        private GameColor _color;
        private Vector2 _startPosition;
        private Vector2 _controlPoint;
        private float _progress;
        private bool _isHoming;

        // 위치 기반 호밍용 (UI 풍선)
        private Vector3 _targetPosition;
        private bool _usePositionTarget;
        private TargetAreaUI _targetAreaUI;  // UI 풍선 참조 (동적 위치 업데이트용)

        // ========== 이벤트 ==========
        public event Action<HomingArrow, BalloonController> OnHitTarget;
        public event Action<HomingArrow, GameColor> OnHitPosition;

        // ========== 프로퍼티 ==========
        public GameColor Color => _color;
        public bool IsHoming => _isHoming;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            // 원래 스케일 저장
            _originalScale = transform.localScale;

            // Sorting Order 설정 (풍선 UI보다 위에 표시되도록)
            ApplySortingOrder();
        }

        /// <summary>
        /// SpriteRenderer와 TrailRenderer의 sortingOrder 설정
        /// </summary>
        private void ApplySortingOrder()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = _sortingOrder;
            }

            if (_trailRenderer != null)
            {
                _trailRenderer.sortingOrder = _sortingOrder - 1;  // Trail은 화살표보다 살짝 뒤
            }
        }

        private void Update()
        {
            if (_isHoming)
            {
                if (_usePositionTarget || _target != null)
                {
                    UpdateHoming();
                }
            }
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 호밍 시작
        /// </summary>
        public void StartHoming(Vector2 startPos, BalloonController target, GameColor color)
        {
            _startPosition = startPos;
            _target = target;
            _color = color;
            _progress = 0f;
            _isHoming = true;

            transform.position = startPos;

            // 컨트롤 포인트 계산 (Bezier 커브용)
            Vector2 targetPos = target.transform.position;
            Vector2 midPoint = (_startPosition + targetPos) * 0.5f;
            Vector2 perpendicular = Vector2.Perpendicular((targetPos - _startPosition).normalized);
            _controlPoint = midPoint + perpendicular * _curveStrength;

            // 타겟 마킹
            target.SetTargeted();

            UpdateVisual();

            // 발사 애니메이션 (DOTween)
            PlayLaunchAnimation();

            // 발사 파티클
            SpawnLaunchParticle();
        }

        /// <summary>
        /// 위치 기반 호밍 시작 (UI 풍선용) - TargetAreaUI 참조 버전
        /// 카메라 이동 시에도 풍선 위치를 동적으로 추적
        /// </summary>
        public void StartHomingToPosition(Vector2 startPos, TargetAreaUI targetAreaUI, GameColor color)
        {
            _startPosition = startPos;
            _targetAreaUI = targetAreaUI;
            _usePositionTarget = true;
            _target = null;
            _color = color;
            _progress = 0f;
            _isHoming = true;

            transform.position = startPos;

            // 현재 풍선 위치로 초기 타겟 위치 설정
            _targetPosition = _targetAreaUI.GetBalloonWorldPosition(color);

            // 컨트롤 포인트 계산 (Bezier 커브용)
            Vector2 midPoint = (_startPosition + (Vector2)_targetPosition) * 0.5f;
            Vector2 perpendicular = Vector2.Perpendicular(((Vector2)_targetPosition - _startPosition).normalized);
            _controlPoint = midPoint + perpendicular * _curveStrength;

            // 초기 회전 설정 (타겟 방향)
            Vector2 initialDirection = ((Vector2)_targetPosition - _startPosition).normalized;
            float initialAngle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, initialAngle);

            UpdateVisual();

            // 발사 애니메이션 (DOTween)
            PlayLaunchAnimation();

            // 발사 파티클
            SpawnLaunchParticle();

            Debug.Log($"[HomingArrow] StartHomingToPosition: from {startPos} to {_targetPosition}, color: {color}");
        }

        /// <summary>
        /// 위치 기반 호밍 시작 (UI 풍선용) - 고정 좌표 버전 (레거시)
        /// </summary>
        public void StartHomingToPosition(Vector2 startPos, Vector3 targetPos, GameColor color)
        {
            _startPosition = startPos;
            _targetPosition = targetPos;
            _targetAreaUI = null;
            _usePositionTarget = true;
            _target = null;
            _color = color;
            _progress = 0f;
            _isHoming = true;

            transform.position = startPos;

            // 컨트롤 포인트 계산 (Bezier 커브용)
            Vector2 midPoint = (_startPosition + (Vector2)targetPos) * 0.5f;
            Vector2 perpendicular = Vector2.Perpendicular(((Vector2)targetPos - _startPosition).normalized);
            _controlPoint = midPoint + perpendicular * _curveStrength;

            UpdateVisual();

            // 발사 애니메이션 (DOTween)
            PlayLaunchAnimation();

            // 발사 파티클
            SpawnLaunchParticle();

            Debug.Log($"[HomingArrow] StartHomingToPosition (fixed): from {startPos} to {targetPos}, color: {color}");
        }

        /// <summary>
        /// Arrow 위치에서 바로 시작하는 호밍 (전환 연출용)
        /// Arrow가 탈출하는 위치에서 즉시 HomingArrow로 전환되어 풍선으로 날아감
        /// </summary>
        public void StartHomingFromArrowPosition(
            Vector2 arrowHeadPos,
            Vector2 exitDirection,
            TargetAreaUI targetUI,
            GameColor color,
            int arrowLength)
        {
            _startPosition = arrowHeadPos;
            _targetAreaUI = targetUI;
            _usePositionTarget = true;
            _target = null;
            _color = color;
            _originalArrowLength = arrowLength;
            _progress = 0f;
            _isHoming = true;

            transform.position = arrowHeadPos;

            // Arrow 길이를 Trail 길이에 반영
            SetTrailLength(arrowLength);

            // 현재 풍선 위치로 타겟 설정
            _targetPosition = _targetAreaUI.GetBalloonWorldPosition(color);

            // 컨트롤 포인트: 탈출 방향으로 더 크게 오프셋하여 부드러운 곡선 생성
            Vector2 exitOffset = exitDirection.normalized * _curveStrength * 1.5f;
            Vector2 midPoint = (arrowHeadPos + (Vector2)_targetPosition) * 0.5f;
            _controlPoint = midPoint + exitOffset;

            // 초기 회전: 탈출 방향
            float initialAngle = Mathf.Atan2(exitDirection.y, exitDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, initialAngle);

            UpdateVisual();
            PlayLaunchAnimation();
            SpawnLaunchParticle();

            Debug.Log($"[HomingArrow] StartHomingFromArrowPosition: from {arrowHeadPos} (exit: {exitDirection}), length: {arrowLength}, color: {color}");
        }

        /// <summary>
        /// Trail 길이를 Arrow 길이에 따라 설정
        /// </summary>
        private void SetTrailLength(int arrowLength)
        {
            if (_trailRenderer != null)
            {
                // 길이에 비례한 Trail 시간 설정
                _trailRenderer.time = _baseTrailTime + (arrowLength - 1) * _trailTimePerCell;
            }
        }

        // ========== 내부 유틸리티 ==========
        private void UpdateHoming()
        {
            Vector2 targetPos;

            if (_usePositionTarget)
            {
                // UI 풍선 참조가 있으면 매 프레임 위치 업데이트 (카메라 이동 대응)
                if (_targetAreaUI != null)
                {
                    Vector3 newTargetPos = _targetAreaUI.GetBalloonWorldPosition(_color);
                    if (newTargetPos != Vector3.zero)
                    {
                        _targetPosition = newTargetPos;
                    }
                }
                targetPos = _targetPosition;
            }
            else if (_target != null)
            {
                if (_target.State == BalloonState.Popped)
                {
                    Destroy(gameObject);
                    return;
                }
                targetPos = _target.transform.position;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 진행도 업데이트
            _progress += _homingSpeed * Time.deltaTime / GetPathLength();
            _progress = Mathf.Clamp01(_progress);

            // Bezier 커브 위치 계산 (시작점과 컨트롤 포인트는 고정, 타겟만 동적)
            Vector2 newPos = CalculateBezierPoint(_progress, _startPosition, _controlPoint, targetPos);
            transform.position = newPos;

            // 방향 업데이트 (다음 위치를 향해)
            if (_progress < 1f)
            {
                Vector2 nextPos = CalculateBezierPoint(_progress + 0.05f, _startPosition, _controlPoint, targetPos);
                Vector2 direction = (nextPos - newPos).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            // 도착 체크
            float distance = Vector2.Distance(newPos, targetPos);
            if (distance < _arrivalThreshold || _progress >= 1f)
            {
                HitTarget();
            }
        }

        private void HitTarget()
        {
            _isHoming = false;

            // 충돌 파티클
            SpawnHitParticle();

            if (_usePositionTarget)
            {
                // 위치 기반 호밍: 이벤트로 색상 전달 (UI 풍선 팝은 GameManager에서 처리)
                OnHitPosition?.Invoke(this, _color);
                Debug.Log($"[HomingArrow] Hit position target, color: {_color}");
            }
            else if (_target != null)
            {
                // 기존 BalloonController 기반 호밍
                OnHitTarget?.Invoke(this, _target);
                _target.Pop();
            }

            Destroy(gameObject, 0.1f);
        }

        // ========== 애니메이션 ==========
        private void PlayLaunchAnimation()
        {
            // 시작 스케일: 원래 크기의 일정 비율
            transform.localScale = _originalScale * _launchScaleMultiplier;

            // 스케일 펀치 애니메이션: 작게 → 원래 크기로 복귀 (OutBack으로 약간 튕기는 효과)
            transform.DOScale(_originalScale, _launchScaleDuration)
                .SetEase(Ease.OutBack);
        }

        // ========== 파티클 ==========
        private void SpawnLaunchParticle()
        {
            if (_launchParticlePrefab == null)
                return;

            var particle = GetPooledParticle(_launchParticlePrefab, transform.position);

            var main = particle.main;
            main.startColor = GetUnityColor(_color);

            particle.Play();

            // 파티클 재생 후 풀로 반환
            StartCoroutine(ReturnParticleAfterPlay(_launchParticlePrefab, particle));
        }

        private void SpawnHitParticle()
        {
            if (_hitParticlePrefab == null)
                return;

            var particle = GetPooledParticle(_hitParticlePrefab, transform.position);

            var main = particle.main;
            main.startColor = GetUnityColor(_color);

            particle.Play();

            // HitParticle은 HomingArrow가 곧 파괴되므로 자체 코루틴 사용 불가
            // MonoBehaviour가 없어도 동작하도록 지연 반환 구조 사용
            ScheduleParticleReturn(_hitParticlePrefab, particle);
        }

        private IEnumerator ReturnParticleAfterPlay(ParticleSystem prefab, ParticleSystem instance)
        {
            var main = instance.main;
            yield return WaitForSecondsCache.Get(main.duration + main.startLifetime.constantMax);
            ReturnParticleToPool(prefab, instance);
        }

        private static void ScheduleParticleReturn(ParticleSystem prefab, ParticleSystem instance)
        {
            // ParticleSystem.main의 duration 후 비활성화 콜백 등록
            var main = instance.main;
            float delay = main.duration + main.startLifetime.constantMax;
            // DOTween의 DelayedCall은 MonoBehaviour 독립적으로 동작
            DOVirtual.DelayedCall(delay, () => ReturnParticleToPool(prefab, instance));
        }

        private float GetPathLength()
        {
            Vector2 targetPos;

            if (_usePositionTarget)
            {
                targetPos = _targetPosition;
            }
            else if (_target != null)
            {
                targetPos = _target.transform.position;
            }
            else
            {
                return 1f;
            }

            // 대략적인 경로 길이 계산
            return Vector2.Distance(_startPosition, targetPos) * 1.2f;
        }

        private Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            // Quadratic Bezier curve: B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private void UpdateVisual()
        {
            UnityEngine.Color unityColor = GetUnityColor(_color);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = unityColor;
            }

            if (_trailRenderer != null)
            {
                _trailRenderer.startColor = unityColor;
                _trailRenderer.endColor = new UnityEngine.Color(unityColor.r, unityColor.g, unityColor.b, 0f);
            }
        }

        private UnityEngine.Color GetUnityColor(GameColor gameColor)
        {
            return gameColor switch
            {
                GameColor.Red => new UnityEngine.Color(1f, 0.2f, 0.2f),
                GameColor.Blue => new UnityEngine.Color(0.2f, 0.4f, 1f),
                GameColor.Green => new UnityEngine.Color(0.2f, 0.8f, 0.2f),
                GameColor.Yellow => new UnityEngine.Color(1f, 0.9f, 0.2f),
                GameColor.Purple => new UnityEngine.Color(0.6f, 0.2f, 0.8f),
                GameColor.Orange => new UnityEngine.Color(1f, 0.5f, 0.1f),
                GameColor.Cyan => new UnityEngine.Color(0.2f, 0.9f, 0.9f),
                GameColor.Pink => new UnityEngine.Color(1f, 0.6f, 0.7f),
                GameColor.Brown => new UnityEngine.Color(0.6f, 0.4f, 0.2f),
                GameColor.Lime => new UnityEngine.Color(0.6f, 1f, 0.2f),
                GameColor.Navy => new UnityEngine.Color(0.1f, 0.1f, 0.5f),
                GameColor.Magenta => new UnityEngine.Color(1f, 0.2f, 0.8f),
                _ => UnityEngine.Color.white
            };
        }
    }
}
