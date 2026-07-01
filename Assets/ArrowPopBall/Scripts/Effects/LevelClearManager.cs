using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace Game.Effects
{
    /// <summary>
    /// Level Clear 연출 전체를 관리하는 매니저
    ///
    /// 연출 시퀀스:
    /// 1. Dot Matrix Pulse (중앙 → 외곽 웨이브 + 색상 변화)
    /// 2. Praise Text (칭찬 문구)
    /// 3. Confetti Effect (축하 파티클)
    /// 4. 로비 복귀
    /// </summary>
    public class LevelClearManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static LevelClearManager Instance { get; private set; }

        // ========== 인스펙터 노출 변수 ==========
        [Header("Effect References")]
        [SerializeField] private DotMatrixPulseEffect _dotMatrixEffect;
        [SerializeField] private PraiseTextEffect _praiseTextEffect;
        [SerializeField] private ConfettiEffect _confettiEffect;

        [Header("Timing Settings")]
        [SerializeField] private float _praiseDelayFromPulseStart = 0.5f;
        [SerializeField] private float _confettiDelayAfterPraise = 0.5f;
        [SerializeField] private float _returnToLobbyDelay = 1.5f;  // 연출을 충분히 감상할 수 있도록 딜레이 증가

        [Header("UI References (CanvasGroup)")]
        [SerializeField] private CanvasGroup _targetAreaUI;
        [SerializeField] private CanvasGroup _topBarUI;
        [SerializeField] private CanvasGroup _bottomBarUI;

        [Header("UI Animation")]
        [SerializeField] private float _uiFadeDuration = 0.3f;

        // ========== 내부 상태 변수 ==========
        private bool _isPlaying;
        private Coroutine _sequenceCoroutine;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 자동 참조 찾기 (인스펙터에서 할당되지 않은 경우)
            AutoFindReferences();
        }

        private void AutoFindReferences()
        {
            if (_dotMatrixEffect == null)
            {
                _dotMatrixEffect = GetComponentInChildren<DotMatrixPulseEffect>(true);
            }

            if (_confettiEffect == null)
            {
                _confettiEffect = GetComponentInChildren<ConfettiEffect>(true);
            }

            if (_praiseTextEffect == null)
            {
                _praiseTextEffect = FindObjectOfType<PraiseTextEffect>(true);
            }

            Debug.Log($"[LevelClearManager] References: Dot={_dotMatrixEffect != null}, Confetti={_confettiEffect != null}, Praise={_praiseTextEffect != null}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// Level Clear 연출 시작
        /// </summary>
        /// <param name="onComplete">연출 완료 후 콜백</param>
        public void StartClearSequence(System.Action onComplete = null)
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[LevelClearManager] Clear sequence already playing!");
                return;
            }

            Debug.Log("[LevelClearManager] Starting clear sequence...");
            _isPlaying = true;

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
            }

            _sequenceCoroutine = StartCoroutine(ClearSequenceCoroutine(onComplete));
        }

        /// <summary>
        /// 연출 중단 (씬 전환 시 등)
        /// </summary>
        public void StopSequence()
        {
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            _isPlaying = false;

            // 각 이펙트 정리
            _dotMatrixEffect?.Stop();
            _praiseTextEffect?.Hide();
            _confettiEffect?.Stop();
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// 게임 UI 숨김 (클리어 연출 시작 시)
        /// </summary>
        private void HideGameUI()
        {
            Debug.Log("[LevelClearManager] Hiding game UI...");

            if (_targetAreaUI != null)
            {
                _targetAreaUI.DOFade(0f, _uiFadeDuration).SetEase(Ease.OutCubic);
                _targetAreaUI.interactable = false;
                _targetAreaUI.blocksRaycasts = false;
            }

            if (_topBarUI != null)
            {
                _topBarUI.DOFade(0f, _uiFadeDuration).SetEase(Ease.OutCubic);
                _topBarUI.interactable = false;
                _topBarUI.blocksRaycasts = false;
            }

            if (_bottomBarUI != null)
            {
                _bottomBarUI.DOFade(0f, _uiFadeDuration).SetEase(Ease.OutCubic);
                _bottomBarUI.interactable = false;
                _bottomBarUI.blocksRaycasts = false;
            }
        }

        private IEnumerator ClearSequenceCoroutine(System.Action onComplete)
        {
            // 게임 UI 숨김 (클리어 연출 시작과 동시에)
            HideGameUI();

            Debug.Log("[LevelClearManager] Stage 1: Starting Dot Matrix Pulse");

            // Stage 1-3: Dot Matrix Pulse 시작
            float pulseDuration = 0f;
            if (_dotMatrixEffect != null)
            {
                _dotMatrixEffect.Play();
                pulseDuration = _dotMatrixEffect.GetTotalDuration();
            }

            // Stage 4: Praise Text (Pulse 진행 중에 표시)
            yield return new WaitForSeconds(_praiseDelayFromPulseStart);

            Debug.Log("[LevelClearManager] Stage 2: Showing Praise Text");
            if (_praiseTextEffect != null)
            {
                _praiseTextEffect.Show();
            }

            // Stage 5: Confetti (Praise Text 이후)
            yield return new WaitForSeconds(_confettiDelayAfterPraise);

            Debug.Log("[LevelClearManager] Stage 3: Playing Confetti");
            float confettiDuration = 0f;
            if (_confettiEffect != null)
            {
                _confettiEffect.Play();
                confettiDuration = _confettiEffect.Duration;
            }

            // Pulse와 Confetti 중 더 긴 것이 끝날 때까지 대기
            float remainingPulseTime = Mathf.Max(0, pulseDuration - _praiseDelayFromPulseStart - _confettiDelayAfterPraise);
            float waitTime = Mathf.Max(remainingPulseTime, confettiDuration);

            yield return new WaitForSeconds(waitTime);

            // Stage 6: 추가 딜레이 (연출 감상 시간)
            yield return new WaitForSeconds(_returnToLobbyDelay);

            // Stage 7: Praise Text 페이드 아웃 (Lobby 복귀 직전)
            Debug.Log("[LevelClearManager] Stage 4: Fading out Praise Text");
            float praiseOutDuration = 0f;
            if (_praiseTextEffect != null && _praiseTextEffect.IsShowing)
            {
                _praiseTextEffect.FadeOut();
                praiseOutDuration = _praiseTextEffect.FadeOutDuration;
            }

            // 페이드 아웃 완료 대기
            yield return new WaitForSeconds(praiseOutDuration);

            Debug.Log("[LevelClearManager] Clear sequence complete!");
            _isPlaying = false;
            _sequenceCoroutine = null;

            onComplete?.Invoke();
        }

        /// <summary>
        /// 연출이 진행 중인지 확인
        /// </summary>
        public bool IsPlaying => _isPlaying;
    }
}