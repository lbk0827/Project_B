using UnityEngine;
using UnityEditor;
using Game.Data;

namespace Game.Editor.LevelEditor
{
    /// <summary>
    /// Shape Mask 및 이미지 처리 관리 클래스
    /// Level Editor 리팩토링 - Phase 1
    /// </summary>
    public class ShapeMaskManager
    {
        // ========== 데이터 ==========
        private bool[,] _shapeMask;
        private GameColor[,] _colorMap;
        private int _gridWidth;
        private int _gridHeight;

        // ========== 프로퍼티 ==========
        public bool[,] ShapeMask => _shapeMask;
        public GameColor[,] ColorMap => _colorMap;
        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;

        // ========== 초기화 ==========

        /// <summary>
        /// 그리드 크기 설정
        /// </summary>
        public void SetGridSize(int width, int height)
        {
            _gridWidth = width;
            _gridHeight = height;
        }

        /// <summary>
        /// 마스크 초기화 (모든 셀 활성화)
        /// </summary>
        public void Initialize()
        {
            _shapeMask = new bool[_gridWidth, _gridHeight];
            FillAll(true);
        }

        /// <summary>
        /// 모든 마스크 셀을 채우거나 비움
        /// </summary>
        public void FillAll(bool value)
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
        /// 마스크 배열 직접 설정
        /// </summary>
        public void SetMask(bool[,] mask)
        {
            _shapeMask = mask;
            if (mask != null)
            {
                _gridWidth = mask.GetLength(0);
                _gridHeight = mask.GetLength(1);
            }
        }

        /// <summary>
        /// 색상맵 직접 설정
        /// </summary>
        public void SetColorMap(GameColor[,] colorMap)
        {
            _colorMap = colorMap;
        }

        // ========== 셀 쿼리 ==========

        /// <summary>
        /// 활성화된 셀 개수 반환
        /// </summary>
        public int CountActiveCells()
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
        /// 셀이 마스크에서 활성화되어 있는지 확인
        /// </summary>
        public bool IsCellInMask(int x, int y)
        {
            if (_shapeMask == null) return true;
            if (x < 0 || x >= _gridWidth || y < 0 || y >= _gridHeight) return false;
            return _shapeMask[x, y];
        }

        /// <summary>
        /// 셀이 마스크에서 활성화되어 있는지 확인 (Vector2Int)
        /// </summary>
        public bool IsCellInMask(Vector2Int cell)
        {
            return IsCellInMask(cell.x, cell.y);
        }

        // ========== 이미지 처리 ==========

        /// <summary>
        /// PNG 이미지에서 Shape 마스크 및 색상맵 생성
        /// </summary>
        /// <returns>활성화된 셀 개수</returns>
        public int ApplyShapeFromImage(Texture2D shapeImage)
        {
            if (shapeImage == null) return 0;

            // 이미지를 읽기 가능하게 설정 확인
            string path = AssetDatabase.GetAssetPath(shapeImage);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(path);
            }

            // 그리드 크기에 맞게 마스크 생성
            _shapeMask = new bool[_gridWidth, _gridHeight];
            _colorMap = new GameColor[_gridWidth, _gridHeight];

            int imgWidth = shapeImage.width;
            int imgHeight = shapeImage.height;

            for (int gx = 0; gx < _gridWidth; gx++)
            {
                for (int gy = 0; gy < _gridHeight; gy++)
                {
                    // 그리드 좌표를 이미지 좌표로 변환
                    int imgX = (int)((gx + 0.5f) / _gridWidth * imgWidth);
                    int imgY = (int)((gy + 0.5f) / _gridHeight * imgHeight);

                    imgX = Mathf.Clamp(imgX, 0, imgWidth - 1);
                    imgY = Mathf.Clamp(imgY, 0, imgHeight - 1);

                    Color pixel = shapeImage.GetPixel(imgX, imgY);

                    // 알파값이 0.5 이상이면 활성화
                    _shapeMask[gx, gy] = pixel.a > 0.5f;

                    // Color Mapping: 픽셀 색상을 게임 색상으로 매핑
                    if (_shapeMask[gx, gy])
                    {
                        _colorMap[gx, gy] = MapPixelToGameColor(pixel);
                    }
                }
            }

            return CountActiveCells();
        }

        /// <summary>
        /// 픽셀 색상을 가장 가까운 GameColor로 매핑
        /// </summary>
        public static GameColor MapPixelToGameColor(Color pixel)
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
        public static float ColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }
    }
}
