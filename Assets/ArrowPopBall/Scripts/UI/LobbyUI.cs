using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Game.Ads;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 로비 UI 컨트롤러 - Home 화면
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("Home 화면")]
        [SerializeField] private GameObject _homePanel;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private Button _playButton;

        [Header("하단 탭바")]
        [SerializeField] private Button _challengeTabButton;
        [SerializeField] private Button _homeTabButton;
        [SerializeField] private Button _settingsTabButton;
        [SerializeField] private RectTransform _tabHighlight;  // 하나의 하이라이트 (위치 이동)

        [Header("데이터 리셋")]
        [SerializeField] private Button _resetButton;

        [Header("하이라이트 애니메이션")]
        [SerializeField] private float _highlightMoveDuration = 0.2f;

        [Header("탭 패널")]
        [SerializeField] private GameObject _challengePanel;
        [SerializeField] private GameObject _settingsPanel;

        [Header("Coming Soon 설정")]
        [SerializeField] private string _comingSoonText = "Coming Soon..";
        [SerializeField] private int _comingSoonFontSize = 48;
        [SerializeField] private Color _comingSoonColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("애니메이션")]
        [SerializeField] private float _buttonPunchScale = 0.1f;
        [SerializeField] private float _panelFadeDuration = 0.2f;

        [Header("Level Up 연출")]
        [SerializeField] private float _levelUpAnimDuration = 0.5f;
        [SerializeField] private float _levelUpSlideDistance = 80f;  // 숫자가 위로 이동하는 거리

        // ========== 내부 상태 변수 ==========
        private int _currentLevel;
        private TabType _currentTab = TabType.Home;
        private static int _previousLevel = -1;  // 이전 레벨 (레벨업 연출용)

        private enum TabType
        {
            Challenge,
            Home,
            Settings
        }

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            SetupButtons();
            EnsureComingSoonPanels();
        }

        private void Start()
        {
            Debug.Log("[LobbyUI] Lobby Scene started");
            AdManager.Instance?.ShowLobbyBanner();

            // 현재 레벨 로드
            LoadCurrentLevel();

            // 레벨업 연출이 필요한지 확인
            if (_previousLevel > 0 && _previousLevel < _currentLevel)
            {
                // 레벨업 연출 재생
                PlayLevelUpAnimation(_previousLevel, _currentLevel);
            }
            else
            {
                UpdateLevelText();
            }

            // 이전 레벨 초기화
            _previousLevel = -1;

            // Home 탭 활성화
            SwitchToTab(TabType.Home);
        }

        // ========== 내부 유틸리티 ==========
        private void SetupButtons()
        {
            // 플레이 버튼
            if (_playButton != null)
            {
                _playButton.onClick.AddListener(OnPlayButtonClicked);
            }

            // 탭 버튼
            if (_challengeTabButton != null)
                _challengeTabButton.onClick.AddListener(() => SwitchToTab(TabType.Challenge));

            if (_homeTabButton != null)
                _homeTabButton.onClick.AddListener(() => SwitchToTab(TabType.Home));

            // 설정 버튼: 탭바 없이 단독 기어 버튼으로 쓰일 수 있으므로 토글 동작
            if (_settingsTabButton != null)
                _settingsTabButton.onClick.AddListener(() =>
                    SwitchToTab(_currentTab == TabType.Settings ? TabType.Home : TabType.Settings));

            // 리셋 버튼
            if (_resetButton != null)
                _resetButton.onClick.AddListener(OnResetButtonClicked);
        }

        /// <summary>
        /// Challenge/Settings 패널이 없으면 자동 생성하고 Coming Soon 텍스트 추가
        /// </summary>
        private void EnsureComingSoonPanels()
        {
            // 부모 Canvas 찾기
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = FindObjectOfType<Canvas>();
            }

            Transform panelParent = parentCanvas != null ? parentCanvas.transform : transform;

            // Challenge 패널 생성/설정
            if (_challengePanel == null)
            {
                _challengePanel = CreateComingSoonPanel("ChallengePanel", panelParent);
            }
            else
            {
                // 기존 패널에 Coming Soon 텍스트가 없으면 추가
                EnsureComingSoonText(_challengePanel);
            }

            // Settings 패널 생성/설정
            if (_settingsPanel == null)
            {
                _settingsPanel = CreateComingSoonPanel("SettingsPanel", panelParent);
            }
            else
            {
                // 기존 패널에 Coming Soon 텍스트가 없으면 추가
                EnsureComingSoonText(_settingsPanel);
            }
        }

        /// <summary>
        /// Coming Soon 패널 생성
        /// </summary>
        private GameObject CreateComingSoonPanel(string panelName, Transform parent)
        {
            // 패널 생성
            GameObject panel = new GameObject(panelName);
            panel.transform.SetParent(parent, false);

            // RectTransform 설정 (전체 화면)
            RectTransform rectTransform = panel.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // CanvasGroup 추가 (페이드 애니메이션용)
            panel.AddComponent<CanvasGroup>();

            // Coming Soon 텍스트 추가
            CreateComingSoonTextObject(panel.transform);

            // 초기 상태: 비활성화
            panel.SetActive(false);

            Debug.Log($"[LobbyUI] Created {panelName} with Coming Soon text");
            return panel;
        }

        /// <summary>
        /// 기존 패널에 Coming Soon 텍스트가 없으면 추가
        /// </summary>
        private void EnsureComingSoonText(GameObject panel)
        {
            // 이미 텍스트가 있는지 확인
            var existingText = panel.GetComponentInChildren<TextMeshProUGUI>();
            if (existingText != null && existingText.text == _comingSoonText)
            {
                return;
            }

            // CanvasGroup 확인/추가
            if (panel.GetComponent<CanvasGroup>() == null)
            {
                panel.AddComponent<CanvasGroup>();
            }

            // Coming Soon 텍스트가 없으면 추가
            if (existingText == null)
            {
                CreateComingSoonTextObject(panel.transform);
                Debug.Log($"[LobbyUI] Added Coming Soon text to {panel.name}");
            }
        }

        /// <summary>
        /// Coming Soon 텍스트 오브젝트 생성
        /// </summary>
        private void CreateComingSoonTextObject(Transform parent)
        {
            GameObject textObj = new GameObject("ComingSoonText");
            textObj.transform.SetParent(parent, false);

            // RectTransform 설정 (중앙 정렬)
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(400f, 100f);

            // TextMeshProUGUI 설정
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = _comingSoonText;
            tmpText.fontSize = _comingSoonFontSize;
            tmpText.color = _comingSoonColor;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.fontStyle = FontStyles.Bold;
        }

        private void LoadCurrentLevel()
        {
            // PlayerPrefs에서 현재 레벨 로드 (기본값 1)
            _currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
            Debug.Log($"[LobbyUI] Loaded current level: {_currentLevel}");
        }

        private void UpdateLevelText()
        {
            if (_levelText != null)
            {
                _levelText.text = $"레벨 {_currentLevel}";
            }
        }

        private void SwitchToTab(TabType tab)
        {
            _currentTab = tab;

            // 모든 패널 전환
            SetPanelActive(_homePanel, tab == TabType.Home);
            SetPanelActive(_challengePanel, tab == TabType.Challenge);
            SetPanelActive(_settingsPanel, tab == TabType.Settings);

            // 탭 하이라이트 이동
            MoveHighlightToTab(tab);

            Debug.Log($"[LobbyUI] Switched to tab: {tab}");
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel == null)
                return;

            if (active)
            {
                panel.SetActive(true);
                // 페이드 인 애니메이션
                var canvasGroup = panel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.DOFade(1f, _panelFadeDuration);
                }
            }
            else
            {
                panel.SetActive(false);
            }
        }

        private void MoveHighlightToTab(TabType tab)
        {
            if (_tabHighlight == null)
                return;

            // 대상 버튼 결정
            RectTransform targetButton = tab switch
            {
                TabType.Challenge => _challengeTabButton?.GetComponent<RectTransform>(),
                TabType.Home => _homeTabButton?.GetComponent<RectTransform>(),
                TabType.Settings => _settingsTabButton?.GetComponent<RectTransform>(),
                _ => null
            };

            if (targetButton == null)
                return;

            // 버튼의 월드 위치(position)를 사용하여 하이라이트 이동
            // position은 앵커에 관계없이 실제 화면 위치를 나타냄
            Vector3 targetPos = _tabHighlight.position;
            targetPos.x = targetButton.position.x;

            Debug.Log($"[LobbyUI] Moving highlight to {tab}: worldX={targetButton.position.x}");

            _tabHighlight.DOMove(targetPos, _highlightMoveDuration)
                .SetEase(Ease.OutQuad);
        }

        // ========== 버튼 이벤트 핸들러 ==========
        private void OnResetButtonClicked()
        {
            Debug.Log("[LobbyUI] Reset button clicked - Resetting all data");

            // 버튼 펀치 애니메이션
            if (_resetButton != null)
            {
                _resetButton.transform.DOPunchScale(Vector3.one * _buttonPunchScale, 0.2f, 1, 0.5f);
            }

            // 모든 PlayerPrefs 데이터 초기화
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // 현재 레벨을 1로 설정
            _currentLevel = 1;
            _previousLevel = -1;

            // UI 업데이트
            UpdateLevelText();

            Debug.Log("[LobbyUI] All data reset - Starting from Level 1");
        }

        private void OnPlayButtonClicked()
        {
            Debug.Log($"[LobbyUI] Play button clicked - Loading level {_currentLevel}");
            AdManager.Instance?.HideBanner();

            // 버튼 펀치 애니메이션
            if (_playButton != null)
            {
                _playButton.transform.DOPunchScale(Vector3.one * _buttonPunchScale, 0.2f, 1, 0.5f)
                    .OnComplete(() =>
                    {
                        // 현재 레벨 저장 후 게임 씬 로드
                        PlayerPrefs.SetInt("SelectedLevel", _currentLevel);
                        PlayerPrefs.Save();
                        SceneLoader.LoadGame();
                    });
            }
            else
            {
                PlayerPrefs.SetInt("SelectedLevel", _currentLevel);
                PlayerPrefs.Save();
                SceneLoader.LoadGame();
            }
        }

        /// <summary>
        /// 레벨업 애니메이션 재생 (이전 레벨 → 새 레벨)
        /// </summary>
        private void PlayLevelUpAnimation(int fromLevel, int toLevel)
        {
            if (_levelText == null)
            {
                UpdateLevelText();
                return;
            }

            Debug.Log($"[LobbyUI] Playing level up animation: {fromLevel} → {toLevel}");

            // 초기 상태: 이전 레벨 표시
            _levelText.text = $"레벨 {fromLevel}";

            // RectTransform 가져오기
            RectTransform rectTransform = _levelText.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                UpdateLevelText();
                return;
            }

            // 원래 위치 저장
            Vector2 originalPos = rectTransform.anchoredPosition;

            // 애니메이션 시퀀스
            Sequence seq = DOTween.Sequence();

            // 1. 이전 숫자가 위로 올라가면서 페이드 아웃
            seq.Append(rectTransform.DOAnchorPosY(originalPos.y + _levelUpSlideDistance, _levelUpAnimDuration * 0.4f)
                .SetEase(Ease.InQuad));
            seq.Join(_levelText.DOFade(0f, _levelUpAnimDuration * 0.4f));

            // 2. 새 숫자로 변경하고 아래에서 시작
            seq.AppendCallback(() =>
            {
                _levelText.text = $"레벨 {toLevel}";
                rectTransform.anchoredPosition = new Vector2(originalPos.x, originalPos.y - _levelUpSlideDistance);
            });

            // 3. 새 숫자가 아래에서 올라오면서 페이드 인
            seq.Append(rectTransform.DOAnchorPosY(originalPos.y, _levelUpAnimDuration * 0.4f)
                .SetEase(Ease.OutQuad));
            seq.Join(_levelText.DOFade(1f, _levelUpAnimDuration * 0.4f));

            // 4. 펀치 스케일로 강조
            seq.Append(_levelText.transform.DOPunchScale(Vector3.one * 0.2f, _levelUpAnimDuration * 0.2f, 1, 0.5f));
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 레벨 클리어 후 다음 레벨로 진행
        /// </summary>
        public static void AdvanceToNextLevel()
        {
            int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);

            // 이전 레벨 저장 (Lobby에서 연출용)
            _previousLevel = currentLevel;

            currentLevel++;
            PlayerPrefs.SetInt("CurrentLevel", currentLevel);
            PlayerPrefs.Save();
            Debug.Log($"[LobbyUI] Advanced to level {currentLevel} (previous: {_previousLevel})");
        }

        /// <summary>
        /// 특정 레벨로 설정
        /// </summary>
        public static void SetCurrentLevel(int level)
        {
            PlayerPrefs.SetInt("CurrentLevel", level);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 현재 레벨 가져오기
        /// </summary>
        public static int GetCurrentLevel()
        {
            return PlayerPrefs.GetInt("CurrentLevel", 1);
        }

        private void OnDestroy()
        {
            AdManager.Instance?.HideBanner();
        }
    }
}
