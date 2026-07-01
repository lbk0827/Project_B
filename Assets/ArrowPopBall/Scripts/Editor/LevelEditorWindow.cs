using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Game.Data;
using Game.Editor.LevelEditor;

namespace Game.Editor
{
    /// <summary>
    /// Arrow Pop - Level Editor (v2.0)
    /// 깔끔하게 재설계된 레벨 에디터
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // ========== 그리드 설정 ==========
        private int _gridWidth = 8;
        private int _gridHeight = 10;
        private const int MAX_GRID_WIDTH = 50;
        private const int MAX_GRID_HEIGHT = 60;

        // ========== 화살표 설정 ==========
        // 색상 설정
        private bool _useAutoColors = true;
        private bool[] _selectedColors = new bool[13]; // 13가지 색상 선택 상태
        private bool _colorFoldout = false;

        // 화살표 길이 설정
        private bool _useAutoLength = true;
        private int _minArrowLength = 2;
        private int _maxArrowLength = 5;

        // 화살표 개수 설정
        private bool _useAutoArrowCount = true;
        private int _targetArrowCount = 10;
        private bool _fillRemainingCells = true; // 목표 개수 도달 후 남은 셀 채우기

        // ========== 레벨 저장/불러오기 ==========
        private int _levelId = 1;
        private const string LEVELS_PATH = "Assets/ArrowPopBall/Resources/Levels";

        // ========== Manual Edit 모드 ==========
        private bool _isManualEditMode = false;
        private int _selectedArrowIndex = -1;
        private GameColor _manualEditColor = GameColor.Red;

        // ========== 드래그 상태 ==========
        private bool _isDragging = false;
        private List<Vector2Int> _dragPath = new List<Vector2Int>();

        // ========== Solvable 검증 ==========
        private bool _isSolvable = false;
        private string _solvableMessage = "";
        private SolvabilityValidator.ValidationResult _lastValidationResult = null;
        private readonly List<int> _problemArrowIds = new List<int>();

        // ========== 생성 결과 ==========
        private List<EditorArrow> _arrows = new List<EditorArrow>();
        private string _statusMessage = "";

        // ========== UI 상태 ==========
        private Vector2 _leftPanelScrollPosition;  // 좌측 패널 스크롤
        private Vector2 _rightPanelScrollPosition; // 우측 패널 스크롤
        private float _currentCellSize = 30f;  // 그리드 프리뷰 동적 셀 크기
        private const float LEFT_PANEL_WIDTH = 420f;  // 좌측 패널 고정 너비

        // ========== 마스크/모양 설정 ==========
        private Texture2D _shapeImage = null;
        private bool[,] _shapeMask = null;  // true = 셀 활성화, false = 빈 공간
        private bool _useShapeMask = false;
        private bool _isDrawingMask = false;  // 수동 마스크 그리기 모드
        private bool _maskBrushMode = true;   // true = 그리기, false = 지우기

        // ========== 고급 옵션 ==========
        private bool _useColorMapping = false;  // PNG 이미지 색상을 화살표 색상으로 매핑
        private GameColor[,] _colorMap = null;  // 각 셀의 매핑된 색상

        // ========== 난이도 설정 ==========
        private int _difficultyLevel = 1;  // 1~10 (1=Easy, 10=Expert)
        private bool _useAdvancedDifficulty = false;  // Advanced 모드 토글
        private int _advancedMaxFreeArrows = 3;  // Advanced 모드: 수동 maxFreeArrows
        private int _lastMaxSimultaneousFree = -1;  // 마지막 생성 퍼즐의 분석 결과
        private bool _difficultyFoldout = false;  // 난이도 섹션 폴드아웃

        // ========== Geometric 패턴 설정 ==========
        private bool _useGeometricPatterns = false;
        private bool _patternStraight = true;
        private bool _patternLShape = true;
        private bool _patternUShape = true;
        private bool _patternZigzag = true;
        private bool _patternSnake = true;
        private bool _patternSpiral = true;
        private bool _patternOutline = true;

        // ========== 알고리즘 선택 ==========
        // Reverse Growth만 사용

        // ========== Stages 패널 ==========
        private Vector2 _stagesPanelScrollPosition;
        private List<StageAssetEntry> _stageAssetList = new List<StageAssetEntry>();
        private const float STAGES_PANEL_WIDTH = 200f;

        private struct StageAssetEntry
        {
            public int stageId;
            public string assetPath;
            public StageData stageData;
            public bool isSolvable;
        }

        // ========== Generators ==========
        private ReverseGrowthGenerator _reverseGrowthGenerator;
        private List<int> _lastSolutionOrder;  // 마지막으로 생성된 solutionOrder 저장

