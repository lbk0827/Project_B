using UnityEngine;
using Game.Utilities;

namespace Game.Core
{
    /// <summary>
    /// SFX 사운드 매니저 - 효과음 재생 담당
    /// SingletonMono 기반으로 씬 전환 시에도 유지
    /// </summary>
    public class SFXManager : SingletonMono<SFXManager>
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("SFX 클립")]
        [SerializeField] private AudioClip _balloonPop;
        [SerializeField] private AudioClip _balloonPick;
        [SerializeField] private AudioClip _balloonHit;

        // ========== 내부 상태 변수 ==========
        private AudioSource _audioSource;

        // ========== 유니티 라이프사이클 ==========
        protected override void Awake()
        {
            CleanupCliplessDuplicates();
            base.Awake();
        }

        protected override void OnAwake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.volume = 1f;
            _audioSource.spatialBlend = 0f;
            _audioSource.ignoreListenerPause = true;

            PreloadClip(_balloonPop);
            PreloadClip(_balloonPick);
            PreloadClip(_balloonHit);

            Debug.Log($"[SFXManager] Ready on '{name}' pop={_balloonPop != null} pick={_balloonPick != null} hit={_balloonHit != null}");
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 풍선 팝 사운드 재생
        /// </summary>
        public void PlayBalloonPop()
        {
            PlayClip(_balloonPop);
        }

        /// <summary>
        /// 화살표 선택(픽) 사운드 재생
        /// </summary>
        public void PlayBalloonPick()
        {
            PlayClip(_balloonPick);
        }

        /// <summary>
        /// 화살표가 풍선에 닿는 사운드 재생
        /// </summary>
        public void PlayBalloonHit()
        {
            PlayClip(_balloonHit);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || _audioSource == null)
            {
                Debug.LogWarning($"[SFXManager] Skip play clip. clipNull={clip == null}, sourceNull={_audioSource == null}, object='{name}'");
                return;
            }

            if (!clip.preloadAudioData && clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }

            Debug.Log($"[SFXManager] Play '{clip.name}' state={clip.loadState} sourceEnabled={_audioSource.enabled} volume={_audioSource.volume}");
            _audioSource.PlayOneShot(clip);
        }

        private void CleanupCliplessDuplicates()
        {
            if (!HasAssignedClips())
                return;

            var managers = FindObjectsByType<SFXManager>();
            foreach (var manager in managers)
            {
                if (manager == null || manager == this)
                    continue;

                if (!manager.HasAssignedClips())
                {
                    Debug.LogWarning($"[SFXManager] Removing clipless duplicate '{manager.name}' so scene instance can take over.");
                    Destroy(manager.gameObject);
                }
            }
        }

        private static void PreloadClip(AudioClip clip)
        {
            if (clip == null)
                return;

            if (!clip.preloadAudioData && clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
        }

        private bool HasAssignedClips()
        {
            return _balloonPop != null || _balloonPick != null || _balloonHit != null;
        }
    }
}
