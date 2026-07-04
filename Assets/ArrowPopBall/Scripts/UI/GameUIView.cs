using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Game.Ads;
using Game.Core;
using Game.Data;
using Game.Effects;
using Game.GuideLine;

namespace Game.UI
{
    /// <summary>
    /// 게임 UI 뷰 - 인게임 UI 표시
    /// </summary>
    public class GameUIView : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("Top Bar UI")]
        [SerializeField] private TopBarUI _topBarUI;

        [Header("상단 UI")]
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _moveCountText;
        [SerializeField] private TextMeshProUGUI _balloonCountText;

        [Header("버튼")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _pauseButton;

        [Header("결과 패널")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private TextMeshProUGUI _resultTitleText;
        [SerializeField] private GameObject[] _starObjects;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _retryButton;

        [Header("가이드라인 버튼")]
        [SerializeField] private GuideLineButton _guideLineButton;

        // ========== 유니티 라이프사이클 ==========
        private void Start()
        {
            SetupButtons();
            SubscribeEvents();
            HideResultPanel();
            AdManager.Instance?.ShowIngameBanner();

            // TopBarUI 즉시 초기화 시도 (GameManager가 이미 레벨을 로드한 경우)
            InitializeTopBarUI();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // ========== 내부 유틸리티 ==========
        private void SetupButtons()
        {
            if (_restartButton != null)
                _restartButton.onClick.AddListener(OnRestartClicked);

            if (_nextButton != null)
                _nextButton.onClick.AddListener(OnNextClicked);

            if (_retryButton != null)
                _retryButton.onClick.AddListener(OnRetryClicked);
        }

        private void SubscribeEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMoveCountChanged += UpdateMoveCount;
                GameManager.Instance.OnBalloonPopped += UpdateBalloonCount;
                GameManager.Instance.OnLevelComplete += ShowResult;
                GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
                GameManager.Instance.OnLivesChanged += OnLivesChanged;
            }

            // TopBarUI 이벤트 구독
            if (_topBarUI != null)
            {
                _topBarUI.OnBackClicked += OnBackClicked;
                _topBarUI.OnRestartClicked += OnRestartClicked;
            }

            // 가이드라인 버튼 이벤트 구독
            if (_guideLineButton != null)
            {
                _guideLineButton.OnToggleChanged += OnGuideLineToggled;
            }
        }

        private void UnsubscribeEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMoveCountChanged -= UpdateMoveCount;
                GameManager.Instance.OnBalloonPopped -= UpdateBalloonCount;
                GameManager.Instance.OnLevelComplete -= ShowResult;
                GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
                GameManager.Instance.OnLivesChanged -= OnLivesChanged;
            }

            // TopBarUI 이벤트 해제
            if (_topBarUI != null)
            {
                _topBarUI.OnBackClicked -= OnBackClicked;
                _topBarUI.OnRestartClicked -= OnRestartClicked;
            }

            // 가이드라인 버튼 이벤트 해제
            if (_guideLineButton != null)
            {
                _guideLineButton.OnToggleChanged -= OnGuideLineToggled;
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                HideResultPanel();
                UpdateMoveCount(0);
                UpdateBalloonCount(0, GameManager.Instance.TotalBalloons);

                // TopBarUI 초기화
                InitializeTopBarUI();
            }
        }

        private void InitializeTopBarUI()
        {
            Debug.Log($"[GameUIView] InitializeTopBarUI: topBarUI={_topBarUI != null}, GameManager={GameManager.Instance != null}");

            if (_topBarUI == null || GameManager.Instance == null)
                return;

            var levelData = GameManager.Instance.CurrentLevelData;
            Debug.Log($"[GameUIView] InitializeTopBarUI: levelData={levelData != null}");

            if (levelData != null)
            {
                _topBarUI.Initialize(levelData.levelId, levelData.maxLives);
            }
        }

        private void OnLivesChanged(int current, int max)
        {
            if (_topBarUI != null)
            {
                _topBarUI.SetLives(current);
            }
        }

        private void OnBackClicked()
        {
            Debug.Log("[GameUIView] Back to lobby requested");
            SceneLoader.LoadLobby();
        }

        private void UpdateMoveCount(int count)
        {
            if (_moveCountText != null)
                _moveCountText.text = $"Moves: {count}";
        }

        private void UpdateBalloonCount(int popped, int total)
        {
            if (_balloonCountText != null)
                _balloonCountText.text = $"{popped}/{total}";
        }

        private void ShowResult(bool success, int stars)
        {
            // 클리어 시 Level Clear 연출 후 로비로 복귀
            if (success)
            {
                Debug.Log($"[GameUIView] Level Clear! Stars: {stars}, starting clear sequence...");

                // 다음 레벨로 진행
                LobbyUI.AdvanceToNextLevel();
                AdManager.Instance?.RecordLevelClear();

                // LevelClearManager가 있으면 연출 실행
                if (LevelClearManager.Instance != null)
                {
                    LevelClearManager.Instance.StartClearSequence(() =>
                    {
                        HandleLevelClearTransition();
                    });
                }
                else
                {
                    // LevelClearManager가 없으면 기존 방식대로 딜레이 후 로비 이동
                    Debug.LogWarning("[GameUIView] LevelClearManager not found, using fallback delay");
                    DOVirtual.DelayedCall(0.8f, () =>
                    {
                        HandleLevelClearTransition();
                    });
                }
                return;
            }

            // 실패 시에만 결과 패널 표시
            if (_resultPanel == null)
                return;

            _resultPanel.SetActive(true);

            if (_resultTitleText != null)
                _resultTitleText.text = "Try Again";

            // 별 숨김 (실패)
            if (_starObjects != null)
            {
                for (int i = 0; i < _starObjects.Length; i++)
                {
                    if (_starObjects[i] != null)
                        _starObjects[i].SetActive(false);
                }
            }

            // Next 버튼 숨김 (실패이므로)
            if (_nextButton != null)
                _nextButton.gameObject.SetActive(false);
        }

        private void HideResultPanel()
        {
            if (_resultPanel != null)
                _resultPanel.SetActive(false);
        }

        private void OnRestartClicked()
        {
            GameManager.Instance?.RestartLevel();
        }

        private void OnNextClicked()
        {
            // 다음 레벨 로드 (LevelLoader 필요)
            Debug.Log("[GameUIView] Next level requested");
        }

        private void OnRetryClicked()
        {
            GameManager.Instance?.RestartLevel();
        }

        private void OnGuideLineToggled(bool isOn)
        {
            if (GuideLineManager.Instance != null)
            {
                GuideLineManager.Instance.SetEnabled(isOn);
            }
        }

        // ========== 공개 인터페이스 ==========
        private void HandleLevelClearTransition()
        {
            Debug.Log("[GameUIView] Clear sequence complete, checking interstitial before lobby...");

            int currentLevel = LobbyUI.GetCurrentLevel();
            AdManager.Instance?.TryShowInterstitial(AdTrigger.LevelClear, currentLevel, SceneLoader.LoadLobby);
        }

        public void SetLevelText(int levelId)
        {
            if (_levelText != null)
                _levelText.text = $"Level {levelId}";
        }
    }
}
