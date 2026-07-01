using System;
using UnityEngine;
using Game.Data;

namespace Game.Balloon
{
    /// <summary>
    /// 풍선 컨트롤러 - 개별 풍선 오브젝트 제어
    /// </summary>
    public class BalloonController : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("필수 참조")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private ParticleSystem _popEffect;

        [Header("설정값")]
        [SerializeField] private float _popDuration = 0.3f;
        [SerializeField] private float _scaleOnPop = 1.3f;

        // ========== 내부 상태 변수 ==========
        private int _id;
        private int _slotIndex;
        private GameColor _color;
        private BalloonState _state;

        // ========== 이벤트 ==========
        public event Action<BalloonController> OnPopped;

        // ========== 프로퍼티 ==========
        public int Id => _id;
        public int SlotIndex => _slotIndex;
        public GameColor Color => _color;
        public BalloonState State => _state;

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 풍선 초기화
        /// </summary>
        public void Initialize(BalloonData data, Vector2 position)
        {
            _id = data.id;
            _slotIndex = data.slotIndex;
            _color = data.color;
            _state = BalloonState.Active;

            transform.position = position;
            UpdateVisual();
        }

        /// <summary>
        /// 타겟팅 상태로 변경
        /// </summary>
        public void SetTargeted()
        {
            if (_state != BalloonState.Active)
                return;

            _state = BalloonState.Targeted;
            // 타겟팅 시 약간 확대
            transform.localScale = Vector3.one * 1.1f;
        }

        /// <summary>
        /// 풍선 터뜨리기
        /// </summary>
        public void Pop()
        {
            if (_state == BalloonState.Popping || _state == BalloonState.Popped)
                return;

            _state = BalloonState.Popping;
            StartCoroutine(PopCoroutine());
        }

        // ========== 내부 유틸리티 ==========
        private System.Collections.IEnumerator PopCoroutine()
        {
            // 스케일 업 애니메이션
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = Vector3.one * _scaleOnPop;

            while (elapsed < _popDuration * 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (_popDuration * 0.5f);
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            // 이펙트 재생
            if (_popEffect != null)
            {
                var main = _popEffect.main;
                main.startColor = GetUnityColor(_color);
                _popEffect.Play();
            }

            // 스프라이트 숨기기
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = false;
            }

            // 잠시 대기 후 완료
            yield return new WaitForSeconds(_popDuration * 0.5f);

            _state = BalloonState.Popped;
            OnPopped?.Invoke(this);
        }

        private void UpdateVisual()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = GetUnityColor(_color);
            }

            if (_popEffect != null)
            {
                var main = _popEffect.main;
                main.startColor = GetUnityColor(_color);
            }
        }

        private UnityEngine.Color GetUnityColor(GameColor gameColor)
        {
            return gameColor switch
            {
                GameColor.Red => new UnityEngine.Color(0.9f, 0.2f, 0.2f),
                GameColor.Blue => new UnityEngine.Color(0.2f, 0.4f, 0.9f),
                GameColor.Green => new UnityEngine.Color(0.2f, 0.8f, 0.3f),
                GameColor.Yellow => new UnityEngine.Color(0.95f, 0.85f, 0.2f),
                GameColor.Purple => new UnityEngine.Color(0.7f, 0.3f, 0.9f),
                _ => UnityEngine.Color.white
            };
        }
    }
}