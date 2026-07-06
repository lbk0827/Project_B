using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// MeowDoku 스타일 로비 씬 자동 구성 빌더
    /// 메뉴: Tools > ArrowPopBall > Build Lobby UI (MeowDoku Style)
    /// - 크림색 배경 + 은은한 고양이/X 패턴 그리드
    /// - 데일리 챌린지 잠금 카드 (인디고)
    /// - 중앙 로고 텍스트
    /// - 하단 오렌지 레벨 버튼
    /// - 우상단 설정 기어 버튼
    /// </summary>
    public static class LobbyUIBuilder
    {
        // ========== 경로 ==========
        private const string ScenePath = "Assets/ArrowPopBall/Scenes/LobbyScene.unity";
        private const string FontTtfPath = "Assets/ArrowPopBall/Fonts/NotoSansKR.ttf";
        private const string FontAssetPath = "Assets/ArrowPopBall/Fonts/NotoSansKR SDF.asset";
        private const string SpriteDir = "Assets/ArrowPopBall/Sprites/UI";
        private const string RoundedRectPath = SpriteDir + "/RoundedRect.png";
        private const string BalloonPath = SpriteDir + "/Balloon.png";
        private const string LockIconPath = "Assets/ArrowPopBall/_Heathen Engineering/Assets/UX/Icons/Flat Icons [Free]/Free Flat Lock Closed Icon.png";
        private const string GearIconPath = "Assets/ArrowPopBall/_Heathen Engineering/Assets/UX/Icons/Flat Icons [Free]/Free Flat Gear 1 Icon.png";

        // ========== 디자인 상수 ==========
        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 2340f);

        private static readonly Color BgCream = Hex("F7EEE3");
        private static readonly Color PatternCellColor = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color PatternIconColor = HexA("E9D6BD", 0.7f);
        private static readonly Color CardIndigo = Hex("6F74C6");
        private static readonly Color CardTitleColor = Hex("5056AC");
        private static readonly Color LogoBrown = Hex("7B563E");
        private static readonly Color ButtonOrange = Hex("F79E1B");
        private static readonly Color ButtonShadowColor = HexA("DD8A05", 0.9f);
        private static readonly Color GearBrown = Hex("8A5F49");

        // 로고 텍스트 (리치 텍스트로 O 색상 강조)
        private const string LogoRichText = "ME<color=#94A6EE>O</color>W\nD<color=#F5A31D>O</color>KU";

        [MenuItem("Tools/ArrowPopBall/Build Lobby UI (MeowDoku Style)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            TMP_FontAsset koreanFont = EnsureKoreanFontAsset();
            GenerateSprites();

            Sprite rounded = LoadSprite(RoundedRectPath);
            Sprite balloon = LoadSprite(BalloonPath);
            Sprite lockIcon = LoadSprite(LockIconPath);
            Sprite gearIcon = LoadSprite(GearIconPath);

            // ===== Canvas 준비 =====
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 기존 캔버스 자식 전부 제거 후 재구성
            ClearChildren(canvas.transform);

            // 예전 배경 오브젝트 제거
            var oldBg = GameObject.Find("LobbyBackground");
            if (oldBg != null)
                Object.DestroyImmediate(oldBg);

            // 카메라 배경색도 크림색으로
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = BgCream;
            }

            // ===== 배경 =====
            Image bg = CreateImage("BG", canvas.transform, null, BgCream);
            Stretch(bg.rectTransform);

            BuildBackgroundPattern(canvas.transform, rounded, balloon);

            // ===== Home 패널 =====
            var homePanel = CreateRect("HomePanel", canvas.transform);
            Stretch(homePanel);

            BuildDailyChallengeCard(homePanel, rounded, lockIcon, koreanFont);

            // 로고
            var logo = CreateText("TXT_Logo", homePanel, koreanFont, LogoRichText, 170f, LogoBrown);
            logo.fontStyle = FontStyles.Bold;
            logo.lineSpacing = -40f;
            logo.characterSpacing = 4f;
            logo.rectTransform.sizeDelta = new Vector2(900f, 500f);
            logo.rectTransform.anchoredPosition = new Vector2(0f, 130f);

            // ===== 플레이 버튼 (오렌지 알약형) =====
            var (playButton, levelText) = BuildPlayButton(homePanel, rounded, koreanFont);

            // ===== 설정 기어 버튼 (우상단) =====
            Button settingsButton = BuildSettingsButton(canvas.transform, rounded, gearIcon);

            // ===== LobbyUI 연결 =====
            WireLobbyUI(homePanel.gameObject, levelText, playButton, settingsButton);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[LobbyUIBuilder] MeowDoku 스타일 로비 UI 구성 완료");
        }

        // ========== 배경 패턴 ==========
        private static void BuildBackgroundPattern(Transform parent, Sprite rounded, Sprite balloon)
        {
            var layer = CreateRect("PatternLayer", parent);
            Stretch(layer);

            const float pitch = 216f;
            const float cellSize = 200f;
            const int cols = 3;   // 중심 기준 좌우 3칸 (총 7열, 화면 밖 여유 포함)
            const int rows = 6;   // 중심 기준 상하 6칸

            for (int iy = -rows; iy <= rows; iy++)
            {
                for (int ix = -cols; ix <= cols; ix++)
                {
                    Image cell = CreateImage($"Cell_{ix}_{iy}", layer, rounded, PatternCellColor);
                    cell.type = Image.Type.Sliced;
                    cell.pixelsPerUnitMultiplier = 64f / 40f;   // 모서리 반경 40
                    cell.rectTransform.sizeDelta = new Vector2(cellSize, cellSize);
                    cell.rectTransform.anchoredPosition = new Vector2(ix * pitch, iy * pitch);

                    // 결정적 의사 난수로 풍선 아이콘 배치 (크기/기울기 두 가지 변형)
                    int hash = Mod(ix * 7 + iy * 13, 9);
                    if (hash == 2 || hash == 6)
                    {
                        Image deco = CreateImage("Deco", cell.transform, balloon, PatternIconColor);
                        if (hash == 2)
                        {
                            deco.rectTransform.sizeDelta = new Vector2(115f, 115f);
                        }
                        else
                        {
                            deco.rectTransform.sizeDelta = new Vector2(90f, 90f);
                            deco.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
                        }
                    }
                }
            }
        }

        // ========== 데일리 챌린지 카드 ==========
        private static void BuildDailyChallengeCard(RectTransform parent, Sprite rounded, Sprite lockIcon, TMP_FontAsset font)
        {
            Image card = CreateImage("DailyChallengeCard", parent, rounded, CardIndigo);
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = 64f / 44f;
            var rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(320f, 420f);
            rt.anchoredPosition = new Vector2(0f, -330f);

            var title = CreateText("TXT_Title", rt, font, "데일리\n챌린지", 38f, CardTitleColor);
            title.alignment = TextAlignmentOptions.TopLeft;
            title.fontStyle = FontStyles.Bold;
            title.lineSpacing = -10f;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-56f, 120f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -28f);

            Image lockImg = CreateImage("IMG_Lock", rt, lockIcon, Color.white);
            lockImg.rectTransform.sizeDelta = new Vector2(96f, 96f);
            lockImg.rectTransform.anchoredPosition = new Vector2(0f, -20f);

            var unlockText = CreateText("TXT_Unlock", rt, font, "<b>Lv.21</b>에서 잠금 해제", 30f, Color.white);
            unlockText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            unlockText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            unlockText.rectTransform.pivot = new Vector2(0.5f, 0f);
            unlockText.rectTransform.sizeDelta = new Vector2(300f, 50f);
            unlockText.rectTransform.anchoredPosition = new Vector2(0f, 55f);
        }

        // ========== 플레이 버튼 ==========
        private static (Button, TextMeshProUGUI) BuildPlayButton(RectTransform parent, Sprite rounded, TMP_FontAsset font)
        {
            var root = CreateRect("BTN_Play", parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(580f, 130f);
            root.anchoredPosition = new Vector2(0f, 950f);

            Image shadow = CreateImage("IMG_Shadow", root, rounded, ButtonShadowColor);
            shadow.type = Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = 64f / 65f;
            Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -12f);

            Image bg = CreateImage("IMG_Bg", root, rounded, ButtonOrange);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 64f / 65f;   // 높이 절반 = 알약형
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);

            var text = CreateText("TXT_CurrentLevel", root, font, "레벨 14", 62f, Color.white);
            text.fontStyle = FontStyles.Bold;
            Stretch(text.rectTransform);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            return (button, text);
        }

        // ========== 설정 버튼 ==========
        private static Button BuildSettingsButton(Transform parent, Sprite rounded, Sprite gearIcon)
        {
            var root = CreateRect("BTN_Settings", parent);
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(110f, 110f);
            root.anchoredPosition = new Vector2(-105f, -190f);

            Image bg = CreateImage("IMG_Bg", root, rounded, Color.white);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 64f / 55f;   // 원형
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);

            Image gear = CreateImage("IMG_Gear", root, gearIcon, GearBrown);
            gear.rectTransform.sizeDelta = new Vector2(58f, 58f);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            return button;
        }

        // ========== LobbyUI 필드 연결 ==========
        private static void WireLobbyUI(GameObject homePanel, TextMeshProUGUI levelText, Button playButton, Button settingsButton)
        {
            LobbyUI lobbyUI = Object.FindObjectOfType<LobbyUI>();
            if (lobbyUI == null)
            {
                var manager = GameObject.Find("LobbyManager") ?? new GameObject("LobbyManager");
                lobbyUI = manager.AddComponent<LobbyUI>();
            }

            var so = new SerializedObject(lobbyUI);
            so.FindProperty("_homePanel").objectReferenceValue = homePanel;
            so.FindProperty("_levelText").objectReferenceValue = levelText;
            so.FindProperty("_playButton").objectReferenceValue = playButton;
            so.FindProperty("_settingsTabButton").objectReferenceValue = settingsButton;
            so.FindProperty("_challengeTabButton").objectReferenceValue = null;
            so.FindProperty("_homeTabButton").objectReferenceValue = null;
            so.FindProperty("_tabHighlight").objectReferenceValue = null;
            so.FindProperty("_resetButton").objectReferenceValue = null;
            so.FindProperty("_challengePanel").objectReferenceValue = null;
            so.FindProperty("_settingsPanel").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ========== 한글 폰트 에셋 ==========
        private static TMP_FontAsset EnsureKoreanFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[LobbyUIBuilder] 폰트 파일 없음: {FontTtfPath} — 기본 폰트로 대체 (한글 미표시)");
                return TMP_Settings.defaultFontAsset;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            fontAsset.material.name = fontAsset.name + " Material";
            fontAsset.atlasTexture.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);

            Debug.Log($"[LobbyUIBuilder] 한글 TMP 폰트 에셋 생성: {FontAssetPath}");
            return fontAsset;
        }

        // ========== 프로시저럴 스프라이트 생성 ==========
        private static void GenerateSprites()
        {
            if (!Directory.Exists(SpriteDir))
                Directory.CreateDirectory(SpriteDir);

            // 둥근 사각형 (9-slice, 반경 64)
            CreateSpriteAsset(RoundedRectPath, 256, 256, (x, y) =>
            {
                float dx = Mathf.Max(Mathf.Abs(x - 128f) - 64f, 0f);
                float dy = Mathf.Max(Mathf.Abs(y - 128f) - 64f, 0f);
                return dx * dx + dy * dy <= 64f * 64f;
            }, new Vector4(64f, 64f, 64f, 64f));

            // 풍선 실루엣 (타원 몸통 + 매듭 삼각형 + 꼬리 줄)
            CreateSpriteAsset(BalloonPath, 256, 256, (x, y) =>
            {
                float ex = (x - 128f) / 74f;
                float ey = (y - 152f) / 88f;
                if (ex * ex + ey * ey <= 1f)
                    return true;
                if (InTriangle(x, y, 128f, 72f, 106f, 44f, 150f, 44f))
                    return true;
                // 살짝 휘어진 꼬리 줄
                if (y >= 8f && y < 44f)
                {
                    float t = (44f - y) / 36f;
                    float cx = 128f + Mathf.Sin(t * 3.1415f) * 14f;
                    return Mathf.Abs(x - cx) < 5f;
                }
                return false;
            }, Vector4.zero);
        }

        private static void CreateSpriteAsset(string path, int width, int height, System.Func<float, float, bool> inside, Vector4 border)
        {
            if (File.Exists(path))
                return;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            const int ss = 4;   // 슈퍼샘플링 (안티앨리어싱)

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                            if (inside(px + (sx + 0.5f) / ss, py + (sy + 0.5f) / ss))
                                hits++;

                    byte alpha = (byte)(hits * 255 / (ss * ss));
                    pixels[py * width + px] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        // ========== 헬퍼 ==========
        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                // 스프라이트로 임포트되어 있지 않으면 설정 변경 후 재시도
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
            if (sprite == null)
                Debug.LogWarning($"[LobbyUIBuilder] 스프라이트 로드 실패: {path}");
            return sprite;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
        {
            var rt = CreateRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            var children = new List<GameObject>();
            foreach (Transform child in parent)
                children.Add(child.gameObject);
            foreach (var child in children)
                Object.DestroyImmediate(child);
        }

        private static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Sign(px, py, ax, ay, bx, by);
            float d2 = Sign(px, py, bx, by, cx, cy);
            float d3 = Sign(px, py, cx, cy, ax, ay);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Sign(float px, float py, float ax, float ay, float bx, float by)
        {
            return (px - bx) * (ay - by) - (ax - bx) * (py - by);
        }

        private static int Mod(int value, int mod)
        {
            return ((value % mod) + mod) % mod;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        private static Color HexA(string hex, float alpha)
        {
            var color = Hex(hex);
            color.a = alpha;
            return color;
        }
    }
}
