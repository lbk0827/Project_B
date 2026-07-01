using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Grid;
using Game.Arrow;
using Game.Balloon;
using Game.Utilities;
using Game.UI;
using Game.GuideLine;

namespace Game.Core
{
    /// <summary>
    /// 게임 매니저 - 전체 게임 플로우 관리
    /// HomingArrow 스폰 로직은 HomingArrowSpawner로 분리됨
    /// 씬 종속적 매니저 (DontDestroyOnLoad 사용 안 함)
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ========== 싱글톤 (씬 종속적) ==========
        private static GameManager _instance;
        public static GameManager Instance => _instance;

        // ========== 인스펙터 노출 변수 ==========
        [Header("프리팹")]
        [SerializeField] private GameObject _arrowPrefab;
        [SerializeField] private GameObject _balloonPrefab;

        [Header("참조")]
        [SerializeField] private Transform _arrowContainer;
        [SerializeField] private Transform _balloonContainer;
        [SerializeField] private Transform _balloonSlotContainer;
        [SerializeField] private HomingArrowSpawner _homingArrowSpawner;

        [Header("설정")]
        [SerializeField] private float _balloonSpacing = 1.2f;

        [Header("레벨 시작 연출")]
        [SerializeField] private bool _enableArrowAppearAnimation = true;
        [SerializeField] private float _arrowAppearStaggerDelay = 0.05f;

        [Header("UI")]
        [SerializeField] private TargetAreaUI _targetAreaUI;

        [Header("레벨 설정")]
        [SerializeField] private LevelConfig _levelConfig;

        [Header("자동 배속")]
        [SerializeField] private bool _enableAutoSpeedUp = true;
        [SerializeField] private float _speedUpMultiplier = 2f;

        [Header("가이드라인")]
        [SerializeField] private GuideLineManager _guideLineManager;

        [Header("Arrow Dash")]
        [SerializeField] private ArrowDashManager _arrowDashManager;

        [Header("Hint")]
        [SerializeField] private HintManager _hintManager;

        [Header("Eraser")]
        [SerializeField] private EraserManager _eraserManager;



        // ========== 내부 상태 변수 ==========
        private GameState _gameState;
        private LevelData _currentLevel;
        private List<ArrowController> _arrows = new List<ArrowController>();
        private List<BalloonController> _balloons = new List<BalloonController>();
        private int _moveCount;
        private int _poppedCount;
        private int _maxLives;
        private int _currentLives;
        private bool _isSpeedUp = false;

        // ========== 이벤트 ==========
        public event Action<GameState> OnGameStateChanged;
        public event Action<int> OnMoveCountChanged;
        public event Action<int, int> OnBalloonPopped; // (poppedCount, totalCount)
        public event Action<bool, int> OnLevelComplete; // (success, stars)
        public event Action<int, int> OnLivesChanged; // (currentLives, maxLives)

