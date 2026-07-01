using UnityEngine;
using Game.Data;
using Game.Grid;

namespace Game.Core
{
    /// <summary>
    /// 게임 부트스트랩 - 초기 설정 및 시작
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("필수 참조")]
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private LevelLoader _levelLoader;

        [Header("디버그")]
        [SerializeField] private bool _autoStart = true;
        [SerializeField] private int _startLevelId = 1;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void Start()
        {
            if (_autoStart)
            {
                StartGame();
            }
        }

        // ========== 공개 인터페이스 ==========
        public void StartGame()
        {
            // 레벨 로더가 자동으로 첫 레벨 시작
            if (_levelLoader != null)
            {
                _levelLoader.LoadAndStartLevel(_startLevelId);
            }
        }

        public void RestartLevel()
        {
            if (_gameManager != null)
            {
                _gameManager.RestartLevel();
            }
        }
    }
}