        [MenuItem("Tools/Arrow Pop/Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(1000, 600);  // 3패널 레이아웃을 위해 최소 너비 증가
        }

        private void OnGUI()
        {
            // 타이틀
            EditorGUILayout.LabelField("Arrow Pop - Level Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // ========== 좌우 분할 레이아웃 시작 ==========
            EditorGUILayout.BeginHorizontal();

            // ========== 좌측 패널: 설정 영역 ==========
            EditorGUILayout.BeginVertical(GUILayout.Width(LEFT_PANEL_WIDTH));
            _leftPanelScrollPosition = EditorGUILayout.BeginScrollView(_leftPanelScrollPosition);

            // 통합된 설정 UI
            DrawUnifiedSettingsTab();
            EditorGUILayout.Space(10);

            // 공통 영역: Save/Load
            DrawSaveLoadSection();
            EditorGUILayout.Space(10);

            // 공통 영역: Manual Edit
            DrawManualEditSection();
            EditorGUILayout.Space(10);

            // Status Message (좌측 패널 하단)
            DrawStatusMessage();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // ========== 구분선 ==========
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // ========== 우측 패널: Grid Preview (자동 스케일링) ==========
            EditorGUILayout.BeginVertical();
            _rightPanelScrollPosition = EditorGUILayout.BeginScrollView(_rightPanelScrollPosition);

            DrawGridPreview();
            EditorGUILayout.Space(5);
            DrawGridInfo();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // ========== 구분선 ==========
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // ========== 우측 패널: Stages 목록 ==========
            EditorGUILayout.BeginVertical(GUILayout.Width(STAGES_PANEL_WIDTH));
            DrawStagesPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            // ========== 3패널 레이아웃 끝 ==========
        }

        // ========== Stages Panel ==========

        private void DrawStagesPanel()
        {
            EditorGUILayout.LabelField("Stages", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh", GUILayout.Height(22)))
            {
                RefreshStageAssetList();
            }

            EditorGUILayout.Space(3);

            _stagesPanelScrollPosition = EditorGUILayout.BeginScrollView(_stagesPanelScrollPosition);

            if (_stageAssetList.Count == 0)
            {
                EditorGUILayout.HelpBox("No stage assets found.\nSave a stage first.", MessageType.Info);
            }
            else
            {
                foreach (var entry in _stageAssetList)
                {
                    bool isCurrent = entry.stageId == _levelId;

                    // Solvable 아이콘 + 스테이지 번호를 한 줄 텍스트로
                    string solvableIcon = entry.isSolvable ? "\u2713" : "\u2717";
                    Color textColor = isCurrent ? Color.white : (entry.isSolvable ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.4f, 0.4f));

                    var labelStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal,
                        normal = { textColor = textColor }
                    };

                    var rect = EditorGUILayout.BeginHorizontal();

                    // 현재 스테이지 배경 하이라이트
                    if (isCurrent)
                    {
                        EditorGUI.DrawRect(rect, new Color(0.2f, 0.4f, 0.7f, 0.3f));
                    }

                    // 클릭 가능한 텍스트 영역
                    if (GUILayout.Button($" {solvableIcon} Stage {entry.stageId}", labelStyle))
                    {
                        LoadStageIntoEditor(entry);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void RefreshStageAssetList()
        {
            _stageAssetList.Clear();

            if (!Directory.Exists(STAGES_PATH))
                return;

            var guids = AssetDatabase.FindAssets("t:StageData", new[] { STAGES_PATH });

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var stageData = AssetDatabase.LoadAssetAtPath<StageData>(path);

                if (stageData == null) continue;

                // 파일명에서 스테이지 ID 추출 (Stage_001.asset → 1)
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                int stageId = 0;
                if (fileName.StartsWith("Stage_") && int.TryParse(fileName.Substring(6), out int parsedId))
                {
                    stageId = parsedId;
                }

                // Solvable 검증 (동적 시뮬레이션 - solutionOrder에 의존하지 않음)
                bool solvable = false;
                if (stageData.arrows != null && stageData.arrows.Count > 0)
                {
                    var tempArrows = new List<EditorArrow>();
                    foreach (var stageArrow in stageData.arrows)
                    {
                        var editorArrow = ConvertFromStageArrowData(stageArrow);
                        if (editorArrow != null)
                            tempArrows.Add(editorArrow);
                    }

                    if (tempArrows.Count > 0)
                    {
                        var depGraph = new DependencyGraph(stageData.gridWidth, stageData.gridHeight);
                        var sortResult = depGraph.TopologicalSortDynamic(tempArrows);
                        solvable = sortResult.Count == tempArrows.Count;
                    }
                }

                _stageAssetList.Add(new StageAssetEntry
                {
                    stageId = stageId,
                    assetPath = path,
                    stageData = stageData,
                    isSolvable = solvable
                });
            }

            // ID 순 정렬
            _stageAssetList.Sort((a, b) => a.stageId.CompareTo(b.stageId));
        }

        private void LoadStageIntoEditor(StageAssetEntry entry)
        {
            if (entry.stageData == null) return;

            var stageData = entry.stageData;

            // 그리드 설정 반영
            _gridWidth = stageData.gridWidth;
            _gridHeight = stageData.gridHeight;
            _levelId = entry.stageId;

            // 화살표 변환 (기존 ConvertFromStageArrowData 재사용)
            _arrows.Clear();
            if (stageData.arrows != null)
            {
                foreach (var stageArrow in stageData.arrows)
                {
                    var editorArrow = ConvertFromStageArrowData(stageArrow);
                    if (editorArrow != null)
                        _arrows.Add(editorArrow);
                }
            }

            // Solution order 반영
            _lastSolutionOrder = stageData.solutionOrder != null && stageData.solutionOrder.Count > 0
                ? new List<int>(stageData.solutionOrder)
                : null;

            _selectedArrowIndex = -1;

            // Solvable 검증
            ValidateSolvable();

            // 난이도 분석
            if (_arrows.Count > 0)
            {
                var (maxSimFree, _) = SolvabilityValidator.AnalyzeDifficulty(
                    _arrows, _gridWidth, _gridHeight);
                _lastMaxSimultaneousFree = maxSimFree;
            }

            _statusMessage = $"Loaded Stage {entry.stageId:D3} ({stageData.gridWidth}x{stageData.gridHeight}, {_arrows.Count} arrows)";
            Repaint();
        }

        private void OnEnable()
        {
            RefreshStageAssetList();
        }

        // ========== Unified Settings Tab ==========
        private void DrawUnifiedSettingsTab()
        {
            // 이미지 임포트 (선택사항)
            DrawImageImportSection();
            EditorGUILayout.Space(10);

            DrawGridSettings();
            EditorGUILayout.Space(10);

            DrawArrowSettings();
            EditorGUILayout.Space(10);

            DrawGenerateButton();
        }

        // ========== UI 그리기 ==========

        private void DrawImageImportSection()
        {
            EditorGUILayout.LabelField("Image Import (Optional)", EditorStyles.boldLabel);

            // PNG 이미지 선택
            _shapeImage = (Texture2D)EditorGUILayout.ObjectField("PNG Image", _shapeImage, typeof(Texture2D), false);

            if (_shapeImage != null)
            {
                EditorGUILayout.HelpBox(
                    $"Image Size: {_shapeImage.width} x {_shapeImage.height}\n" +
                    "Shape and colors will be extracted from the image.",
                    MessageType.Info);

                _useColorMapping = EditorGUILayout.Toggle("Use Color Mapping", _useColorMapping);

                if (GUILayout.Button("Apply ShapeMask", GUILayout.Height(25)))
                {
                    _useShapeMask = true;
                    ApplyShapeFromImage();
                    _statusMessage = _useColorMapping
                        ? "ShapeMask + Color Mapping applied from image."
                        : "ShapeMask applied from image (Color Mapping off).";
                    Repaint();
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "이미지 없이 생성 시: 그리드 전체를 사용합니다.\n" +
                    "이미지 선택 시: 이미지 모양과 색상을 적용합니다.",
                    MessageType.Info);
            }
        }

        private void DrawGridSettings()
        {
            EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Width", GUILayout.Width(50));
            _gridWidth = EditorGUILayout.IntSlider(_gridWidth, 4, MAX_GRID_WIDTH);
            EditorGUILayout.LabelField("Height", GUILayout.Width(50));
            _gridHeight = EditorGUILayout.IntSlider(_gridHeight, 4, MAX_GRID_HEIGHT);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGenerateButton()
        {
            string buttonText = _shapeImage != null
                ? "Generate Puzzle (Reverse Growth + Image)"
                : "Generate Puzzle (Reverse Growth)";

            if (GUILayout.Button(buttonText, GUILayout.Height(40)))
            {
                // 이미지 있으면 Shape 적용 (Color Mapping은 체크박스 상태 유지)
                if (_shapeImage != null)
                {
                    _useShapeMask = true;
                    ApplyShapeFromImage();
                }
                else
                {
                    _useShapeMask = false;
                    _useColorMapping = false;
                }

                GeneratePuzzleReverseGrowth();
            }
        }

        private void DrawArrowSettings()
        {
            EditorGUILayout.LabelField("Arrow Settings", EditorStyles.boldLabel);

            // ========== 색상 설정 ==========
            EditorGUILayout.BeginHorizontal();
            _useAutoColors = EditorGUILayout.Toggle("Auto Colors", _useAutoColors);
            if (_useAutoColors)
            {
                int autoColorCount = Mathf.Clamp((_gridWidth * _gridHeight) / 15, 3, 6);
                EditorGUILayout.LabelField($"({autoColorCount} colors)", GUILayout.Width(80));
            }
            EditorGUILayout.EndHorizontal();

            if (!_useAutoColors)
            {
                _colorFoldout = EditorGUILayout.Foldout(_colorFoldout, "Select Colors (13 available)");
                if (_colorFoldout)
                {
                    EditorGUI.indentLevel++;
                    string[] colorNames = { "Red", "Blue", "Green", "Yellow", "Purple", "Orange", "Cyan", "Pink", "Brown", "Lime", "Navy", "Magenta", "Black" };
                    Color[] previewColors = {
                        Color.red, Color.blue, Color.green, Color.yellow,
                        new Color(0.5f, 0, 0.5f), new Color(1f, 0.5f, 0), Color.cyan, new Color(1f, 0.4f, 0.7f),
                        new Color(0.6f, 0.3f, 0.1f), new Color(0.5f, 1f, 0), new Color(0, 0, 0.5f), Color.magenta, Color.black
                    };

                    // 4열로 표시 - 클릭 가능한 컬러 버튼
                    for (int row = 0; row < 4; row++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        for (int col = 0; col < 4; col++)
                        {
                            int i = row * 4 + col;
                            if (i >= 13) break;

                            var prevBgColor = GUI.backgroundColor;
                            GUI.backgroundColor = previewColors[i];

                            var btnStyle = new GUIStyle(GUI.skin.button)
                            {
                                fixedWidth = 90,
                                fixedHeight = 28,
                                fontStyle = _selectedColors[i] ? FontStyle.Bold : FontStyle.Normal,
                                alignment = TextAnchor.MiddleCenter
                            };
                            // 어두운 배경에서 텍스트 가독성 확보
                            float lum = 0.299f * previewColors[i].r + 0.587f * previewColors[i].g + 0.114f * previewColors[i].b;
                            btnStyle.normal.textColor = lum > 0.5f ? Color.black : Color.white;

                            string btnLabel = _selectedColors[i] ? $"\u2713 {colorNames[i]}" : colorNames[i];
                            if (GUILayout.Button(btnLabel, btnStyle))
                            {
                                _selectedColors[i] = !_selectedColors[i];
                            }

                            GUI.backgroundColor = prevBgColor;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    // 선택된 색상 개수 표시
                    int selectedCount = 0;
                    for (int i = 0; i < 13; i++) if (_selectedColors[i]) selectedCount++;
                    EditorGUILayout.LabelField($"Selected: {selectedCount} colors");
                    if (selectedCount < 1)
                    {
                        EditorGUILayout.HelpBox("At least 1 color required!", MessageType.Warning);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(5);

            // ========== 화살표 길이 설정 ==========
            EditorGUILayout.BeginHorizontal();
            _useAutoLength = EditorGUILayout.Toggle("Auto Length", _useAutoLength);
            if (_useAutoLength)
            {
                var (minLen, maxLen) = GetAutoArrowLength();
                EditorGUILayout.LabelField($"({minLen} ~ {maxLen})", GUILayout.Width(80));
            }
            EditorGUILayout.EndHorizontal();

            if (!_useAutoLength)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Min", GUILayout.Width(30));
                _minArrowLength = EditorGUILayout.IntSlider(_minArrowLength, 1, 50);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Max", GUILayout.Width(30));
                _maxArrowLength = EditorGUILayout.IntSlider(_maxArrowLength, _minArrowLength, 99);
                EditorGUILayout.EndHorizontal();
            }


            EditorGUILayout.Space(5);

            // ========== 화살표 개수 설정 ==========
            EditorGUILayout.BeginHorizontal();
            _useAutoArrowCount = EditorGUILayout.Toggle("Auto Arrow Count", _useAutoArrowCount);
            if (_useAutoArrowCount)
            {
                EditorGUILayout.LabelField("(Fill grid)", GUILayout.Width(80));
            }
            EditorGUILayout.EndHorizontal();

            if (!_useAutoArrowCount)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target Count", GUILayout.Width(80));
                _targetArrowCount = EditorGUILayout.IntSlider(_targetArrowCount, 1, 99);
                EditorGUILayout.EndHorizontal();

                _fillRemainingCells = EditorGUILayout.Toggle("Fill Remaining Cells", _fillRemainingCells);
                if (!_fillRemainingCells)
                {
                    EditorGUILayout.HelpBox("Grid may have empty cells after generation.", MessageType.Info);
                }
            }

            EditorGUILayout.Space(5);

            // ========== Geometric 패턴 설정 ==========
            DrawGeometricPatternSettings();

            EditorGUILayout.Space(5);

            // ========== 난이도 설정 ==========
            DrawDifficultySettings();
        }

        private void DrawGeometricPatternSettings()
        {
            EditorGUILayout.LabelField("Geometric Pattern", EditorStyles.boldLabel);

            _useGeometricPatterns = EditorGUILayout.Toggle(
                new GUIContent("Use Geometric Patterns", "규칙적인 geometric 형태로 화살표 생성"),
                _useGeometricPatterns);

            if (_useGeometricPatterns)
            {
                EditorGUI.indentLevel++;

                // Select All / Deselect All 버튼
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUI.indentLevel * 15);
                if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft, GUILayout.Width(80)))
                {
                    _patternStraight = _patternLShape = _patternUShape = true;
                    _patternZigzag = _patternSnake = _patternSpiral = _patternOutline = true;
                }
                if (GUILayout.Button("Deselect All", EditorStyles.miniButtonRight, GUILayout.Width(80)))
                {
                    _patternStraight = _patternLShape = _patternUShape = false;
                    _patternZigzag = _patternSnake = _patternSpiral = _patternOutline = false;
                }
                EditorGUILayout.EndHorizontal();

                // 개별 패턴 체크박스
                _patternStraight = EditorGUILayout.Toggle("Straight (직선형)", _patternStraight);
                _patternLShape   = EditorGUILayout.Toggle("L-Shape (L자형)", _patternLShape);
                _patternUShape   = EditorGUILayout.Toggle("U-Shape (U자형)", _patternUShape);
                _patternZigzag   = EditorGUILayout.Toggle("Zigzag (지그재그형)", _patternZigzag);
                _patternSnake    = EditorGUILayout.Toggle("Snake (Snake형)", _patternSnake);
                _patternSpiral   = EditorGUILayout.Toggle("Spiral (나선형)", _patternSpiral);
                _patternOutline  = EditorGUILayout.Toggle("Outline (외곽형)", _patternOutline);

                // 활성 패턴 수 표시
                int activeCount = 0;
                if (_patternStraight) activeCount++;
                if (_patternLShape)   activeCount++;
                if (_patternUShape)   activeCount++;
                if (_patternZigzag)   activeCount++;
                if (_patternSnake)    activeCount++;
                if (_patternSpiral)   activeCount++;
                if (_patternOutline)  activeCount++;

                EditorGUILayout.LabelField($"Active: {activeCount} / 7", EditorStyles.miniLabel);

                if (activeCount == 0)
                {
                    EditorGUILayout.HelpBox("At least 1 pattern required! Straight will be used as fallback.", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawDifficultySettings()
        {
            EditorGUILayout.LabelField("Difficulty", EditorStyles.boldLabel);

            // Difficulty 슬라이더
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Level", GUILayout.Width(40));
            _difficultyLevel = EditorGUILayout.IntSlider(_difficultyLevel, 1, 10);
            EditorGUILayout.LabelField(GetDifficultyLabel(_difficultyLevel), GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            // 현재 설정의 maxFreeArrows 예상값 표시
            if (_difficultyLevel > 1 && !_useAdvancedDifficulty)
            {
                int estimated = GetMaxFreeArrowsFromDifficulty();
                EditorGUILayout.HelpBox(
                    $"동시 탈출 가능: 최대 {(estimated == 0 ? "제한 없음" : estimated + "개")}\n" +
                    "높은 난이도는 생성 시간이 더 걸릴 수 있습니다.",
                    MessageType.Info);
            }

            // Advanced 폴드아웃
            _difficultyFoldout = EditorGUILayout.Foldout(_difficultyFoldout, "Advanced Difficulty");
            if (_difficultyFoldout)
            {
                EditorGUI.indentLevel++;
                _useAdvancedDifficulty = EditorGUILayout.Toggle("Manual Max Free Arrows", _useAdvancedDifficulty);

                if (_useAdvancedDifficulty)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Max Free", GUILayout.Width(60));
                    _advancedMaxFreeArrows = EditorGUILayout.IntSlider(_advancedMaxFreeArrows, 1, 20);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.HelpBox(
                        $"각 스텝에서 동시 탈출 가능 화살표가 {_advancedMaxFreeArrows}개 이하인 퍼즐만 생성합니다.",
                        MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }
        }

        private string GetDifficultyLabel(int level)
        {
            return level switch
            {
                1 => "Very Easy",
                2 => "Easy",
                3 => "Easy+",
                4 => "Normal",
                5 => "Normal+",
                6 => "Hard",
                7 => "Hard+",
                8 => "Very Hard",
                9 => "Expert",
                10 => "Expert+",
                _ => ""
            };
        }

        /// <summary>
        /// Difficulty 슬라이더 값에서 maxFreeArrows 계산
        /// </summary>
        private int GetMaxFreeArrowsFromDifficulty()
        {
            if (_useAdvancedDifficulty)
                return _advancedMaxFreeArrows;

            if (_difficultyLevel <= 1)
                return 0; // 제한 없음

            // 예상 화살표 수 계산
            var (minLen, maxLen) = GetArrowLengthRange();
            int avgLen = (minLen + maxLen) / 2;
            int activeCells = _useShapeMask && _shapeMask != null ? CountActiveCells() : _gridWidth * _gridHeight;
            int estimatedArrows = Mathf.Max(1, activeCells / Mathf.Max(1, avgLen));

            return _difficultyLevel switch
            {
                2 => Mathf.Max(estimatedArrows * 60 / 100, 5),
                3 => Mathf.Max(estimatedArrows * 45 / 100, 4),
                4 => Mathf.Max(estimatedArrows * 35 / 100, 4),
                5 => Mathf.Max(estimatedArrows * 25 / 100, 3),
                6 => Mathf.Max(estimatedArrows * 18 / 100, 3),
                7 => Mathf.Max(estimatedArrows * 12 / 100, 2),
                8 => 3,
                9 => 2,
                10 => 1,
                _ => 0
            };
        }

        /// <summary>
        /// 그리드 크기에 따라 화살표 길이 자동 계산
        ///
        /// 핵심 원리: 화살표가 너무 길면 의존성 사이클 발생 확률 급증
        /// 큰 그리드일수록 화살표 수는 많지만, 개별 길이는 적당히 제한해야 Solvable 확률 증가
        ///
        /// 그리드 크기별 권장 범위:
        /// | 크기      | Min | Max | 비고                    |
        /// |-----------|-----|-----|------------------------|
        /// | 5x5       | 2   | 4   | 초소형                  |
        /// | 8x8       | 2   | 6   | 소형                    |
        /// | 10x10     | 2   | 8   | 기본                    |
        /// | 12x12     | 2   | 10  | 중소형                  |
        /// | 15x15     | 3   | 12  | 중형                    |
        /// | 20x20     | 4   | 16  | 대형                    |
        /// | 25x25     | 5   | 20  | 초대형                  |
        /// | 30x30     | 5   | 24  | 거대형                  |
        /// | 40x40     | 6   | 32  | 초거대형                |
        /// | 50x60+    | 7   | 40  | 최대 크기               |
        /// </summary>
        private (int min, int max) GetAutoArrowLength()
        {
            int largerDim = Mathf.Max(_gridWidth, _gridHeight);

            // 최소 길이: 그리드가 클수록 최소 길이 증가 (짧은 화살표가 많으면 사이클 확률 증가)
            int minLength;
            if (largerDim <= 10)
                minLength = 2;
            else if (largerDim <= 15)
                minLength = 3;
            else if (largerDim <= 20)
                minLength = 4;
            else if (largerDim <= 30)
                minLength = 5;
            else if (largerDim <= 40)
                minLength = 6;
            else
                minLength = 7;  // 40+ 그리드: 최소 7칸

            // 최대 길이: 그리드 크기에 비례 (큰 그리드에서는 긴 화살표 허용)
            // 긴 화살표 = 적은 화살표 수 = 단순한 의존성 그래프 = 사이클 감소
            int maxLength;
            if (largerDim <= 5)
                maxLength = 4;   // 5x5: 최대 4칸
            else if (largerDim <= 8)
                maxLength = 6;   // 8x8: 최대 6칸
            else if (largerDim <= 10)
                maxLength = 8;   // 10x10: 최대 8칸
            else if (largerDim <= 12)
                maxLength = 10;  // 12x12: 최대 10칸
            else if (largerDim <= 15)
                maxLength = 12;  // 15x15: 최대 12칸
            else if (largerDim <= 18)
                maxLength = 14;  // 18x18: 최대 14칸
            else if (largerDim <= 20)
                maxLength = 16;  // 20x20: 최대 16칸
            else if (largerDim <= 25)
                maxLength = 20;  // 25x25: 최대 20칸
            else if (largerDim <= 30)
                maxLength = 24;  // 30x30: 최대 24칸
            else if (largerDim <= 40)
                maxLength = 32;  // 40x40: 최대 32칸
            else
                maxLength = 40;  // 50x60+: 최대 40칸

            return (minLength, maxLength);
        }

        private void DrawSaveLoadSection()
        {
            EditorGUILayout.LabelField("Save / Load", EditorStyles.boldLabel);

            // Stage ID 입력
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Stage ID", GUILayout.Width(60));
            _levelId = EditorGUILayout.IntField(_levelId, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // StageData 저장 (권장)
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Stage Asset", GUILayout.Height(25)))
            {
                SaveToStageAsset();
            }
            if (GUILayout.Button("Load Stage Asset", GUILayout.Height(25)))
            {
                LoadFromStageAsset();
            }
            EditorGUILayout.EndHorizontal();

            // Stage Asset 경로 표시
            string stageAssetPath = GetStageAssetPath(_levelId);
            bool stageExists = AssetDatabase.LoadAssetAtPath<StageData>(stageAssetPath) != null;
            EditorGUILayout.HelpBox(
                $"Stage: {stageAssetPath}\n{(stageExists ? "✓ Asset exists" : "✗ Asset not found")}",
                stageExists ? MessageType.None : MessageType.Warning);

            EditorGUILayout.Space(5);

            // Reset 버튼
            if (GUILayout.Button("Reset Grid", GUILayout.Height(25)))
            {
                _arrows.Clear();
                _selectedArrowIndex = -1;
                _dragPath.Clear();
                _shapeImage = null;
                _shapeMask = null;
                _useShapeMask = false;
                _colorMap = null;
                _useColorMapping = false;
                _isSolvable = false;
                _solvableMessage = "";
                _lastValidationResult = null;
                _problemArrowIds.Clear();
                _lastSolutionOrder = null;
                _statusMessage = "Grid reset.";
                Repaint();
            }
        }

        private void DrawManualEditSection()
        {
            EditorGUILayout.LabelField("Manual Edit (Drag Mode)", EditorStyles.boldLabel);

            _isManualEditMode = EditorGUILayout.Toggle("Edit Mode", _isManualEditMode);

            if (_isManualEditMode)
            {
                // 색상 선택
                _manualEditColor = (GameColor)EditorGUILayout.EnumPopup("Arrow Color", _manualEditColor);

                EditorGUILayout.HelpBox(
                    "◆ Drag to draw arrow (Tail → Head)\n" +
                    "◆ Right Click: Delete arrow\n" +
                    "◆ Left Click: Select arrow",
                    MessageType.Info);

                // Solvable 상태 표시
                DrawSolvableStatus();

                // 선택된 화살표 편집
                if (_selectedArrowIndex >= 0 && _selectedArrowIndex < _arrows.Count)
                {
                    var selectedArrow = _arrows[_selectedArrowIndex];
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField($"Selected: Arrow #{selectedArrow.id} (Length: {selectedArrow.cells.Count})", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Change Color"))
                    {
                        selectedArrow.color = _manualEditColor;
                        ValidateSolvable();
                        Repaint();
                    }
                    if (GUILayout.Button("Delete"))
                    {
                        _arrows.RemoveAt(_selectedArrowIndex);
                        _selectedArrowIndex = -1;
                        ValidateSolvable();
                        Repaint();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // Clear All 버튼
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Clear All Arrows"))
                {
                    _arrows.Clear();
                    _selectedArrowIndex = -1;
                    _dragPath.Clear();
                    _isSolvable = false;
                    _solvableMessage = "";
                    _lastValidationResult = null;
                    _problemArrowIds.Clear();
                    _statusMessage = "All arrows cleared.";
                    Repaint();
                }
            }
        }

        private void DrawSolvableStatus()
        {
            if (_arrows.Count == 0)
            {
                EditorGUILayout.HelpBox("No arrows yet. Draw arrows on the grid.", MessageType.None);
                return;
            }

            // 색상과 메시지
            Color boxColor = _isSolvable ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.7f, 0.2f, 0.2f);
            Color labelColor = _isSolvable ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);
            string icon = _isSolvable ? "✓" : "✗";

            var style = new GUIStyle(EditorStyles.helpBox);
            var labelStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = labelColor } };
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = boxColor;

            EditorGUILayout.BeginVertical(style);
            EditorGUILayout.LabelField($"{icon} {_solvableMessage}", labelStyle);
            if (!_isSolvable && _lastValidationResult != null && !string.IsNullOrEmpty(_lastValidationResult.error))
            {
                EditorGUILayout.LabelField($"Reason: {_lastValidationResult.error}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = originalColor;

            DrawProblemArrowsSection();
        }

        private void DrawGridPreview()
        {
            EditorGUILayout.LabelField("Grid Preview", EditorStyles.boldLabel);

            if (_arrows.Count == 0 && !_isManualEditMode)
            {
                EditorGUILayout.HelpBox("Click 'Generate Puzzle' to create arrows.", MessageType.Info);
            }

            // 그리드 프리뷰 영역 - 자동 스케일링
            float availableWidth = position.width - LEFT_PANEL_WIDTH - 40; // 여유 공간 고려
            float availableHeight = position.height - 180; // 헤더 + 하단 Grid Info 영역 확보

            // 그리드 비율 유지하면서 사용 가능한 영역에 맞춤
            float gridAspect = (float)_gridWidth / _gridHeight;
            float availableAspect = availableWidth / availableHeight;

            float cellSize;
            if (gridAspect > availableAspect)
            {
                // 너비에 맞춤
                cellSize = availableWidth / _gridWidth;
            }
            else
            {
                // 높이에 맞춤
                cellSize = availableHeight / _gridHeight;
            }

            // 그리드를 컴팩트하게 표시 (70% 축소)
            cellSize *= 0.7f;
            // 최소/최대 셀 크기 제한
            cellSize = Mathf.Clamp(cellSize, 8f, 35f);
            _currentCellSize = cellSize; // 현재 셀 크기 저장

            float previewWidth = _gridWidth * cellSize;
            float previewHeight = _gridHeight * cellSize;

            Rect previewRect = GUILayoutUtility.GetRect(previewWidth + 20, previewHeight + 20);
            Rect gridRect = new Rect(previewRect.x + 10, previewRect.y + 10, previewWidth, previewHeight);

            // 배경
            EditorGUI.DrawRect(gridRect, new Color(0.2f, 0.2f, 0.2f));

            // 마스크 표시 (비활성화된 셀은 어둡게)
            if (_useShapeMask && _shapeMask != null)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    for (int y = 0; y < _gridHeight; y++)
                    {
                        if (!_shapeMask[x, y])
                        {
                            Rect cellRect = new Rect(
                                gridRect.x + x * _currentCellSize,
                                gridRect.y + (_gridHeight - 1 - y) * _currentCellSize,
                                _currentCellSize,
                                _currentCellSize
                            );
                            EditorGUI.DrawRect(cellRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
                        }
                    }
                }
            }

            // 그리드 라인
            Handles.color = new Color(0.4f, 0.4f, 0.4f);
            for (int x = 0; x <= _gridWidth; x++)
            {
                float xPos = gridRect.x + x * _currentCellSize;
                Handles.DrawLine(new Vector3(xPos, gridRect.y), new Vector3(xPos, gridRect.yMax));
            }
            for (int y = 0; y <= _gridHeight; y++)
            {
                float yPos = gridRect.y + y * _currentCellSize;
                Handles.DrawLine(new Vector3(gridRect.x, yPos), new Vector3(gridRect.xMax, yPos));
            }

            // 화살표 그리기
            foreach (var arrow in _arrows)
            {
                DrawArrow(gridRect, arrow);
            }

            // 선택된 화살표 하이라이트 (화살표 색상으로 표시)
            if (_selectedArrowIndex >= 0 && _selectedArrowIndex < _arrows.Count)
            {
                var selected = _arrows[_selectedArrowIndex];
                Color highlightColor = GetColorForGameColor(selected.color);
                highlightColor.a = 0.3f;

                foreach (var cell in selected.cells)
                {
                    Rect cellRect = new Rect(
                        gridRect.x + cell.x * _currentCellSize,
                        gridRect.y + (_gridHeight - 1 - cell.y) * _currentCellSize,
                        _currentCellSize,
                        _currentCellSize
                    );
                    // 반투명 색상 채우기
                    EditorGUI.DrawRect(cellRect, highlightColor);
                    // 색상 테두리
                    Color borderColor = GetColorForGameColor(selected.color);
                    Handles.color = borderColor;
                    Handles.DrawWireCube(
                        new Vector3(cellRect.center.x, cellRect.center.y, 0),
                        new Vector3(_currentCellSize - 2, _currentCellSize - 2, 0));
                }
            }

            // 드래그 중인 경로 표시
            if (_isDragging && _dragPath.Count > 0)
            {
                DrawDragPath(gridRect);
            }

            // Manual Edit 모드에서 클릭 처리
            if (_isManualEditMode)
            {
                HandleGridClick(gridRect);
            }

            // 마스크 그리기 모드에서 클릭 처리
            if (_isDrawingMask)
            {
                HandleMaskDrawing(gridRect);
            }
        }

        private void DrawGridInfo()
        {
            if (_arrows.Count == 0) return;

            // 화살표 수
            EditorGUILayout.LabelField($"Arrows: {_arrows.Count}", EditorStyles.boldLabel);

            // Solvable 상태 박스
            Color boxColor = _isSolvable ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.7f, 0.2f, 0.2f);
            Color labelColor = _isSolvable ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);
            string icon = _isSolvable ? "\u2713" : "\u2717";

            var style = new GUIStyle(EditorStyles.helpBox);
            var labelStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = labelColor } };
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = boxColor;

            EditorGUILayout.BeginVertical(style);
            string solvableText = _isSolvable ? "Solvable!" : _solvableMessage;
            EditorGUILayout.LabelField($"{icon} {solvableText}", labelStyle);
            if (!_isSolvable && _lastValidationResult != null && !string.IsNullOrEmpty(_lastValidationResult.error))
            {
                EditorGUILayout.LabelField($"Reason: {_lastValidationResult.error}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            GUI.backgroundColor = originalColor;
            DrawProblemArrowsSection();

            // 난이도 분석 결과
            if (_lastMaxSimultaneousFree >= 0)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField($"Max Simultaneous Free: {_lastMaxSimultaneousFree}", EditorStyles.miniLabel);
            }

            // 해답 순서 시각화
            DrawSolutionOrderVisual();
        }

        private void DrawProblemArrowsSection()
        {
            if (_problemArrowIds.Count == 0) return;

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField($"Problem Arrows ({_problemArrowIds.Count})", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            int displayCount = Mathf.Min(_problemArrowIds.Count, 20);

            for (int i = 0; i < displayCount; i++)
            {
                int arrowId = _problemArrowIds[i];
                int arrowIndex = FindArrowIndexById(arrowId);
                if (arrowIndex < 0) continue;

                var arrow = _arrows[arrowIndex];
                Color btnColor = GetColorForGameColor(arrow.color);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = btnColor;

                bool isSelected = _selectedArrowIndex == arrowIndex;
                string btnLabel = isSelected ? $"[#{arrowId}]" : $"#{arrowId}";
                if (GUILayout.Button(btnLabel, GUILayout.Width(52), GUILayout.Height(22)))
                {
                    _selectedArrowIndex = arrowIndex;
                    Repaint();
                }

                GUI.backgroundColor = prevBg;
            }

            if (_problemArrowIds.Count > 20)
            {
                EditorGUILayout.LabelField($"+{_problemArrowIds.Count - 20} more", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private int FindArrowIndexById(int arrowId)
        {
            for (int i = 0; i < _arrows.Count; i++)
            {
                if (_arrows[i].id == arrowId) return i;
            }
            return -1;
        }

        private void SetProblemArrowIds(List<int> arrowIds)
        {
            _problemArrowIds.Clear();
            if (arrowIds == null) return;

            var seen = new HashSet<int>();
            foreach (var arrowId in arrowIds)
            {
                if (seen.Add(arrowId))
                {
                    _problemArrowIds.Add(arrowId);
                }
            }
        }

        private void DrawSolutionOrderVisual()
        {
            if (_lastSolutionOrder == null || _lastSolutionOrder.Count == 0) return;
            if (!_isSolvable) return;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Solution Order:", EditorStyles.boldLabel);

            float squareSize = 26f;
            float arrowWidth = 14f;
            float maxRowWidth = position.width - LEFT_PANEL_WIDTH - 40;
            float currentRowWidth = 0f;

            EditorGUILayout.BeginHorizontal();

            for (int step = 0; step < _lastSolutionOrder.Count; step++)
            {
                int arrowId = _lastSolutionOrder[step];

                // ID로 화살표 찾기
                EditorArrow arrow = null;
                int arrowIndex = -1;
                for (int i = 0; i < _arrows.Count; i++)
                {
                    if (_arrows[i].id == arrowId)
                    {
                        arrow = _arrows[i];
                        arrowIndex = i;
                        break;
                    }
                }
                if (arrow == null) continue;

                // 줄바꿈
                float neededWidth = squareSize + (step < _lastSolutionOrder.Count - 1 ? arrowWidth : 0);
                if (currentRowWidth + neededWidth > maxRowWidth && currentRowWidth > 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    currentRowWidth = 0f;
                }

                Color arrowColor = GetColorForGameColor(arrow.color);
                bool isSelected = (_selectedArrowIndex == arrowIndex);

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = arrowColor;

                var btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fixedWidth = squareSize,
                    fixedHeight = squareSize,
                    fontSize = 12,
                    fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter
                };
                btnStyle.normal.textColor = GetContrastTextColor(arrowColor);

                // 선택된 상태 표시
                string label = isSelected ? $"[{step + 1}]" : (step + 1).ToString();
                if (GUILayout.Button(label, btnStyle))
                {
                    _selectedArrowIndex = arrowIndex;
                    Repaint();
                }

                GUI.backgroundColor = prevBg;
                currentRowWidth += squareSize;

                // 화살표 기호
                if (step < _lastSolutionOrder.Count - 1)
                {
                    GUILayout.Label("\u2192", GUILayout.Width(arrowWidth), GUILayout.Height(squareSize));
                    currentRowWidth += arrowWidth;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private Color GetContrastTextColor(Color bgColor)
        {
            float luminance = 0.299f * bgColor.r + 0.587f * bgColor.g + 0.114f * bgColor.b;
            return luminance > 0.5f ? Color.black : Color.white;
        }

        /// <summary>
        /// 드래그 경로 시각화
        /// </summary>
        private void DrawDragPath(Rect gridRect)
        {
            Color pathColor = GetColorForGameColor(_manualEditColor);
            pathColor.a = 0.6f; // 반투명

            for (int i = 0; i < _dragPath.Count; i++)
            {
                var cell = _dragPath[i];
                Rect cellRect = new Rect(
                    gridRect.x + cell.x * _currentCellSize + 2,
                    gridRect.y + (_gridHeight - 1 - cell.y) * _currentCellSize + 2,
                    _currentCellSize - 4,
                    _currentCellSize - 4
                );
                EditorGUI.DrawRect(cellRect, pathColor);

                // Tail 표시 (첫 번째 셀)
                if (i == 0)
                {
                    Handles.color = Color.white;
                    GUI.Label(cellRect, "T", new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    });
                }
                // Head 표시 (마지막 셀)
                else if (i == _dragPath.Count - 1)
                {
                    Handles.color = Color.yellow;
                    GUI.Label(cellRect, "H", new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.yellow }
                    });
                }
            }

            // 경로 연결선 그리기
            if (_dragPath.Count >= 2)
            {
                Handles.color = Color.white;
                for (int i = 0; i < _dragPath.Count - 1; i++)
                {
                    Vector2 from = GetCellCenter(gridRect, _dragPath[i]);
                    Vector2 to = GetCellCenter(gridRect, _dragPath[i + 1]);
                    Handles.DrawLine(new Vector3(from.x, from.y, 0), new Vector3(to.x, to.y, 0));
                }
            }
        }

        /// <summary>
        /// 셀 중심 좌표 계산
        /// </summary>
        private Vector2 GetCellCenter(Rect gridRect, Vector2Int cell)
        {
            return new Vector2(
                gridRect.x + cell.x * _currentCellSize + _currentCellSize / 2,
                gridRect.y + (_gridHeight - 1 - cell.y) * _currentCellSize + _currentCellSize / 2
            );
        }

        /// <summary>
        /// 그리드 클릭/드래그 이벤트 처리
        /// </summary>
        private void HandleGridClick(Rect gridRect)
        {
            Event e = Event.current;
            Vector2Int? cellUnderMouse = GetCellAtPosition(e.mousePosition, gridRect);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (gridRect.Contains(e.mousePosition) && cellUnderMouse.HasValue)
                    {
                        if (e.button == 0) // Left Click
                        {
                            int arrowIndex = FindArrowAtCell(cellUnderMouse.Value);

                            if (e.control && arrowIndex >= 0) // Ctrl + Left Click: 방향 순환
                            {
                                CycleArrowDirection(_arrows[arrowIndex]);
                                _selectedArrowIndex = arrowIndex;
                                ValidateSolvable();
                            }
                            else if (arrowIndex < 0) // 빈 셀에서 드래그 시작
                            {
                                _isDragging = true;
                                _dragPath.Clear();
                                _dragPath.Add(cellUnderMouse.Value);
                                _selectedArrowIndex = -1;
                            }
                            else // 기존 화살표 선택
                            {
                                _selectedArrowIndex = arrowIndex;
                                _statusMessage = $"Selected Arrow #{_arrows[arrowIndex].id}";
                            }
                        }
                        else if (e.button == 1) // Right Click: 삭제
                        {
                            RemoveArrowAtCell(cellUnderMouse.Value);
                            ValidateSolvable();
                        }
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_isDragging && cellUnderMouse.HasValue)
                    {
                        Vector2Int cell = cellUnderMouse.Value;

                        // 이미 경로에 있거나 다른 화살표가 점유한 셀은 제외
                        if (!_dragPath.Contains(cell) && FindArrowAtCell(cell) < 0)
                        {
                            // 마지막 셀과 인접한지 확인
                            if (_dragPath.Count > 0)
                            {
                                Vector2Int last = _dragPath[^1];
                                if (IsAdjacent(last, cell))
                                {
                                    _dragPath.Add(cell);
                                    Repaint();
                                }
                            }
                        }
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_isDragging && e.button == 0)
                    {
                        _isDragging = false;

                        // 최소 2칸 이상이면 화살표 생성
                        if (_dragPath.Count >= 2)
                        {
                            CreateArrowFromDragPath();
                            ValidateSolvable();
                        }
                        else if (_dragPath.Count == 1)
                        {
                            _statusMessage = "Drag at least 2 cells to create an arrow.";
                        }

                        _dragPath.Clear();
                        e.Use();
                        Repaint();
                    }
                    break;
            }
        }

        /// <summary>
        /// 마스크 그리기/지우기 처리
        /// </summary>
        private void HandleMaskDrawing(Rect gridRect)
        {
            Event e = Event.current;

            // 마스크가 없으면 초기화
            if (_shapeMask == null || _shapeMask.GetLength(0) != _gridWidth || _shapeMask.GetLength(1) != _gridHeight)
            {
                InitializeShapeMask();
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                case EventType.MouseDrag:
                    if (e.button == 0)
                    {
                        Vector2Int? cellUnderMouse = GetCellAtPosition(e.mousePosition, gridRect);
                        if (cellUnderMouse.HasValue)
                        {
                            Vector2Int cell = cellUnderMouse.Value;
                            _shapeMask[cell.x, cell.y] = _maskBrushMode;
                            _useShapeMask = true;
                            e.Use();
                            Repaint();
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 마우스 위치에서 셀 좌표 계산
        /// </summary>
        private Vector2Int? GetCellAtPosition(Vector2 mousePos, Rect gridRect)
        {
            if (!gridRect.Contains(mousePos))
                return null;

            float localX = mousePos.x - gridRect.x;
            float localY = mousePos.y - gridRect.y;

            int cellX = Mathf.FloorToInt(localX / _currentCellSize);
            int cellY = _gridHeight - 1 - Mathf.FloorToInt(localY / _currentCellSize);

            if (cellX >= 0 && cellX < _gridWidth && cellY >= 0 && cellY < _gridHeight)
                return new Vector2Int(cellX, cellY);

            return null;
        }

        /// <summary>
        /// 두 셀이 인접한지 확인
        /// </summary>
        private bool IsAdjacent(Vector2Int a, Vector2Int b)
        {
            Vector2Int diff = b - a;
            return (Mathf.Abs(diff.x) == 1 && diff.y == 0) ||
                   (diff.x == 0 && Mathf.Abs(diff.y) == 1);
        }

        /// <summary>
        /// 드래그 경로에서 화살표 생성
        /// </summary>
        private void CreateArrowFromDragPath()
        {
            if (_dragPath.Count < 2) return;

            // 경로: [0]=Tail, [^1]=Head
            // Head 방향 = Head에서 이전 셀 반대 방향 (탈출 방향)
            Vector2Int head = _dragPath[^1];
            Vector2Int prevCell = _dragPath[^2];
            
            // 마지막 이동 방향 = Head 직전셀에서 Head로 가는 방향
            ArrowDirection lastMoveDir = GetDirectionFromTo(prevCell, head);
            
            // Head 방향은 마지막 이동 방향과 동일해야 함 (규칙: Head 직전 셀은 탈출 방향과 같은 방향으로 연결)
            ArrowDirection headDir = lastMoveDir;

            int newId = _arrows.Count > 0 ? _arrows[^1].id + 1 : 1;

            _arrows.Add(new EditorArrow
            {
                id = newId,
                cells = new List<Vector2Int>(_dragPath),
                headDirection = headDir,
                color = _manualEditColor
            });

            _selectedArrowIndex = _arrows.Count - 1;
            _statusMessage = $"Created Arrow #{newId} (Length: {_dragPath.Count})";
        }

        /// <summary>
        /// 특정 셀에 있는 화살표 인덱스 찾기
        /// </summary>
        private int FindArrowAtCell(Vector2Int cell)
        {
            for (int i = 0; i < _arrows.Count; i++)
            {
                if (_arrows[i].cells.Contains(cell))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 특정 셀의 화살표 제거
        /// </summary>
        private void RemoveArrowAtCell(Vector2Int cell)
        {
            int index = FindArrowAtCell(cell);
            if (index >= 0)
            {
                int removedId = _arrows[index].id;
                _arrows.RemoveAt(index);

                if (_selectedArrowIndex == index)
                    _selectedArrowIndex = -1;
                else if (_selectedArrowIndex > index)
                    _selectedArrowIndex--;

                _statusMessage = $"Removed Arrow #{removedId}";
            }
            else
            {
                _statusMessage = "No arrow at this cell.";
            }
        }

        /// <summary>
        /// 화살표 방향을 순환 (Up → Right → Down → Left → Up...)
        /// </summary>
        private void CycleArrowDirection(EditorArrow arrow)
        {
            arrow.headDirection = arrow.headDirection switch
            {
                ArrowDirection.Up => ArrowDirection.Right,
                ArrowDirection.Right => ArrowDirection.Down,
                ArrowDirection.Down => ArrowDirection.Left,
                ArrowDirection.Left => ArrowDirection.Up,
                _ => ArrowDirection.Up
            };
            _statusMessage = $"Arrow #{arrow.id} direction changed to {arrow.headDirection}";
        }

        // ========== Solvable 검증 ==========

        /// <summary>
        /// 실시간 Solvable 검증 - 모든 화살표가 탈출 가능한지 체크
        /// </summary>
        private void ValidateSolvable()
        {
            if (_arrows.Count == 0)
            {
                _isSolvable = false;
                _solvableMessage = "";
                _lastValidationResult = null;
                _problemArrowIds.Clear();
                return;
            }

            // 시뮬레이션으로 검증
            var result = SimulateSolve();
            _isSolvable = result.success;
            _problemArrowIds.Clear();

            if (_isSolvable)
            {
                _lastSolutionOrder = result.order;
                _solvableMessage = $"SOLVABLE! Solution: {string.Join(" \u2192 ", result.order)}";
                _lastValidationResult = new SolvabilityValidator.ValidationResult
                {
                    solvable = true,
                    problemType = SolvabilityValidator.ProblemType.None,
                    simulatedOrder = new List<int>(result.order)
                };
            }
            else
            {
                _lastSolutionOrder = null;
                _solvableMessage = $"DEADLOCK! Arrow #{result.blockedArrowId} is blocked.";
                _lastValidationResult = new SolvabilityValidator.ValidationResult
                {
                    solvable = false,
                    problemType = SolvabilityValidator.ProblemType.Deadlock,
                    blockedArrowId = result.blockedArrowId,
                    error = _solvableMessage
                };
                if (result.blockedArrowId >= 0)
                {
                    SetProblemArrowIds(new List<int> { result.blockedArrowId });
                }
            }
        }

        /// <summary>
        /// 퍼즐 풀이 시뮬레이션
        /// </summary>
        private (bool success, List<int> order, int blockedArrowId) SimulateSolve()
        {
            // 화살표 복사본 생성
            var remaining = new List<EditorArrow>();
            foreach (var arrow in _arrows)
            {
                remaining.Add(new EditorArrow
                {
                    id = arrow.id,
                    cells = new List<Vector2Int>(arrow.cells),
                    headDirection = arrow.headDirection,
                    color = arrow.color
                });
            }

            var solveOrder = new List<int>();
            int maxIterations = remaining.Count * remaining.Count; // 무한 루프 방지
            int iterations = 0;

            while (remaining.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                bool foundEscapable = false;

                for (int i = 0; i < remaining.Count; i++)
                {
                    var arrow = remaining[i];
                    if (CanEscape(arrow, remaining))
                    {
                        solveOrder.Add(arrow.id);
                        remaining.RemoveAt(i);
                        foundEscapable = true;
                        break;
                    }
                }

                if (!foundEscapable)
                {
                    // Deadlock - 탈출 불가능한 화살표 찾기
                    int blockedId = remaining.Count > 0 ? remaining[0].id : -1;
                    return (false, solveOrder, blockedId);
                }
            }

            return (true, solveOrder, -1);
        }

        /// <summary>
        /// 화살표가 탈출 가능한지 확인
        /// </summary>
        private bool CanEscape(EditorArrow arrow, List<EditorArrow> allArrows)
        {
            if (arrow.cells.Count == 0) return true;

            Vector2Int head = arrow.cells[^1];
            Vector2Int dir = GetDirectionVector(arrow.headDirection);

            // Head 방향으로 이동하면서 경계까지 갈 수 있는지 체크
            Vector2Int current = head;

            while (true)
            {
                current += dir;

                // 그리드 밖으로 나가면 탈출 성공
                if (current.x < 0 || current.x >= _gridWidth ||
                    current.y < 0 || current.y >= _gridHeight)
                {
                    return true;
                }

                // 다른 화살표에 막혀 있는지 체크
                foreach (var other in allArrows)
                {
                    if (other.id == arrow.id) continue;
                    if (other.cells.Contains(current))
                    {
                        return false; // 막혀있음
                    }
                }
            }
        }

        private void DrawArrow(Rect gridRect, EditorArrow arrow)
        {
            Color arrowColor = GetColorForGameColor(arrow.color);
            float lineWidth = 4f;

            // 선형으로 화살표 그리기 (셀 중심을 연결하는 선)
            if (arrow.cells.Count >= 2)
            {
                Handles.color = arrowColor;

                for (int i = 0; i < arrow.cells.Count - 1; i++)
                {
                    Vector2Int from = arrow.cells[i];
                    Vector2Int to = arrow.cells[i + 1];

                    int dx = to.x - from.x;
                    int dy = to.y - from.y;

                    Vector3 fromPos = CellToScreenPos(gridRect, from);
                    Vector3 toPos = CellToScreenPos(gridRect, to);

                    // 대각선인 경우 (X와 Y 모두 변화) - 코너 포인트 삽입하여 직각으로 그리기
                    if (dx != 0 && dy != 0)
                    {
                        // 코너 포인트 계산 (먼저 X 이동, 그 다음 Y 이동)
                        Vector2Int corner = new Vector2Int(to.x, from.y);
                        Vector3 cornerPos = CellToScreenPos(gridRect, corner);

                        Handles.DrawAAPolyLine(lineWidth, fromPos, cornerPos);
                        Handles.DrawAAPolyLine(lineWidth, cornerPos, toPos);
                    }
                    else
                    {
                        // 직선인 경우 그대로 연결
                        Handles.DrawAAPolyLine(lineWidth, fromPos, toPos);
                    }
                }
            }

            // Tail 표시 (원형)
            if (arrow.cells.Count > 0)
            {
                Vector2Int tail = arrow.cells[0];
                Vector2 tailCenter = CellToScreenPos(gridRect, tail);
                Handles.color = arrowColor;
                Handles.DrawSolidDisc(tailCenter, Vector3.forward, Mathf.Clamp(_currentCellSize * 0.2f, 3f, 8f));
            }

            // Head 표시 (삼각형)
            if (arrow.cells.Count > 0)
            {
                Vector2Int head = arrow.cells[^1];
                Vector2 headCenter = CellToScreenPos(gridRect, head);

                DrawArrowHead(headCenter, arrow.headDirection, arrowColor);
            }
        }

        private Vector3 CellToScreenPos(Rect gridRect, Vector2Int cell)
        {
            return new Vector3(
                gridRect.x + cell.x * _currentCellSize + _currentCellSize / 2,
                gridRect.y + (_gridHeight - 1 - cell.y) * _currentCellSize + _currentCellSize / 2,
                0
            );
        }

        private void DrawArrowHead(Vector2 center, ArrowDirection dir, Color color)
        {
            float size = Mathf.Clamp(_currentCellSize * 0.25f, 4f, 10f);
            Vector3[] triangle = new Vector3[3];

            switch (dir)
            {
                case ArrowDirection.Up:
                    triangle[0] = new Vector3(center.x, center.y - size);
                    triangle[1] = new Vector3(center.x - size, center.y + size);
                    triangle[2] = new Vector3(center.x + size, center.y + size);
                    break;
                case ArrowDirection.Down:
                    triangle[0] = new Vector3(center.x, center.y + size);
                    triangle[1] = new Vector3(center.x - size, center.y - size);
                    triangle[2] = new Vector3(center.x + size, center.y - size);
                    break;
                case ArrowDirection.Left:
                    triangle[0] = new Vector3(center.x - size, center.y);
                    triangle[1] = new Vector3(center.x + size, center.y - size);
                    triangle[2] = new Vector3(center.x + size, center.y + size);
                    break;
                case ArrowDirection.Right:
                    triangle[0] = new Vector3(center.x + size, center.y);
                    triangle[1] = new Vector3(center.x - size, center.y - size);
                    triangle[2] = new Vector3(center.x - size, center.y + size);
                    break;
            }

            Handles.color = color;
            Handles.DrawAAConvexPolygon(triangle);

            // 어두운 색상 가시성을 위한 반투명 흰색 외곽선
            Handles.color = new Color(1f, 1f, 1f, 0.5f);
            Handles.DrawPolyLine(triangle[0], triangle[1], triangle[2], triangle[0]);
        }

        private void DrawStatusMessage()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }
        }

        // ========== 퍼즐 생성 ==========

        /// <summary>
        /// Maze-based Dependency Chain Generation 알고리즘
        /// - Fill Rate 100% 보장
        /// - 정돈된 패턴 (ㄱ, ㄴ, ㄷ, ㅁ)
        /// - 난이도 기반 의존성 조절
        /// </summary>
        private void GeneratePuzzleMazeBased()
        {
            _statusMessage = "Maze-based generation has been removed. Use Reverse Growth instead.";
            Debug.LogWarning("[LevelEditor] Maze-based generation is no longer supported. Use Reverse Growth instead.");
            Repaint();
        }

        private void GeneratePuzzleMazeGuided()
        {
            _statusMessage = "Maze-guided generation has been removed. Use Reverse Growth instead.";
            Debug.LogWarning("[LevelEditor] Maze-guided generation is no longer supported. Use Reverse Growth instead.");
            Repaint();
        }

        /// <summary>
        /// Reverse Growth: ValidTargets 확장 알고리즘
        /// - ValidTargets = 경계 + 기존 화살표 Body
        /// - Head가 ValidTarget을 향하면 탈출 가능
        /// - 내부 Head 허용, 의존성 자동 생성
        /// </summary>
        private void GeneratePuzzleReverseGrowth()
        {
            _arrows.Clear();

            // 항상 랜덤 시드 사용
            int currentSeed = Random.Range(0, int.MaxValue);

            Debug.Log($"[LevelEditor] ========== Reverse Growth 퍼즐 생성 시작 ==========");
            Debug.Log($"[LevelEditor] 그리드: {_gridWidth}x{_gridHeight}, Seed: {currentSeed}");

            // 파라미터 준비
            var (minLen, maxLen) = GetArrowLengthRange();
            int colorCount = GetAvailableColors().Count;
            int maxFreeArrows = GetMaxFreeArrowsFromDifficulty();

            Debug.Log($"[LevelEditor] 난이도: {_difficultyLevel} ({GetDifficultyLabel(_difficultyLevel)}), maxFreeArrows: {(maxFreeArrows == 0 ? "제한 없음" : maxFreeArrows.ToString())}");

            // Best-Effort 재시도 루프
            // 난이도 조건을 만족하는 퍼즐을 찾되, 못 찾으면 가장 가까운 결과를 사용
            int maxRetries = _difficultyLevel <= 1 ? 10 : Mathf.Min(10 + _difficultyLevel * 3, 50);
            List<EditorArrow> arrows = null;
            List<int> solutionOrder = null;

            // Best-effort 추적: 가장 낮은 maxSimFree를 가진 결과 저장
            List<EditorArrow> bestArrows = null;
            List<int> bestSolutionOrder = null;
            int bestMaxSimFree = int.MaxValue;
            bool exactMatch = false;
            bool cancelled = false;

            try
            {
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    // Progress Bar: 시도 횟수 표시 + Cancel 체크
                    float retryProgress = (float)retry / maxRetries;
                    string retryInfo = $"시도 {retry + 1}/{maxRetries}";
                    if (bestMaxSimFree < int.MaxValue)
                        retryInfo += $" | Best: maxFree={bestMaxSimFree}";

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Level 생성 중...", retryInfo, retryProgress))
                    {
                        cancelled = true;
                        Debug.Log($"[LevelEditor] 사용자가 생성을 취소했습니다. (시도 {retry}회)");
                        break;
                    }

                    if (retry > 0)
                    {
                        currentSeed = Random.Range(0, int.MaxValue);
                        if (retry % 5 == 0)
                            Debug.Log($"[LevelEditor] 재시도 {retry}/{maxRetries}, 새 Seed: {currentSeed}, bestMaxFree={bestMaxSimFree}");
                    }

                    // Generator 초기화
                    _reverseGrowthGenerator = new ReverseGrowthGenerator(currentSeed);
                    _reverseGrowthGenerator.SetContext(_gridWidth, _gridHeight, _shapeMask, _useShapeMask);
                    _reverseGrowthGenerator.SetParameters(minLen, maxLen, colorCount);
                    _reverseGrowthGenerator.SetDifficultyParameters(maxFreeArrows);
                    _reverseGrowthGenerator.SetGeometricPatterns(
                        _useGeometricPatterns, BuildGeometricPatternFlags());
                    _reverseGrowthGenerator.SetColorMap(_colorMap, _useColorMapping);

                    // Progress 콜백: Generator 내부 Phase 진행률을 Progress Bar에 반영
                    // DisplayCancelableProgressBar 사용으로 항상 Cancel 버튼 노출
                    int currentRetry = retry;
                    _reverseGrowthGenerator.SetProgressCallback((message, phaseProgress) =>
                    {
                        float overall = ((float)currentRetry + phaseProgress) / maxRetries;
                        return EditorUtility.DisplayCancelableProgressBar(
                            "Level 생성 중...",
                            $"시도 {currentRetry + 1}/{maxRetries} | {message}",
                            overall);
                    });

                    // 생성
                    var result = _reverseGrowthGenerator.Generate();
                    var candidateArrows = result.Item1;
                    var candidateOrder = result.Item2;

                    if (candidateArrows == null || candidateArrows.Count == 0)
                        continue;

                    // 난이도 분석
                    if (maxFreeArrows > 0)
                    {
                        var (maxSimFree, _) = SolvabilityValidator.AnalyzeDifficulty(
                            candidateArrows, _gridWidth, _gridHeight);

                        // Best-effort: 더 나은 결과면 저장
                        if (maxSimFree < bestMaxSimFree)
                        {
                            bestMaxSimFree = maxSimFree;
                            bestArrows = candidateArrows;
                            bestSolutionOrder = candidateOrder;
                        }

                        // 정확히 목표 달성
                        if (maxSimFree <= maxFreeArrows)
                        {
                            arrows = candidateArrows;
                            solutionOrder = candidateOrder;
                            exactMatch = true;
                            Debug.Log($"[LevelEditor] 난이도 목표 달성! maxFree={maxSimFree} ≤ target={maxFreeArrows} (시도 {retry + 1}회)");
                            break;
                        }
                    }
                    else
                    {
                        // 난이도 제한 없음 — 첫 성공 결과 사용
                        arrows = candidateArrows;
                        solutionOrder = candidateOrder;
                        exactMatch = true;
                        Debug.Log($"[LevelEditor] 퍼즐 생성 성공! (시도 횟수: {retry + 1}, Seed: {currentSeed})");
                        break;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // 취소 시: best-effort 결과가 있으면 사용
            if (cancelled && !exactMatch && bestArrows != null)
            {
                arrows = bestArrows;
                solutionOrder = bestSolutionOrder;
                Debug.Log($"[LevelEditor] 취소됨 - best-effort 결과 사용: maxFree={bestMaxSimFree}");
            }

            // 정확한 매칭을 못 찾았으면 가장 가까운 결과 사용
            if (!exactMatch && !cancelled && bestArrows != null)
            {
                arrows = bestArrows;
                solutionOrder = bestSolutionOrder;
                Debug.LogWarning($"[LevelEditor] 난이도 목표 미달, best-effort 사용: maxFree={bestMaxSimFree} (목표: {maxFreeArrows})");
            }

            if (arrows != null && arrows.Count > 0)
            {
                _arrows = arrows;
                _lastSolutionOrder = solutionOrder;  // solutionOrder 저장

                // 검증
                var (valid, errors) = ArrowValidator.ValidateAll(_arrows, _gridWidth, _gridHeight);
                if (!valid)
                {
                    Debug.LogError("[LevelEditor] 생성된 화살표 검증 실패:");
                    foreach (var error in errors)
                    {
                        Debug.LogError($"  - {error}");
                    }
                }

                // Solvable 검증 - ReverseGrowthGenerator가 생성한 solutionOrder 사용
                // 핵심 수정: SimulateSolve() 대신 SolvabilityValidator.Validate() 사용
                // SimulateSolve()는 Greedy 알고리즘으로 다른 순서를 생성하여 불일치 발생 가능
                _lastValidationResult = SolvabilityValidator.ValidateDetailed(
                    _arrows, solutionOrder, _gridWidth, _gridHeight);
                _isSolvable = _lastValidationResult.solvable;
                SetProblemArrowIds(_lastValidationResult.problemArrowIds);

                _solvableMessage = _isSolvable
                    ? $"Solvable! 해답 순서: {string.Join(" → ", solutionOrder)}"
                    : $"Not Solvable! {_lastValidationResult.error}";

                // Fill Rate 계산
                int totalCells = _useShapeMask && _shapeMask != null ? CountActiveCells() : _gridWidth * _gridHeight;
                int arrowCells = 0;
                foreach (var arrow in _arrows)
                    arrowCells += arrow.cells.Count;
                float fillRate = (float)arrowCells / totalCells * 100f;

                // 내부 Head 통계
                int internalHeads = CountInternalHeads();
                float internalHeadRate = (float)internalHeads / _arrows.Count * 100f;

                // 난이도 분석
                var (maxSimFree, freePerStep) = SolvabilityValidator.AnalyzeDifficulty(
                    _arrows, _gridWidth, _gridHeight);
                _lastMaxSimultaneousFree = maxSimFree;

                string difficultyNote = "";
                if (maxFreeArrows > 0 && !exactMatch)
                {
                    difficultyNote = $"\n⚠ 난이도 목표 미달 (목표: {maxFreeArrows}, 실제: {maxSimFree})";
                }

                string cancelNote = cancelled ? " (취소됨 - best-effort)" : "";
                _statusMessage = $"Generated {_arrows.Count} arrows (Reverse Growth){cancelNote}\n" +
                               $"Fill Rate: {fillRate:F1}%\n" +
                               $"Internal Heads: {internalHeads}/{_arrows.Count} ({internalHeadRate:F1}%)\n" +
                               $"Max Simultaneous Free: {maxSimFree}{difficultyNote}";

                Debug.Log($"[LevelEditor] ========== 생성 완료 ==========");
                Debug.Log($"[LevelEditor] 화살표 수: {_arrows.Count}, Fill Rate: {fillRate:F1}%");
                Debug.Log($"[LevelEditor] 내부 Head: {internalHeads}/{_arrows.Count} ({internalHeadRate:F1}%)");
                Debug.Log($"[LevelEditor] Max Simultaneous Free: {maxSimFree}");
                Debug.Log($"[LevelEditor] {_solvableMessage}");
            }
            else
            {
                _lastMaxSimultaneousFree = -1;
                if (cancelled)
                {
                    _statusMessage = "생성이 취소되었습니다.";
                }
                else
                {
                    string difficultyInfo = maxFreeArrows > 0
                        ? $"\n난이도 제한(maxFree={maxFreeArrows})이 너무 엄격할 수 있습니다. 난이도를 낮추거나 그리드 크기를 조정해보세요."
                        : "";
                    _statusMessage = $"Failed to generate puzzle after {maxRetries} attempts!\n" +
                                   $"사이클이 반복적으로 발생합니다. 그리드 크기를 줄이거나 화살표 길이를 조정해보세요.{difficultyInfo}";
                    Debug.LogError($"[LevelEditor] Reverse Growth 퍼즐 생성 실패 (최대 재시도 {maxRetries}회 초과)");
                }
            }

            Repaint();
        }

        /// <summary>
        /// 내부 Head 개수 계산 (경계가 아닌 Head)
        /// </summary>
        private int CountInternalHeads()
        {
            int count = 0;
            foreach (var arrow in _arrows)
            {
                if (arrow.cells.Count == 0) continue;
                Vector2Int head = arrow.cells[arrow.cells.Count - 1];

                // 경계에 있지 않으면 내부 Head
                if (head.x > 0 && head.x < _gridWidth - 1 &&
                    head.y > 0 && head.y < _gridHeight - 1)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Legacy: 순차 배치 + Solvable 검증 알고리즘
        /// - 그리드 내부 어디서든 시작 가능
        /// - 길고 구불구불한 화살표 우선 생성
        /// - 배치 후 Solvable 검증, 실패 시 롤백
        /// </summary>
        private void GeneratePuzzle()
        {
            _statusMessage = "Legacy generation has been removed. Use Reverse Growth instead.";
            Debug.LogWarning("[LevelEditor] Legacy generation is no longer supported. Use Reverse Growth instead.");
            Repaint();
        }

        /// <summary>
        /// Head 직전 셀이 탈출 방향과 정렬되도록 셀 추가
        /// 규칙: Head 직전 셀 → Head 연결 방향 = headDirection
        /// </summary>
        private bool EnsureHeadAlignedWithDirection(List<Vector2Int> cells, ArrowDirection headDir, bool[,] occupied)
        {
            if (cells.Count < 2) return true; // 1칸짜리는 처리 불필요

            Vector2Int head = cells[^1];
            Vector2Int prevCell = cells[^2];
            ArrowDirection lastMoveDir = GetDirectionFromTo(prevCell, head);

            // 이미 정렬되어 있으면 OK
            if (lastMoveDir == headDir)
                return true;

            // 최대 길이에 도달했으면 셀 추가 불가
            var (_, maxLen) = GetArrowLengthRange();
            if (cells.Count >= maxLen)
                return false;

            // 정렬되어 있지 않으면, headDir 방향으로 셀 하나 추가 시도
            Vector2Int newHead = head + GetDirectionVector(headDir);

            // 새 셀이 유효한지 검사 (그리드 내, 비어있음, 이미 cells에 없음)
            if (IsValidCell(newHead) &&
                !occupied[newHead.x, newHead.y] &&
                !cells.Contains(newHead))
            {
                cells.Add(newHead);
                return true;
            }

            // 셀 추가 불가 - 실패
            return false;
        }

        /// <summary>
        /// 길고 구불구불한 화살표 생성
        /// - 중앙에 가까운 시작점 우선 (내부에서 외부로 확장)
        /// - 꺾임 우선 성장
        /// - 탈출 가능한 방향 설정
        /// </summary>
        // 실패 원인 통계 (분석용)
        private int _failReason_NoEmptyCells = 0;
        private int _failReason_TooShort = 0;
        private int _failReason_NoEscapeDir = 0;
        private int _failReason_HeadAlignment = 0;

        // 탈출 가능 셀 필터링 통계
        private int _escapableCellsTotal = 0;
        private int _escapableCellsFiltered = 0;

        private EditorArrow TryCreateWindingArrow(bool[,] occupied, int id)
        {
            // 1. 빈 셀 중 탈출 가능한 셀만 필터링
            var emptyCells = GetEmptyCells(occupied);
            if (emptyCells.Count == 0)
            {
                _failReason_NoEmptyCells++;
                return null;
            }

            // 탈출 가능하고 인접 빈 셀이 있는 셀만 후보로 (성장 가능해야 함)
            var growableCells = new List<Vector2Int>();
            foreach (var cell in emptyCells)
            {
                if (HasAnyEscapeDirection(cell, occupied) && HasAdjacentEmptyCell(cell, occupied))
                {
                    growableCells.Add(cell);
                }
            }

            // 통계 기록
            _escapableCellsTotal += emptyCells.Count;
            _escapableCellsFiltered += growableCells.Count;

            // 성장 가능한 셀이 없으면 탈출 가능 셀, 그래도 없으면 일반 빈 셀 사용
            List<Vector2Int> candidateCells;
            if (growableCells.Count > 0)
            {
                candidateCells = growableCells;
            }
            else
            {
                // fallback: 탈출 가능하기만 한 셀
                var escapableCells = emptyCells.FindAll(c => HasAnyEscapeDirection(c, occupied));
                candidateCells = escapableCells.Count > 0 ? escapableCells : emptyCells;
            }
            Vector2Int startCell = SelectCellNearCenter(candidateCells);

            // 2. 구불구불하게 성장 (꺾임 우선)
            List<Vector2Int> cells = GrowWindingPath(startCell, occupied);

            if (cells.Count < 2)
            {
                // 최소 2칸 필요, 1칸이면 나중에 FillRemaining에서 처리
                _failReason_TooShort++;
                return null;
            }

            // 3. Head 위치 결정 - 더 내부에 있는 끝점을 Head로 선택
            //    (Head가 내부에 있어야 다른 화살표에 막혀서 재미있는 퍼즐이 됨)
            Vector2Int firstCell = cells[0];
            Vector2Int lastCell = cells[^1];
            float firstDistToEdge = GetMinDistanceToEdge(firstCell);
            float lastDistToEdge = GetMinDistanceToEdge(lastCell);

            // 더 내부에 있는 쪽(경계에서 먼 쪽)을 Head로 시도
            if (firstDistToEdge > lastDistToEdge)
            {
                cells.Reverse(); // firstCell이 더 내부 → 그쪽을 Head로
            }

            Vector2Int head = cells[^1];
            ArrowDirection? escapeDir = FindValidEscapeDirection(head, cells, occupied);

            if (!escapeDir.HasValue)
            {
                // 탈출 방향 없음 - 반대쪽을 Head로 시도
                cells.Reverse();
                head = cells[^1];
                escapeDir = FindValidEscapeDirection(head, cells, occupied);

                if (!escapeDir.HasValue)
                {
                    // 여전히 없으면 실패
                    _failReason_NoEscapeDir++;
                    return null;
                }
            }

            // 5. Head 직전 셀이 탈출 방향과 정렬되도록 보정
            if (!EnsureHeadAlignedWithDirection(cells, escapeDir.Value, occupied))
            {
                // 정렬 실패 - 반대쪽(Tail)을 Head로 다시 시도
                cells.Reverse();
                head = cells[^1];
                escapeDir = FindValidEscapeDirection(head, cells, occupied);

                if (!escapeDir.HasValue || !EnsureHeadAlignedWithDirection(cells, escapeDir.Value, occupied))
                {
                    _failReason_HeadAlignment++;
                    return null;
                }
            }

            // 색상 결정
            GameColor arrowColor;
            if (_useColorMapping && _colorMap != null)
            {
                // Color Mapping: 화살표 셀들의 가장 흔한 색상 사용
                arrowColor = GetDominantColorForCells(cells);
            }
            else
            {
                // 랜덤 색상
                var availableColors = GetAvailableColors();
                arrowColor = availableColors[Random.Range(0, availableColors.Count)];
            }

            return new EditorArrow
            {
                id = id,
                cells = cells,
                headDirection = escapeDir.Value,
                color = arrowColor
            };
        }

        /// <summary>
        /// 셀 목록에서 가장 흔한 매핑 색상 반환
        /// </summary>
        private GameColor GetDominantColorForCells(List<Vector2Int> cells)
        {
            if (_colorMap == null || cells.Count == 0)
            {
                var availableColors = GetAvailableColors();
                return availableColors[Random.Range(0, availableColors.Count)];
            }

            var colorCount = new Dictionary<GameColor, int>();
            foreach (var cell in cells)
            {
                if (cell.x >= 0 && cell.x < _gridWidth && cell.y >= 0 && cell.y < _gridHeight)
                {
                    var color = _colorMap[cell.x, cell.y];
                    if (!colorCount.ContainsKey(color))
                        colorCount[color] = 0;
                    colorCount[color]++;
                }
            }

            GameColor dominant = GameColor.Red;
            int maxCount = 0;
            foreach (var kvp in colorCount)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    dominant = kvp.Key;
                }
            }
            return dominant;
        }

        /// <summary>
        /// 빈 셀 목록 반환
        /// </summary>
        private List<Vector2Int> GetEmptyCells(bool[,] occupied)
        {
            var result = new List<Vector2Int>();
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (!occupied[x, y])
                        result.Add(new Vector2Int(x, y));
                }
            }
            return result;
        }

        /// <summary>
        /// 셀 목록에서 그리드 중앙에 가까운 셀 선택 (약간의 랜덤성 포함)
        /// </summary>
        private Vector2Int SelectCellNearCenter(List<Vector2Int> cells)
        {
            if (cells.Count == 0) return Vector2Int.zero;
            if (cells.Count == 1) return cells[0];

            Vector2 center = new Vector2(_gridWidth / 2f, _gridHeight / 2f);

            // 중앙까지의 거리로 정렬 (LINQ 없이)
            var sorted = new List<Vector2Int>(cells);
            sorted.Sort((a, b) =>
                Vector2.Distance(a, center).CompareTo(Vector2.Distance(b, center)));

            // 상위 30% 중에서 랜덤 선택 (완전 결정적이면 항상 같은 패턴)
            int topCount = Mathf.Max(1, sorted.Count / 3);
            return sorted[Random.Range(0, topCount)];
        }

        /// <summary>
        /// 구불구불한 경로 성장 (꺾임 우선)
        /// </summary>
        private List<Vector2Int> GrowWindingPath(Vector2Int start, bool[,] occupied)
        {
            List<Vector2Int> cells = new List<Vector2Int> { start };
            Vector2Int current = start;

            var (minLen, maxLen) = GetArrowLengthRange();
            // 사용자 설정 범위 내에서 목표 길이 결정
            int targetLength = Random.Range(minLen, maxLen + 1);

            ArrowDirection? lastDir = null;
            int bendProbability = 70; // 70% 확률로 꺾임 시도

            while (cells.Count < targetLength)
            {
                // 다음 방향 결정
                ArrowDirection nextDir;

                if (lastDir.HasValue && Random.Range(0, 100) >= bendProbability)
                {
                    // 같은 방향 유지
                    nextDir = lastDir.Value;
                }
                else
                {
                    // 꺾임: 새로운 방향 선택 (이전 방향과 반대 제외)
                    nextDir = GetRandomBendDirection(lastDir);
                }

                Vector2Int next = current + GetDirectionVector(nextDir);

                // 유효성 검사
                if (!IsValidCell(next) || occupied[next.x, next.y] || cells.Contains(next))
                {
                    // 다른 방향 시도
                    bool found = false;
                    foreach (var dir in GetShuffledDirections())
                    {
                        if (lastDir.HasValue && dir == GetOppositeDirection(lastDir.Value))
                            continue; // 뒤로 가기 금지

                        Vector2Int tryNext = current + GetDirectionVector(dir);
                        if (IsValidCell(tryNext) && !occupied[tryNext.x, tryNext.y] && !cells.Contains(tryNext))
                        {
                            nextDir = dir;
                            next = tryNext;
                            found = true;
                            break;
                        }
                    }

                    if (!found) break; // 더 이상 성장 불가
                }

                cells.Add(next);
                current = next;
                lastDir = nextDir;
            }

            return cells;
        }

        /// <summary>
        /// 랜덤 꺾임 방향 선택 (이전 방향의 반대 제외)
        /// </summary>
        private ArrowDirection GetRandomBendDirection(ArrowDirection? lastDir)
        {
            var dirs = GetShuffledDirections();

            if (lastDir.HasValue)
            {
                // 반대 방향 제외
                dirs.Remove(GetOppositeDirection(lastDir.Value));

                // 수직 방향 우선 (꺾임 효과)
                var perpendicular = GetPerpendicularDirections(lastDir.Value);
                if (perpendicular.Count > 0 && Random.Range(0, 100) < 80)
                {
                    return perpendicular[Random.Range(0, perpendicular.Count)];
                }
            }

            return dirs.Count > 0 ? dirs[0] : ArrowDirection.Up;
        }

        /// <summary>
        /// 수직 방향 목록 반환
        /// </summary>
        private List<ArrowDirection> GetPerpendicularDirections(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up or ArrowDirection.Down =>
                    new List<ArrowDirection> { ArrowDirection.Left, ArrowDirection.Right },
                ArrowDirection.Left or ArrowDirection.Right =>
                    new List<ArrowDirection> { ArrowDirection.Up, ArrowDirection.Down },
                _ => new List<ArrowDirection>()
            };
        }

        /// <summary>
        /// 특정 셀에서 어떤 방향으로든 탈출 가능한지 확인
        /// 시작 셀 선택 시 탈출 가능성을 미리 검증
        /// </summary>
        private bool HasAnyEscapeDirection(Vector2Int cell, bool[,] occupied)
        {
            // 4방향 중 하나라도 탈출 가능하면 true
            foreach (ArrowDirection dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                // 빈 셀 리스트 (시작 셀만 포함)
                var singleCell = new List<Vector2Int> { cell };

                if (CanEscapeToDirection(cell, dir, singleCell, occupied))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 특정 셀에 인접한 빈 셀이 있는지 확인
        /// 시작 셀에서 최소 1칸은 성장할 수 있어야 함
        /// </summary>
        private bool HasAdjacentEmptyCell(Vector2Int cell, bool[,] occupied)
        {
            foreach (ArrowDirection dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                Vector2Int neighbor = cell + GetDirectionVector(dir);
                if (IsValidCell(neighbor) && !occupied[neighbor.x, neighbor.y])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 유효한 탈출 방향 찾기
        /// - 자신의 몸통에 막히지 않음
        /// - 기존 화살표에 막히지 않음 (현재 시점)
        /// </summary>
        private ArrowDirection? FindValidEscapeDirection(Vector2Int head, List<Vector2Int> arrowCells, bool[,] occupied)
        {
            // 경��에서 먼 방향 우선 (Head가 내부에 배치되어 다른 화살표에 막히도록)
            var candidates = new List<(ArrowDirection dir, int distance)>();

            // 각 방향 검사
            foreach (ArrowDirection dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                if (CanEscapeToDirection(head, dir, arrowCells, occupied))
                {
                    int distance = GetDistanceToBoundary(head, dir);
                    candidates.Add((dir, distance));
                }
            }

            if (candidates.Count == 0)
                return null;

            // 경계에서 가장 먼 방향 선택 (내부 배치 유도)
            candidates.Sort((a, b) => b.distance.CompareTo(a.distance));
            return candidates[0].dir;
        }

        /// <summary>
        /// 특정 방향으로 경계까지 탈출 가능한지 확인
        /// </summary>
        private bool CanEscapeToDirection(Vector2Int head, ArrowDirection dir, List<Vector2Int> arrowCells, bool[,] occupied)
        {
            Vector2Int dirVec = GetDirectionVector(dir);
            Vector2Int current = head + dirVec;

            while (current.x >= 0 && current.x < _gridWidth &&
                   current.y >= 0 && current.y < _gridHeight)
            {
                // 마스크 사용 시: 마스크 외부로 나가면 탈출 성공
                if (_useShapeMask && _shapeMask != null && !_shapeMask[current.x, current.y])
                {
                    return true; // 마스크 경계 도달 = 탈출 성공
                }

                // 자신의 몸통에 막히면 불가
                if (arrowCells.Contains(current))
                    return false;

                // 다른 화살표에 막히면 불가
                if (occupied[current.x, current.y])
                    return false;

                current += dirVec;
            }

            return true; // 그리드 경계 도달 성공
        }

        /// <summary>
        /// 특정 방향으로 경계까지의 거리
        /// </summary>
        private int GetDistanceToBoundary(Vector2Int cell, ArrowDirection dir)
        {
            // 마스크 사용 시: 마스크 경계까지의 거리 계산
            if (_useShapeMask && _shapeMask != null)
            {
                Vector2Int dirVec = GetDirectionVector(dir);
                Vector2Int current = cell + dirVec;
                int distance = 0;

                while (current.x >= 0 && current.x < _gridWidth &&
                       current.y >= 0 && current.y < _gridHeight &&
                       _shapeMask[current.x, current.y])
                {
                    distance++;
                    current += dirVec;
                }
                return distance;
            }

            // 마스크 미사용 시: 그리드 경계까지의 거리
            return dir switch
            {
                ArrowDirection.Up => _gridHeight - 1 - cell.y,
                ArrowDirection.Down => cell.y,
                ArrowDirection.Right => _gridWidth - 1 - cell.x,
                ArrowDirection.Left => cell.x,
                _ => int.MaxValue
            };
        }

        /// <summary>
        /// 셀에서 가장 가까운 경계까지의 거리 (4방향 중 최소값)
        /// 값이 클수록 내부에 위치
        /// </summary>
        private float GetMinDistanceToEdge(Vector2Int cell)
        {
            int distUp = GetDistanceToBoundary(cell, ArrowDirection.Up);
            int distDown = GetDistanceToBoundary(cell, ArrowDirection.Down);
            int distRight = GetDistanceToBoundary(cell, ArrowDirection.Right);
            int distLeft = GetDistanceToBoundary(cell, ArrowDirection.Left);
            return Mathf.Min(distUp, distDown, distRight, distLeft);
        }

        /// <summary>
        /// 빈 셀들을 FloodFill로 연결된 그룹으로 분류
        /// </summary>
        private List<List<Vector2Int>> GroupEmptyCells(bool[,] occupied)
        {
            var groups = new List<List<Vector2Int>>();
            bool[,] visited = new bool[_gridWidth, _gridHeight];

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (occupied[x, y] || visited[x, y]) continue;

                    // FloodFill로 연결된 셀 수집
                    var group = new List<Vector2Int>();
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        group.Add(cell);

                        foreach (var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        {
                            var next = cell + dir;
                            if (next.x >= 0 && next.x < _gridWidth &&
                                next.y >= 0 && next.y < _gridHeight &&
                                !occupied[next.x, next.y] && !visited[next.x, next.y])
                            {
                                visited[next.x, next.y] = true;
                                queue.Enqueue(next);
                            }
                        }
                    }

                    if (group.Count > 0)
                        groups.Add(group);
                }
            }

            // 큰 그룹 먼저 처리
            groups.Sort((a, b) => b.Count.CompareTo(a.Count));
            return groups;
        }

        /// <summary>
        /// 그룹 내에서 긴 화살표 생성 시도
        /// </summary>
        private EditorArrow TryCreateArrowFromGroup(List<Vector2Int> group, bool[,] occupied, int id)
        {
            if (group.Count < _minArrowLength)
                return null;

            // 그룹 내에서 랜덤 시작점
            Vector2Int start = group[Random.Range(0, group.Count)];

            // 그룹 내에서만 성장하도록 임시 occupied 마스크 생성
            bool[,] groupMask = new bool[_gridWidth, _gridHeight];
            for (int x = 0; x < _gridWidth; x++)
                for (int y = 0; y < _gridHeight; y++)
                    groupMask[x, y] = occupied[x, y];

            // 그룹 외의 셀은 점유된 것으로 처리 (그룹 내에서만 이동)
            var groupSet = new HashSet<Vector2Int>(group);
            for (int x = 0; x < _gridWidth; x++)
                for (int y = 0; y < _gridHeight; y++)
                    if (!groupSet.Contains(new Vector2Int(x, y)))
                        groupMask[x, y] = true;

            // 성장
            List<Vector2Int> cells = GrowWindingPath(start, groupMask);

            if (cells.Count < _minArrowLength)
                return null;

            // 탈출 방향 찾기
            Vector2Int head = cells[^1];
            ArrowDirection? escapeDir = FindValidEscapeDirection(head, cells, occupied);

            if (!escapeDir.HasValue)
            {
                cells.Reverse();
                head = cells[^1];
                escapeDir = FindValidEscapeDirection(head, cells, occupied);
            }

            if (!escapeDir.HasValue)
                return null;

            // Head 정렬
            if (!EnsureHeadAlignedWithDirection(cells, escapeDir.Value, occupied))
            {
                cells.Reverse();
                head = cells[^1];
                escapeDir = FindValidEscapeDirection(head, cells, occupied);
                if (!escapeDir.HasValue || !EnsureHeadAlignedWithDirection(cells, escapeDir.Value, occupied))
                    return null;
            }

            var availableColors = GetAvailableColors();
            return new EditorArrow
            {
                id = id,
                cells = cells,
                headDirection = escapeDir.Value,
                color = availableColors[Random.Range(0, availableColors.Count)]
            };
        }

        /// <summary>
        /// 남은 빈 셀을 긴 화살표 우선으로 채우기 (Solvable 검증 포함)
        /// </summary>
        private void FillRemainingCellsWithValidation(bool[,] occupied, ref int arrowId)
        {
            // 1단계: 빈 셀 그룹화
            var groups = GroupEmptyCells(occupied);

            foreach (var group in groups)
            {
                // 그룹이 _minArrowLength 이상이면 긴 화살표 시도
                int attempts = 0;
                int maxAttempts = group.Count * 2;

                while (attempts < maxAttempts)
                {
                    attempts++;

                    // 현재 그룹에서 아직 채워지지 않은 셀만 필터링
                    var remainingInGroup = group.FindAll(c => !occupied[c.x, c.y]);
                    if (remainingInGroup.Count < _minArrowLength) break;

                    var arrow = TryCreateArrowFromGroup(remainingInGroup, occupied, arrowId);
                    if (arrow == null) break;

                    // 임시 추가 및 검증
                    _arrows.Add(arrow);
                    foreach (var cell in arrow.cells)
                        occupied[cell.x, cell.y] = true;

                    var result = SimulateSolve();
                    if (result.success)
                    {
                        arrowId++;
                    }
                    else
                    {
                        // 롤백
                        _arrows.RemoveAt(_arrows.Count - 1);
                        foreach (var cell in arrow.cells)
                            occupied[cell.x, cell.y] = false;
                        break;
                    }
                }
            }

        }

        /// <summary>
        /// 1칸 화살표용 탈출 방향 찾기 (마스크 경계 포함)
        /// </summary>
        private ArrowDirection FindEscapeDirectionForOneCell(Vector2Int cell, bool[,] occupied)
        {
            // 탈출 가능한 방향들 찾기
            var validDirs = new List<ArrowDirection>();

            foreach (ArrowDirection dir in new[] { ArrowDirection.Up, ArrowDirection.Down, ArrowDirection.Left, ArrowDirection.Right })
            {
                if (CanEscapeToDirection(cell, dir, new List<Vector2Int> { cell }, occupied))
                {
                    validDirs.Add(dir);
                }
            }

            if (validDirs.Count > 0)
            {
                return validDirs[Random.Range(0, validDirs.Count)];
            }

            // 탈출 가능한 방향이 없으면 랜덤
            return (ArrowDirection)Random.Range(0, 4);
        }

        /// <summary>
        /// 탈출 가능한 방향 찾기 (1칸 화살표용)
        /// </summary>
        private ArrowDirection FindEscapeDirection(Vector2Int cell, bool[,] occupied)
        {
            // 경계에 가까운 방향 우선
            if (cell.y == 0) return ArrowDirection.Down;
            if (cell.y == _gridHeight - 1) return ArrowDirection.Up;
            if (cell.x == 0) return ArrowDirection.Left;
            if (cell.x == _gridWidth - 1) return ArrowDirection.Right;

            // 랜덤
            return (ArrowDirection)Random.Range(0, 4);
        }

        // ========== StageData 저장/불러오기 ==========

        private const string STAGES_PATH = "Assets/ArrowPopBall/Resources/Stages";

        private string GetStageAssetPath(int stageId)
        {
            return Path.Combine(STAGES_PATH, $"Stage_{stageId:D3}.asset");
        }

        private void SaveToStageAsset()
        {
            if (_arrows.Count == 0)
            {
                _statusMessage = "No arrows to save!";
                return;
            }

            // 폴더 생성
            if (!Directory.Exists(STAGES_PATH))
            {
                Directory.CreateDirectory(STAGES_PATH);
                AssetDatabase.Refresh();
            }

            string assetPath = GetStageAssetPath(_levelId);

            // 기존 에셋 확인 또는 새로 생성
            StageData stageData = AssetDatabase.LoadAssetAtPath<StageData>(assetPath);
            bool isNewAsset = stageData == null;

            if (isNewAsset)
            {
                stageData = ScriptableObject.CreateInstance<StageData>();
            }

            // 데이터 설정
            stageData.stageId = $"Stage_{_levelId:D3}";
            stageData.description = $"Grid: {_gridWidth}x{_gridHeight}, Arrows: {_arrows.Count}";
            stageData.gridWidth = _gridWidth;
            stageData.gridHeight = _gridHeight;

            // 화살표 변환
            stageData.arrows = ConvertToStageArrowDataList();

            // 정답 순서 저장 (ReverseGrowthGenerator에서 생성된 경우)
            if (_lastSolutionOrder != null && _lastSolutionOrder.Count > 0)
            {
                stageData.solutionOrder = new List<int>(_lastSolutionOrder);
            }
            else
            {
                // 기본 순서 (ID 순)
                stageData.solutionOrder = new List<int>();
                foreach (var arrow in _arrows)
                {
                    stageData.solutionOrder.Add(arrow.id);
                }
            }

            // 에셋 저장
            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(stageData, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(stageData);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _statusMessage = $"Saved StageData to {assetPath}";
            Debug.Log($"[LevelEditor] {_statusMessage}");

            // Stages 패널 자동 새로고침
            RefreshStageAssetList();
        }

        private void LoadFromStageAsset()
        {
            string assetPath = GetStageAssetPath(_levelId);
            StageData stageData = AssetDatabase.LoadAssetAtPath<StageData>(assetPath);

            if (stageData == null)
            {
                _statusMessage = $"Stage asset not found: {assetPath}";
                return;
            }

            // 그리드 설정 적용
            _gridWidth = stageData.gridWidth;
            _gridHeight = stageData.gridHeight;

            // 화살표 변환
            _arrows.Clear();
            if (stageData.arrows != null)
            {
                foreach (var stageArrow in stageData.arrows)
                {
                    var editorArrow = ConvertFromStageArrowData(stageArrow);
                    if (editorArrow != null)
                        _arrows.Add(editorArrow);
                }
            }

            _selectedArrowIndex = -1;
            ValidateSolvable();
            _statusMessage = $"Loaded {_arrows.Count} arrows from {assetPath}";
            Debug.Log($"[LevelEditor] {_statusMessage}");
            Repaint();
        }

        /// <summary>
        /// EditorArrow 리스트를 StageArrowData 리스트로 변환
        /// </summary>
        private List<StageArrowData> ConvertToStageArrowDataList()
        {
            var result = new List<StageArrowData>();

            foreach (var arrow in _arrows)
            {
                if (arrow.cells.Count == 0) continue;

                var stageArrow = new StageArrowData
                {
                    id = arrow.id,
                    x = arrow.cells[0].x,
                    y = arrow.cells[0].y,
                    color = arrow.color,
                    segments = ConvertToStageSegments(arrow.cells, arrow.headDirection)
                };

                result.Add(stageArrow);
            }

            return result;
        }

        /// <summary>
        /// 셀 리스트에서 StageSegmentData 리스트로 변환
        /// </summary>
        private List<StageSegmentData> ConvertToStageSegments(List<Vector2Int> cells, ArrowDirection headDir)
        {
            var segments = new List<StageSegmentData>();
            if (cells.Count < 2)
            {
                // 1칸짜리 화살표
                segments.Add(new StageSegmentData { direction = headDir, length = 0 });
                return segments;
            }

            ArrowDirection? currentDir = null;
            int currentLength = 0;

            for (int i = 1; i < cells.Count; i++)
            {
                ArrowDirection dir = GridUtility.GetDirectionFromTo(cells[i - 1], cells[i]);

                if (currentDir == null || currentDir == dir)
                {
                    currentDir = dir;
                    currentLength++;
                }
                else
                {
                    // 방향 전환 - 이전 세그먼트 저장
                    segments.Add(new StageSegmentData { direction = currentDir.Value, length = currentLength });
                    currentDir = dir;
                    currentLength = 1;
                }
            }

            // 마지막 세그먼트 저장
            if (currentDir != null)
            {
                segments.Add(new StageSegmentData { direction = currentDir.Value, length = currentLength });
            }

            return segments;
        }

        /// <summary>
        /// StageArrowData를 EditorArrow로 변환
        /// </summary>
        private EditorArrow ConvertFromStageArrowData(StageArrowData stageArrow)
        {
            var cells = new List<Vector2Int>();
            Vector2Int current = new Vector2Int(stageArrow.x, stageArrow.y);
            cells.Add(current);

            if (stageArrow.segments != null)
            {
                foreach (var segment in stageArrow.segments)
                {
                    Vector2Int dir = GetDirectionVector(segment.direction);
                    for (int i = 0; i < segment.length; i++)
                    {
                        current += dir;
                        cells.Add(current);
                    }
                }
            }

            return new EditorArrow
            {
                id = stageArrow.id,
                cells = cells,
                headDirection = stageArrow.HeadDirection,
                color = stageArrow.color
            };
        }

        // ========== JSON 저장/불러오기 (Legacy) ==========

        private string GetLevelFilePath(int levelId)
        {
            return Path.Combine(LEVELS_PATH, $"Level_{levelId:D3}.json");
        }

        private void SaveToJson()
        {
            if (_arrows.Count == 0)
            {
                _statusMessage = "No arrows to save!";
                return;
            }

            // 폴더 생성
            if (!Directory.Exists(LEVELS_PATH))
            {
                Directory.CreateDirectory(LEVELS_PATH);
            }

            // LevelData 생성
            LevelData levelData = new LevelData
            {
                levelId = _levelId,
                gridWidth = _gridWidth,
                gridHeight = _gridHeight,
                arrows = ConvertToArrowDataArray(),
                balloons = GenerateBalloonData(),
                parMoves = _arrows.Count,
                starThresholds1 = _arrows.Count + 2,
                starThresholds2 = _arrows.Count + 1,
                starThresholds3 = _arrows.Count
            };

            // JSON 저장
            string json = JsonUtility.ToJson(levelData, true);
            string filePath = GetLevelFilePath(_levelId);
            File.WriteAllText(filePath, json);

            AssetDatabase.Refresh();
            _statusMessage = $"Saved to {filePath}";
        }

        private void LoadFromJson()
        {
            string filePath = GetLevelFilePath(_levelId);

            if (!File.Exists(filePath))
            {
                _statusMessage = $"File not found: {filePath}";
                return;
            }

            string json = File.ReadAllText(filePath);
            LevelData levelData = JsonUtility.FromJson<LevelData>(json);

            if (levelData == null)
            {
                _statusMessage = "Failed to parse JSON!";
                return;
            }

            // 그리드 설정 적용
            _gridWidth = levelData.gridWidth;
            _gridHeight = levelData.gridHeight;

            // 화살표 변환
            _arrows.Clear();
            if (levelData.arrows != null)
            {
                foreach (var arrowData in levelData.arrows)
                {
                    var editorArrow = ConvertFromArrowData(arrowData);
                    if (editorArrow != null)
                        _arrows.Add(editorArrow);
                }
            }

            _selectedArrowIndex = -1;
            _statusMessage = $"Loaded {_arrows.Count} arrows from {filePath}";
            Repaint();
        }

        /// <summary>
        /// EditorArrow 리스트를 ArrowData 배열로 변환
        /// </summary>
        private ArrowData[] ConvertToArrowDataArray()
        {
            var arrowDataList = new List<ArrowData>();

            foreach (var arrow in _arrows)
            {
                if (arrow.cells.Count == 0) continue;

                var arrowData = new ArrowData
                {
                    id = arrow.id,
                    x = arrow.cells[0].x,
                    y = arrow.cells[0].y,
                    color = arrow.color,
                    segments = CalculateSegments(arrow.cells, arrow.headDirection)
                };

                arrowDataList.Add(arrowData);
            }

            return arrowDataList.ToArray();
        }

        /// <summary>
        /// ArrowData를 EditorArrow로 변환
        /// </summary>
        private EditorArrow ConvertFromArrowData(ArrowData arrowData)
        {
            var cells = new List<Vector2Int>();
            Vector2Int current = new Vector2Int(arrowData.x, arrowData.y);
            cells.Add(current);

            if (arrowData.segments != null)
            {
                foreach (var segment in arrowData.segments)
                {
                    Vector2Int dir = GetDirectionVector(segment.direction);
                    for (int i = 0; i < segment.length; i++)
                    {
                        current += dir;
                        cells.Add(current);
                    }
                }
            }

            return new EditorArrow
            {
                id = arrowData.id,
                cells = cells,
                headDirection = arrowData.HeadDirection,
                color = arrowData.color
            };
        }

        /// <summary>
        /// 셀 리스트에서 세그먼트 계산
        /// </summary>
        private SegmentData[] CalculateSegments(List<Vector2Int> cells, ArrowDirection headDir)
        {
            if (cells.Count <= 1)
            {
                // 1칸짜리 화살표: length=1 세그먼트로 저장 (게임은 시작점 + length만큼 이동)
                // 단, 게임에서 1칸짜리는 시작점만 점유하므로 length=0 사용
                // 대신 headDir을 direction으로 저장하여 HeadDirection 속성이 올바르게 반환되도록 함
                return new SegmentData[]
                {
                    new SegmentData { direction = headDir, length = 0 }
                };
            }

            var segments = new List<SegmentData>();
            ArrowDirection currentDir = ArrowDirection.Up;
            int currentLength = 0;

            for (int i = 1; i < cells.Count; i++)
            {
                ArrowDirection dir = GetDirectionFromTo(cells[i - 1], cells[i]);

                if (i == 1)
                {
                    currentDir = dir;
                    currentLength = 1;
                }
                else if (dir == currentDir)
                {
                    currentLength++;
                }
                else
                {
                    segments.Add(new SegmentData { direction = currentDir, length = currentLength });
                    currentDir = dir;
                    currentLength = 1;
                }
            }

            // 마지막 세그먼트 추가 (실제 이동 방향 유지)
            if (currentLength > 0)
            {
                segments.Add(new SegmentData { direction = currentDir, length = currentLength });
            }

            // headDir(탈출 방향)을 저장하기 위해 length=0 세그먼트 추가
            // - 게임 코드는 length=0 세그먼트의 이동을 무시함 (for 루프가 0번 실행)
            // - 하지만 ArrowData.HeadDirection은 마지막 세그먼트의 direction을 반환
            // - 따라서 length=0 세그먼트로 탈출 방향만 저장 가능
            if (segments.Count == 0 || segments[^1].direction != headDir)
            {
                segments.Add(new SegmentData { direction = headDir, length = 0 });
            }

            return segments.ToArray();
        }

        /// <summary>
        /// 화살표 색상에 맞는 풍선 데이터 생성
        /// </summary>
        private BalloonData[] GenerateBalloonData()
        {
            // 색상별 화살표 개수 계산
            var colorCounts = new Dictionary<GameColor, int>();
            foreach (var arrow in _arrows)
            {
                if (!colorCounts.ContainsKey(arrow.color))
                    colorCounts[arrow.color] = 0;
                colorCounts[arrow.color]++;
            }

            // 풍선 데이터 생성
            var balloons = new List<BalloonData>();
            int slotIndex = 0;

            foreach (var kvp in colorCounts)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    balloons.Add(new BalloonData
                    {
                        id = slotIndex + 1,
                        slotIndex = slotIndex,
                        color = kvp.Key
                    });
                    slotIndex++;
                }
            }

            return balloons.ToArray();
        }

        // ========== 유틸리티 ==========

        private bool HasEmptyCell(bool[,] occupied)
        {
            for (int x = 0; x < _gridWidth; x++)
                for (int y = 0; y < _gridHeight; y++)
                    if (!occupied[x, y]) return true;
            return false;
        }

        private int CountFilledCells(bool[,] occupied)
        {
            int count = 0;
            for (int x = 0; x < _gridWidth; x++)
                for (int y = 0; y < _gridHeight; y++)
                    if (occupied[x, y]) count++;
            return count;
        }

        private int CountEmptyCells(bool[,] occupied)
        {
            int count = 0;
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (occupied[x, y]) continue;
                    // 마스크 사용 시 마스크 내 셀만 카운트
                    if (_useShapeMask && _shapeMask != null && !_shapeMask[x, y]) continue;
                    count++;
                }
            }
            return count;
        }

        private bool IsValidCell(Vector2Int cell)
        {
            bool inBounds = cell.x >= 0 && cell.x < _gridWidth &&
                            cell.y >= 0 && cell.y < _gridHeight;
            if (!inBounds) return false;

            // 마스크가 활성화되어 있으면 마스크 내 셀인지 확인
            if (_useShapeMask && _shapeMask != null)
            {
                return _shapeMask[cell.x, cell.y];
            }
            return true;
        }

        private Vector2Int GetDirectionVector(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up => Vector2Int.up,
                ArrowDirection.Down => Vector2Int.down,
                ArrowDirection.Left => Vector2Int.left,
                ArrowDirection.Right => Vector2Int.right,
                _ => Vector2Int.zero
            };
        }

        /// <summary>
        /// Head에서 가장 가까운 경계 방향 찾기 (자신의 몸통에 막히지 않는 방향)
        /// </summary>
        private ArrowDirection GetNearestBoundaryDirection(Vector2Int head, List<Vector2Int> arrowCells)
        {
            // 각 방향별 경계까지의 거리 계산
            var candidates = new List<(ArrowDirection dir, int distance)>();

            // Up: 상단 경계까지
            int distUp = _gridHeight - 1 - head.y;
            if (CanEscapeInDirection(head, ArrowDirection.Up, arrowCells))
                candidates.Add((ArrowDirection.Up, distUp));

            // Down: 하단 경계까지
            int distDown = head.y;
            if (CanEscapeInDirection(head, ArrowDirection.Down, arrowCells))
                candidates.Add((ArrowDirection.Down, distDown));

            // Right: 우측 경계까지
            int distRight = _gridWidth - 1 - head.x;
            if (CanEscapeInDirection(head, ArrowDirection.Right, arrowCells))
                candidates.Add((ArrowDirection.Right, distRight));

            // Left: 좌측 경계까지
            int distLeft = head.x;
            if (CanEscapeInDirection(head, ArrowDirection.Left, arrowCells))
                candidates.Add((ArrowDirection.Left, distLeft));

            // 가장 가까운 경계 방향 선택
            if (candidates.Count > 0)
            {
                candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
                return candidates[0].dir;
            }

            // 모든 방향이 막혀있으면 기본값 (Up)
            return ArrowDirection.Up;
        }

        /// <summary>
        /// 특정 방향으로 경계까지 이동 가능한지 확인 (자신의 몸통에 막히지 않는지)
        /// </summary>
        private bool CanEscapeInDirection(Vector2Int head, ArrowDirection dir, List<Vector2Int> arrowCells)
        {
            Vector2Int dirVec = GetDirectionVector(dir);
            Vector2Int current = head + dirVec;

            while (current.x >= 0 && current.x < _gridWidth &&
                   current.y >= 0 && current.y < _gridHeight)
            {
                // 자신의 몸통에 막히면 탈출 불가
                if (arrowCells.Contains(current))
                    return false;
                current += dirVec;
            }

            return true;
        }

        private ArrowDirection GetOppositeDirection(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up => ArrowDirection.Down,
                ArrowDirection.Down => ArrowDirection.Up,
                ArrowDirection.Left => ArrowDirection.Right,
                ArrowDirection.Right => ArrowDirection.Left,
                _ => dir
            };
        }

        private ArrowDirection GetDirectionFromTo(Vector2Int from, Vector2Int to)
        {
            Vector2Int diff = to - from;
            if (diff.x > 0) return ArrowDirection.Right;
            if (diff.x < 0) return ArrowDirection.Left;
            if (diff.y > 0) return ArrowDirection.Up;
            if (diff.y < 0) return ArrowDirection.Down;
            return ArrowDirection.Up;
        }

        private List<ArrowDirection> GetShuffledDirections()
        {
            var dirs = new List<ArrowDirection>
            {
                ArrowDirection.Up, ArrowDirection.Down,
                ArrowDirection.Left, ArrowDirection.Right
            };

            // Fisher-Yates shuffle
            for (int i = dirs.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
            }

            return dirs;
        }

        private Color GetColorForGameColor(GameColor gameColor)
        {
            return gameColor switch
            {
                GameColor.Red => Color.red,
                GameColor.Blue => Color.blue,
                GameColor.Green => Color.green,
                GameColor.Yellow => Color.yellow,
                GameColor.Purple => new Color(0.5f, 0, 0.5f),
                GameColor.Orange => new Color(1f, 0.5f, 0),
                GameColor.Cyan => Color.cyan,
                GameColor.Pink => new Color(1f, 0.4f, 0.7f),
                GameColor.Brown => new Color(0.6f, 0.3f, 0.1f),
                GameColor.Lime => new Color(0.5f, 1f, 0),
                GameColor.Navy => new Color(0, 0, 0.5f),
                GameColor.Magenta => Color.magenta,
                GameColor.Black => Color.black,
                _ => Color.gray
            };
        }

        /// <summary>
        /// 현재 설정에 따라 사용 가능한 색상 목록 반환
        /// </summary>
        private List<GameColor> GetAvailableColors()
        {
            var colors = new List<GameColor>();

            if (_useAutoColors)
            {
                // Auto: 그리드 크기에 따라 적절한 수의 색상 선택
                int autoColorCount = Mathf.Clamp((_gridWidth * _gridHeight) / 15, 3, 6);
                for (int i = 0; i < autoColorCount; i++)
                {
                    colors.Add((GameColor)i);
                }
            }
            else
            {
                // Manual: 선택된 색상만 사용
                for (int i = 0; i < 13; i++)
                {
                    if (_selectedColors[i])
                    {
                        colors.Add((GameColor)i);
                    }
                }

                // 최소 1개 보장
                if (colors.Count < 1)
                {
                    colors.Clear();
                    colors.Add(GameColor.Red);
                }
            }

            return colors;
        }

        /// <summary>
        /// 현재 설정에 따라 화살표 길이 범위 반환
        /// </summary>
        private GeometricPatternType BuildGeometricPatternFlags()
        {
            var flags = GeometricPatternType.None;
            if (_patternStraight) flags |= GeometricPatternType.Straight;
            if (_patternLShape)   flags |= GeometricPatternType.LShape;
            if (_patternUShape)   flags |= GeometricPatternType.UShape;
            if (_patternZigzag)   flags |= GeometricPatternType.Zigzag;
            if (_patternSnake)    flags |= GeometricPatternType.Snake;
            if (_patternSpiral)   flags |= GeometricPatternType.Spiral;
            if (_patternOutline)  flags |= GeometricPatternType.Outline;
            return flags;
        }

        private (int min, int max) GetArrowLengthRange()
        {
            if (_useAutoLength)
            {
                return GetAutoArrowLength();
            }
            return (_minArrowLength, _maxArrowLength);
        }

        // ========== Shape/Mask 관련 함수 ==========

        /// <summary>
        /// 마스크 초기화 (모든 셀 활성화)
        /// </summary>
        private void InitializeShapeMask()
        {
            _shapeMask = new bool[_gridWidth, _gridHeight];
            FillAllMask(true);
            _useShapeMask = true;
        }

        /// <summary>
        /// 모든 마스크 셀을 채우거나 비움
        /// </summary>
        private void FillAllMask(bool value)
        {
            if (_shapeMask == null || _shapeMask.GetLength(0) != _gridWidth || _shapeMask.GetLength(1) != _gridHeight)
            {
                _shapeMask = new bool[_gridWidth, _gridHeight];
            }

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    _shapeMask[x, y] = value;
                }
            }
        }

        /// <summary>
        /// 활성화된 셀 개수 반환
        /// </summary>
        private int CountActiveCells()
        {
            if (_shapeMask == null) return _gridWidth * _gridHeight;

            int count = 0;
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    if (_shapeMask[x, y]) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// PNG 이미지에서 Shape 마스크 생성
        /// </summary>
        private void ApplyShapeFromImage()
        {
            if (_shapeImage == null) return;

            // 이미지를 읽기 가능하게 설정 확인
            string path = AssetDatabase.GetAssetPath(_shapeImage);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(path);
            }

            // 그리드 크기에 맞게 마스크 생성
            _shapeMask = new bool[_gridWidth, _gridHeight];
            _colorMap = new GameColor[_gridWidth, _gridHeight];

            int imgWidth = _shapeImage.width;
            int imgHeight = _shapeImage.height;

            for (int gx = 0; gx < _gridWidth; gx++)
            {
                for (int gy = 0; gy < _gridHeight; gy++)
                {
                    // 그리드 좌표를 이미지 좌표로 변환
                    int imgX = (int)((gx + 0.5f) / _gridWidth * imgWidth);
                    int imgY = (int)((gy + 0.5f) / _gridHeight * imgHeight);

                    imgX = Mathf.Clamp(imgX, 0, imgWidth - 1);
                    imgY = Mathf.Clamp(imgY, 0, imgHeight - 1);

                    Color pixel = _shapeImage.GetPixel(imgX, imgY);

                    // 알파값이 0.5 이상이면 활성화
                    _shapeMask[gx, gy] = pixel.a > 0.5f;

                    // Color Mapping: 픽셀 색상을 게임 색상으로 매핑
                    if (_shapeMask[gx, gy])
                    {
                        _colorMap[gx, gy] = MapPixelToGameColor(pixel);
                    }
                }
            }

            _useShapeMask = true;
            _statusMessage = $"Shape applied! {CountActiveCells()} cells active.";
        }

        /// <summary>
        /// 픽셀 색상을 가장 가까운 GameColor로 매핑
        /// </summary>
        private GameColor MapPixelToGameColor(Color pixel)
        {
            GameColor bestMatch = GameColor.Red;
            float bestDistance = float.MaxValue;

            // 각 GameColor와의 색상 거리 계산
            var colorMappings = new (GameColor gameColor, Color rgb)[]
            {
                (GameColor.Red, new Color(0.9f, 0.2f, 0.2f)),
                (GameColor.Blue, new Color(0.2f, 0.4f, 0.9f)),
                (GameColor.Green, new Color(0.2f, 0.8f, 0.3f)),
                (GameColor.Yellow, new Color(0.95f, 0.85f, 0.2f)),
                (GameColor.Purple, new Color(0.7f, 0.3f, 0.9f)),
                (GameColor.Orange, new Color(1f, 0.65f, 0f)),
                (GameColor.Cyan, new Color(0f, 0.9f, 0.9f)),
                (GameColor.Pink, new Color(1f, 0.75f, 0.8f)),
                (GameColor.Brown, new Color(0.55f, 0.27f, 0.07f)),
                (GameColor.Lime, new Color(0.2f, 0.8f, 0.2f)),
                (GameColor.Navy, new Color(0.1f, 0.1f, 0.5f)),
                (GameColor.Magenta, new Color(1f, 0f, 1f)),
            };

            foreach (var (gameColor, rgb) in colorMappings)
            {
                float distance = ColorDistance(pixel, rgb);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = gameColor;
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// 두 색상 간의 거리 (RGB 유클리드 거리)
        /// </summary>
        private float ColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }

        /// <summary>
        /// 셀이 마스크에서 활성화되어 있는지 확인
        /// </summary>
        private bool IsCellInMask(int x, int y)
        {
            if (!_useShapeMask || _shapeMask == null) return true;
            if (x < 0 || x >= _gridWidth || y < 0 || y >= _gridHeight) return false;
            return _shapeMask[x, y];
        }

        /// <summary>
        /// 셀이 마스크에서 활성화되어 있는지 확인 (Vector2Int)
        /// </summary>
        private bool IsCellInMask(Vector2Int cell)
        {
            return IsCellInMask(cell.x, cell.y);
        }
    }
}
