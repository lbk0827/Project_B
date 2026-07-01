using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// Confetti 이펙트
    /// 레벨 클리어 시 축하 파티클 재생
    /// CelebrationController 스타일: BurstLeft + BurstRight + Falling
    /// </summary>
    public class ConfettiEffect : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem _burstLeftParticle;
        [SerializeField] private ParticleSystem _burstRightParticle;
        [SerializeField] private ParticleSystem _fallingParticle;

        [Header("Settings")]
        [SerializeField] private float _duration = 3f;
        [SerializeField] private float _fallingStartDelay = 0.05f;
        [SerializeField] private bool _autoCreateIfMissing = true;

        [Header("Burst Settings")]
        [SerializeField] private int _burstParticleCount = 50;
        [SerializeField] private float _burstSpeed = 12f;
        [SerializeField] private Vector3 _burstLeftPosition = new Vector3(-4f, -2f, 0);
        [SerializeField] private Vector3 _burstRightPosition = new Vector3(4f, -2f, 0);

        [Header("Falling Settings")]
        [SerializeField] private int _fallingParticleCount = 30;
        [SerializeField] private float _fallingSpeed = 8f;
        [SerializeField] private float _fallingGravity = 0.8f;
        [SerializeField] private Vector3 _fallingPosition = new Vector3(0, 8f, 0);

        // ========== 내부 상태 변수 ==========
        private bool _isPlaying;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (_autoCreateIfMissing)
            {
                CreateParticlesIfMissing();
            }

            // 초기 상태: 모든 파티클 비활성화 (Play On Awake 방지)
            StopAndHideAllParticles();
        }

        private void StopAndHideAllParticles()
        {
            StopAndHideParticle(_burstLeftParticle);
            StopAndHideParticle(_burstRightParticle);
            StopAndHideParticle(_fallingParticle);
        }

        private void StopAndHideParticle(ParticleSystem particle)
        {
            if (particle != null)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.gameObject.SetActive(false);
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// Confetti 재생 (BurstLeft + BurstRight 먼저, Falling은 딜레이 후)
        /// </summary>
        public void Play()
        {
            if (_isPlaying) return;

            Debug.Log("[ConfettiEffect] Playing confetti!");
            _isPlaying = true;

            // 카메라 뷰포트 기준으로 파티클 위치 조정
            PositionParticlesRelativeToCamera();

            // 1. Burst 이펙트 즉시 재생
            PlayBurstEffects();

            // 2. Falling 이펙트 딜레이 후 재생
            Invoke(nameof(PlayFallingEffect), _fallingStartDelay);

            // 3. Duration 후 자동 중지
            Invoke(nameof(OnParticleComplete), _duration);
        }

        /// <summary>
        /// Confetti 중지
        /// </summary>
        public void Stop()
        {
            CancelInvoke(nameof(PlayFallingEffect));
            CancelInvoke(nameof(OnParticleComplete));

            StopParticle(_burstLeftParticle);
            StopParticle(_burstRightParticle);
            StopParticle(_fallingParticle);

            _isPlaying = false;
        }

        /// <summary>
        /// 재생 중인지 확인
        /// </summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// 지속 시간
        /// </summary>
        public float Duration => _duration;

        // ========== 내부 유틸리티 ==========

        private void PlayBurstEffects()
        {
            PlayParticle(_burstLeftParticle);
            PlayParticle(_burstRightParticle);
        }

        private void PlayFallingEffect()
        {
            if (_isPlaying)
            {
                PlayParticle(_fallingParticle);
            }
        }

        private void PlayParticle(ParticleSystem particle)
        {
            if (particle != null)
            {
                particle.gameObject.SetActive(true);
                particle.Play();
            }
        }

        private void StopParticle(ParticleSystem particle)
        {
            if (particle != null)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnParticleComplete()
        {
            _isPlaying = false;
            Debug.Log("[ConfettiEffect] Confetti complete");
        }

        /// <summary>
        /// 카메라 뷰포트 기준으로 파티클 위치 조정
        /// </summary>
        private void PositionParticlesRelativeToCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[ConfettiEffect] Main camera not found!");
                return;
            }

            // 카메라 뷰포트 경계 계산 (Z는 카메라로부터의 거리)
            float zDistance = Mathf.Abs(cam.transform.position.z);

            // 화면 좌하단, 우하단, 상단 중앙의 월드 좌표 계산
            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, zDistance));
            Vector3 bottomRight = cam.ViewportToWorldPoint(new Vector3(1f, 0f, zDistance));
            Vector3 topCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, zDistance));
            Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, zDistance));

            // Burst Left: 화면 좌하단에서 약간 위
            if (_burstLeftParticle != null)
            {
                Vector3 leftPos = cam.ViewportToWorldPoint(new Vector3(0.1f, 0.2f, zDistance));
                _burstLeftParticle.transform.position = leftPos;
            }

            // Burst Right: 화면 우하단에서 약간 위
            if (_burstRightParticle != null)
            {
                Vector3 rightPos = cam.ViewportToWorldPoint(new Vector3(0.9f, 0.2f, zDistance));
                _burstRightParticle.transform.position = rightPos;
            }

            // Falling: 화면 상단 (화면 안쪽에서 바로 보이도록)
            if (_fallingParticle != null)
            {
                Vector3 topPos = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.95f, zDistance));
                _fallingParticle.transform.position = topPos;

                // Falling Shape 크기도 화면 너비에 맞게 조정
                float screenWidth = bottomRight.x - bottomLeft.x;
                var shape = _fallingParticle.shape;
                shape.scale = new Vector3(screenWidth * 1.2f, 0.5f, 1f);
            }

            Debug.Log($"[ConfettiEffect] Positioned particles relative to camera at Z={zDistance}");
        }

        /// <summary>
        /// 파티클 시스템이 없을 경우 자동 생성
        /// </summary>
        private void CreateParticlesIfMissing()
        {
            if (_burstLeftParticle == null)
            {
                _burstLeftParticle = CreateBurstParticle("ConfettiBurstLeft", _burstLeftPosition, 45f);
            }

            if (_burstRightParticle == null)
            {
                _burstRightParticle = CreateBurstParticle("ConfettiBurstRight", _burstRightPosition, -45f);
            }

            if (_fallingParticle == null)
            {
                _fallingParticle = CreateFallingParticle("ConfettiFalling", _fallingPosition);
            }

            Debug.Log("[ConfettiEffect] All confetti particle systems created");
        }

        /// <summary>
        /// Burst 파티클 생성 (좌/우 폭죽 스타일)
        /// </summary>
        private ParticleSystem CreateBurstParticle(string name, Vector3 position, float rotationY)
        {
            GameObject particleGO = new GameObject(name);
            particleGO.transform.SetParent(transform);
            particleGO.transform.localPosition = position;
            particleGO.transform.localRotation = Quaternion.Euler(-60f, rotationY, 0f);

            ParticleSystem ps = particleGO.AddComponent<ParticleSystem>();

            // Main Module
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(_burstSpeed * 0.8f, _burstSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.gravityModifier = 0.8f;
            main.maxParticles = _burstParticleCount * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = CreateRandomColorGradient();

            // Emission (Burst)
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, _burstParticleCount)
            });

            // Shape (Cone - 폭죽처럼 퍼지는 형태)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.5f;

            // Color over Lifetime (페이드 아웃)
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateFadeOutGradient();

            // Velocity over Lifetime (약간의 흔들림)
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            // Rotation over Lifetime
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

            // Renderer
            var renderer = particleGO.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            particleGO.SetActive(false);
            return ps;
        }

        /// <summary>
        /// Falling 파티클 생성 (위에서 아래로 내리는 스타일)
        /// </summary>
        private ParticleSystem CreateFallingParticle(string name, Vector3 position)
        {
            GameObject particleGO = new GameObject(name);
            particleGO.transform.SetParent(transform);
            particleGO.transform.localPosition = position;
            particleGO.transform.localRotation = Quaternion.identity;

            ParticleSystem ps = particleGO.AddComponent<ParticleSystem>();

            // Main Module
            var main = ps.main;
            main.duration = _duration - _fallingStartDelay;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(_fallingSpeed * 0.5f, _fallingSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.gravityModifier = _fallingGravity;
            main.maxParticles = _fallingParticleCount * 3;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = CreateRandomColorGradient();

            // Emission (지속적으로 방출)
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = _fallingParticleCount;

            // Shape (넓은 Box에서 아래로)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(10f, 0.5f, 1f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            // Color over Lifetime (페이드 아웃)
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateFadeOutGradient();

            // Velocity over Lifetime (좌우 흔들림 - 나뭇잎처럼)
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            // Rotation over Lifetime
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

            // Size over Lifetime
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.8f);
            sizeCurve.AddKey(0.3f, 1f);
            sizeCurve.AddKey(1f, 0.6f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Renderer
            var renderer = particleGO.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            particleGO.SetActive(false);
            return ps;
        }

        /// <summary>
        /// 랜덤 밝은 색상 그라디언트 생성
        /// </summary>
        private ParticleSystem.MinMaxGradient CreateRandomColorGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f),      // Red
                    new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.2f),    // Yellow
                    new GradientColorKey(new Color(0.3f, 1f, 0.3f), 0.4f),    // Green
                    new GradientColorKey(new Color(0.3f, 0.8f, 1f), 0.6f),    // Cyan
                    new GradientColorKey(new Color(0.6f, 0.3f, 1f), 0.8f),    // Purple
                    new GradientColorKey(new Color(1f, 0.4f, 0.7f), 1f)       // Pink
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>
        /// 페이드 아웃 그라디언트 생성
        /// </summary>
        private Gradient CreateFadeOutGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }
    }
}