        // ========== 프로퍼티 ==========
        public GameState State => _gameState;
        public int MoveCount => _moveCount;
        public int PoppedCount => _poppedCount;
        public int TotalBalloons => _balloons.Count;
        public int CurrentLives => _currentLives;
        public int MaxLives => _maxLives;
        public LevelData CurrentLevelData => _currentLevel;
        public LevelConfig LevelConfig => _levelConfig;
        public List<ArrowController> Arrows => _arrows;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            // 씬 종속적 싱글톤 설정
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[GameManager] Duplicate instance found, destroying...");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // HomingArrowSpawner 이벤트 구독
            if (_homingArrowSpawner != null)
            {
                _homingArrowSpawner.OnHomingHitPosition += HandleHomingHitPosition;
            }
        }

        private void OnDestroy()
        {
            // 싱글톤 인스턴스 정리
            if (_instance == this)
            {
                _instance = null;
            }

            // 이벤트 구독 해제
            if (_homingArrowSpawner != null)
            {
                _homingArrowSpawner.OnHomingHitPosition -= HandleHomingHitPosition;
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 레벨 번호로 레벨 로드
        /// StageTable.json 전용 (Stage Asset 기반)
        /// </summary>
        public void LoadLevel(int levelNumber)
        {
            // StageTable.json에서 로드 (유일한 소스)
            LevelData levelData = StageTableLoader.LoadLevelData(levelNumber);
            if (levelData != null)
            {
                Debug.Log($"[GameManager] Loading level {levelNumber} from StageTable.json (Stage_{StageTableLoader.GetRecord(levelNumber)?.StageIdx:D3})");
                LoadLevel(levelData);
                return;
            }

            // 실패 시 에러 로그
            Debug.LogError($"[GameManager] Level {levelNumber} not found in StageTable.json!");
        }

        /// <summary>
        /// 레벨 로드 및 시작
        /// </summary>
        public void LoadLevel(LevelData levelData)
        {
            _currentLevel = levelData;
            SetGameState(GameState.Loading);

            ClearLevel();
            InitializeGrid();

            // 카메라 크기 자동 조절
            AdjustCameraToLevel();

            // HomingArrowSpawner 초기화
            if (_homingArrowSpawner != null)
            {
                _homingArrowSpawner.Initialize(_targetAreaUI);
            }

            SpawnArrows();
            SpawnBalloons();

            // 가이드라인 초기화
            Debug.Log($"[GameManager] GuideLineManager ref: {_guideLineManager != null}, arrows count: {_arrows.Count}");
            if (_guideLineManager != null)
            {
                _guideLineManager.InitializeForLevel(_arrows);
            }
            else
            {
                Debug.LogWarning("[GameManager] GuideLineManager is NULL - not assigned in Inspector!");
            }

            // Arrow Dash 초기화
            if (_arrowDashManager != null)
            {
                _arrowDashManager.Initialize(_arrows);

                // CameraController 참조 전달
                var cameraController = Camera.main?.GetComponent<CameraController>();
                if (cameraController != null)
                {
                    _arrowDashManager.SetCameraController(cameraController);
                }
            }

            // Hint 초기화
            if (_hintManager != null)
            {
                _hintManager.Initialize(_arrows);
            }

            // Eraser 초기화
            if (_eraserManager != null)
            {
                _eraserManager.Initialize(_arrows);
            }



            // 풍선 UI 초기화 (화살표 색상 기반)
            InitializeTargetAreaUI();

            _moveCount = 0;
            _poppedCount = 0;

            // 배속 리셋
            ResetSpeed();

            // Life 초기화
            _maxLives = _currentLevel.maxLives;
            _currentLives = _maxLives;
            OnLivesChanged?.Invoke(_currentLives, _maxLives);

            SetGameState(GameState.Ready);
            SetGameState(GameState.Playing);
        }

        /// <summary>
        /// 현재 레벨 재시작
        /// </summary>
        public void RestartLevel()
        {
            if (_currentLevel != null)
            {
                LoadLevel(_currentLevel);
            }
        }

        /// <summary>
        /// Life 풀 회복 후 현재 상태에서 게임 계속
        /// Play On 버튼 (광고 시청 후) 용도
        /// </summary>
        public void ContinueWithFullLife()
        {
            _currentLives = _maxLives;
            OnLivesChanged?.Invoke(_currentLives, _maxLives);

            // 게임 상태를 Playing으로 복원
            SetGameState(GameState.Playing);

            Debug.Log($"[GameManager] ContinueWithFullLife - Lives restored to {_currentLives}/{_maxLives}");
        }

        /// <summary>
        /// 타겟팅 시스템 - 동일 색상의 가장 가까운 풍선 찾기
        /// </summary>
        public BalloonController FindTargetBalloon(Vector2 position, GameColor color)
        {
            BalloonController closest = null;
            float closestDistance = float.MaxValue;

            foreach (var balloon in _balloons)
            {
                if (balloon.Color != color)
                    continue;
                if (balloon.State != BalloonState.Active)
                    continue;

                float dist = Vector2.Distance(position, balloon.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closest = balloon;
                }
            }

            return closest;
        }

        // ========== 내부 유틸리티 ==========
        private void SetGameState(GameState newState)
        {
            if (_gameState == newState)
                return;

            _gameState = newState;
            OnGameStateChanged?.Invoke(_gameState);
        }

        private void ClearLevel()
        {
            // 가이드라인 정리
            if (_guideLineManager != null)
            {
                _guideLineManager.ClearAll();
            }

            // 화살표 정리
            foreach (var arrow in _arrows)
            {
                if (arrow != null)
                    Destroy(arrow.gameObject);
            }
            _arrows.Clear();

            // 풍선 정리
            foreach (var balloon in _balloons)
            {
                if (balloon != null)
                    Destroy(balloon.gameObject);
            }
            _balloons.Clear();

            // 그리드 점유 상태 초기화
            if (GridSystem.Instance != null)
            {
                GridSystem.Instance.ClearAllOccupied();
            }
        }

        private void InitializeGrid()
        {
            GridSystem.Instance.Initialize(_currentLevel.gridWidth, _currentLevel.gridHeight);
        }

        private void AdjustCameraToLevel()
        {
            if (_currentLevel == null)
                return;

            var cameraController = Camera.main?.GetComponent<CameraController>();
            if (cameraController != null)
            {
                cameraController.AdjustToGrid(
                    _currentLevel.gridWidth,
                    _currentLevel.gridHeight,
                    GridSystem.Instance.CellSize
                );
            }
        }

        private void SpawnArrows()
        {
            if (_arrowPrefab == null || _currentLevel.arrows == null)
                return;

            foreach (var arrowData in _currentLevel.arrows)
            {
                Vector2 worldPos = GridSystem.Instance.GridToWorld(arrowData.StartPosition);
                var arrowObj = Instantiate(_arrowPrefab, worldPos, Quaternion.identity, _arrowContainer);
                var arrowController = arrowObj.GetComponent<ArrowController>();

                if (arrowController != null)
                {
                    arrowController.Initialize(arrowData);
                    arrowController.OnExtracted += HandleArrowExtracted;
                    arrowController.OnExtractionStarted += HandleArrowExtractionStarted;
                    arrowController.OnCollided += HandleArrowCollided;
                    _arrows.Add(arrowController);

                    // 화살표가 차지하는 위치에만 Dot 표시
                    GridSystem.Instance.ShowDotsAt(arrowController.GetOccupiedPositions());
                }
            }

            // 등장 연출 실행
            if (_enableArrowAppearAnimation && _arrows.Count > 0)
            {
                StartCoroutine(PlayArrowAppearSequence());
            }
        }

        private System.Collections.IEnumerator PlayArrowAppearSequence()
        {
            // 모든 Arrow 숨김
            foreach (var arrow in _arrows)
            {
                arrow.HideImmediate();
            }

            // 순차적으로 등장 연출
            float delay = 0f;
            foreach (var arrow in _arrows)
            {
                arrow.PlayAppearAnimation(delay);
                delay += _arrowAppearStaggerDelay;
            }

            yield return null;
        }

        private void SpawnBalloons()
        {
            // 월드 공간 풍선 생성은 비활성화 (UI 풍선만 사용)
            // TargetAreaUI.InitializeBalloons()에서 UI 풍선을 생성함
        }

        private void InitializeTargetAreaUI()
        {
            if (_targetAreaUI != null)
            {
                _targetAreaUI.InitializeBalloons(_arrows);
            }
        }

        // ========== Arrow 이벤트 핸들러 ==========

        /// <summary>
        /// Arrow 탈출 시작 시 HomingArrowSpawner에 위임
        /// </summary>
        private void HandleArrowExtractionStarted(ArrowController arrow, Vector2 headPosition, ArrowDirection exitDir)
        {
            Debug.Log($"[GameManager] HandleArrowExtractionStarted: color={arrow.Color}, headPos={headPosition}, dir={exitDir}");

            // Move count 증가
            _moveCount++;
            OnMoveCountChanged?.Invoke(_moveCount);

            // Arrow 리스트에서 제거 (HandleArrowExtracted 중복 방지)
            _arrows.Remove(arrow);

            // 배속 조건 체크
            CheckSpeedUpCondition();

            // HomingArrowSpawner에 위임
            if (_homingArrowSpawner != null)
            {
                _homingArrowSpawner.HandleArrowExtractionStarted(arrow, headPosition, exitDir);
            }
        }

        private void HandleArrowExtracted(ArrowController arrow)
        {
            // OnExtractionStarted에서 이미 처리된 경우 중복 방지
            if (!_arrows.Contains(arrow))
            {
                Debug.Log($"[GameManager] HandleArrowExtracted: arrow already removed (handled by OnExtractionStarted)");
                return;
            }

            // 기존 로직 (OnExtractionStarted가 발생하지 않은 경우를 위한 백업)
            _moveCount++;
            OnMoveCountChanged?.Invoke(_moveCount);

            Debug.Log($"[GameManager] HandleArrowExtracted (legacy): color={arrow.Color}");

            // 화살표 색상과 탈출 방향 저장
            GameColor arrowColor = arrow.Color;
            ArrowDirection escapeDirection = arrow.HeadDirection;

            // 원본 화살표 제거
            _arrows.Remove(arrow);
            Destroy(arrow.gameObject);

            // 배속 조건 체크
            CheckSpeedUpCondition();

            // HomingArrowSpawner에 위임
            if (_homingArrowSpawner != null)
            {
                _homingArrowSpawner.SpawnFromOpposite(arrowColor, escapeDirection);
            }
        }

        private void HandleArrowCollided(ArrowController arrow)
        {
            if (_gameState != GameState.Playing)
                return;

            _currentLives--;
            OnLivesChanged?.Invoke(_currentLives, _maxLives);

            Debug.Log($"[GameManager] Arrow collided! Lives: {_currentLives}/{_maxLives}");

            if (_currentLives <= 0)
            {
                // 게임 오버 - 실패 팝업 표시
                SetGameState(GameState.Failed);

                // PopupFailUI 표시
                if (PopupFailUI.Instance != null)
                {
                    PopupFailUI.Instance.Show();
                }
                else
                {
                    // PopupFailUI가 없으면 기존 이벤트 발생
                    OnLevelComplete?.Invoke(false, 0);
                }

                Debug.Log("[GameManager] Game Over - No lives remaining!");
            }
        }

        // ========== HomingArrow 이벤트 핸들러 ==========

        private void HandleHomingHitPosition(HomingArrow homing, GameColor color)
        {
            // UI 풍선 팝
            if (_targetAreaUI != null)
            {
                _targetAreaUI.PopBalloon(color);
            }

            _poppedCount++;
            OnBalloonPopped?.Invoke(_poppedCount, _arrows.Count + _poppedCount);

            Debug.Log($"[GameManager] Balloon popped: {color}, total popped: {_poppedCount}");

            // 승리 조건 체크
            CheckWinConditionForUI();
        }

        // ========== Balloon 이벤트 핸들러 ==========

        private void HandleBalloonPopped(BalloonController balloon)
        {
            _poppedCount++;
            OnBalloonPopped?.Invoke(_poppedCount, _balloons.Count);

            // 풍선 UI도 업데이트
            if (_targetAreaUI != null)
            {
                _targetAreaUI.PopBalloon(balloon.Color);
            }

            // 승리 조건 체크
            CheckWinCondition();
        }

        // ========== 승/패 판정 ==========

        private void CheckWinCondition()
        {
            int activeBalloons = 0;
            foreach (var balloon in _balloons)
            {
                if (balloon.State == BalloonState.Active || balloon.State == BalloonState.Targeted)
                {
                    activeBalloons++;
                }
            }

            if (activeBalloons == 0)
            {
                // 승리
                ResetSpeed();
                int stars = CalculateStars();
                SetGameState(GameState.Clear);
                OnLevelComplete?.Invoke(true, stars);
            }
            else if (_arrows.Count == 0)
            {
                // 화살표가 없는데 풍선이 남음 - 실패
                ResetSpeed();
                SetGameState(GameState.Failed);
                OnLevelComplete?.Invoke(false, 0);
            }
        }

        private void CheckWinConditionForUI()
        {
            if (_targetAreaUI == null)
                return;

            int remainingBalloons = _targetAreaUI.GetTotalRemainingCount();

            if (remainingBalloons == 0)
            {
                // 승리
                ResetSpeed();
                int stars = CalculateStars();
                SetGameState(GameState.Clear);
                OnLevelComplete?.Invoke(true, stars);
                Debug.Log($"[GameManager] Level Clear! Stars: {stars}");
            }
            else if (_arrows.Count == 0)
            {
                // 화살표가 없는데 풍선이 남음 - 실패
                ResetSpeed();
                SetGameState(GameState.Failed);
                OnLevelComplete?.Invoke(false, 0);
                Debug.Log($"[GameManager] Level Failed! Remaining balloons: {remainingBalloons}");
            }
        }

        private int CalculateStars()
        {
            if (_currentLevel == null)
                return 1;

            if (_moveCount <= _currentLevel.starThresholds3)
                return 3;
            if (_moveCount <= _currentLevel.starThresholds2)
                return 2;
            if (_moveCount <= _currentLevel.starThresholds1)
                return 1;

            return 1;
        }

        // ========== 배속 관리 ==========

        private void CheckSpeedUpCondition()
        {
            if (!_enableAutoSpeedUp)
                return;

            // 그리드에 남은 Arrow가 없으면 배속
            if (_arrows.Count == 0 && !_isSpeedUp)
            {
                _isSpeedUp = true;
                Time.timeScale = _speedUpMultiplier;
                Debug.Log($"[GameManager] Speed Up! x{_speedUpMultiplier}");
            }
        }

        private void ResetSpeed()
        {
            _isSpeedUp = false;
            Time.timeScale = 1f;
        }
    }
}