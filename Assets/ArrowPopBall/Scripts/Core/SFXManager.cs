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
        protected override void OnAwake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 풍선 팝 사운드 재생
        /// </summary>
        public void PlayBalloonPop()
        {
            if (_balloonPop != null)
                _audioSource.PlayOneShot(_balloonPop);
        }

        /// <summary>
        /// 화살표 선택(픽) 사운드 재생
        /// </summary>
        public void PlayBalloonPick()
        {
            if (_balloonPick != null)
                _audioSource.PlayOneShot(_balloonPick);
        }

        /// <summary>
        /// 화살표가 풍선에 닿는 사운드 재생
        /// </summary>
        public void PlayBalloonHit()
        {
            if (_balloonHit != null)
                _audioSource.PlayOneShot(_balloonHit);
        }
    }
}
