using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Grid;

namespace Game.Arrow
{
    /// <summary>
    /// 화살표 시각적 렌더링 담당 (LineRenderer, Head 스프라이트, 색상)
    /// ArrowController에서 분리됨
    /// </summary>
    public class ArrowVisualRenderer : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("필수 참조")]
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private SpriteRenderer _headRenderer;

        [Header("외형 설정")]
        [SerializeField, Range(0.1f, 1f)] private float _lineWidth = 0.3f;
        [SerializeField, Range(0, 20)] private int _numCapVertices = 10;
        [SerializeField, Range(0, 10)] private int _numCornerVertices = 5;
        [SerializeField, Range(0.1f, 0.5f)] private float _headOffset = 0.35f;
        [SerializeField, Range(0.1f, 0.5f)] private float _tailOffset = 0.35f;
        [SerializeField, Range(0.1f, 2f)] private float _headScale = 1f;

        // ========== 프로퍼티 ==========
        public LineRenderer LineRenderer => _lineRenderer;
        public SpriteRenderer HeadRenderer => _headRenderer;
        public float LineWidth => _lineWidth;
        public float HeadOffset => _headOffset;
        public float TailOffset => _tailOffset;

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 초기 시각 설정
        /// </summary>
        public void Initialize(GameColor color, ArrowDirection headDirection)
        {
            Color unityColor = GetUnityColor(color);
            SetupLineRenderer(unityColor);
            SetupHeadRenderer(unityColor, headDirection);
        }

        /// <summary>
        /// LineRenderer 업데이트 (월드 좌표 기반)
        /// </summary>
        public void UpdateLineRenderer(List<Vector2> cellWorldPositions, Vector2Int moveDirection)
        {
            if (_lineRenderer == null || cellWorldPositions == null || cellWorldPositions.Count == 0)
                return;

            // Transform 위치를 첫 번째 셀로 설정
            transform.position = cellWorldPositions[0];

            // 오프셋 계산
            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float headOffsetAmount = cellSize * _headOffset;
            float tailOffsetAmount = cellSize * _tailOffset;

            // Head 방향
            Vector2 headOffsetVec = (Vector2)moveDirection * headOffsetAmount;

            // Tail 방향 계산
            Vector2 tailOffsetVec = CalculateTailOffset(cellWorldPositions, headOffsetVec, tailOffsetAmount);

            // LineRenderer 포인트 설정
            SetLineRendererPositions(cellWorldPositions, headOffsetVec, tailOffsetVec);

            // Head 스프라이트 위치 업데이트
            UpdateHeadPosition(cellWorldPositions, headOffsetVec);
        }

        /// <summary>
        /// 화살표 색상 설정
        /// </summary>
        public void SetColor(Color color)
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = color;
                _lineRenderer.endColor = color;
            }
            if (_headRenderer != null)
            {
                _headRenderer.color = color;
            }
        }

        /// <summary>
        /// 색상 반환
        /// </summary>
        public void SetColor(GameColor gameColor)
        {
            SetColor(GetUnityColor(gameColor));
        }

        /// <summary>
        /// Head 회전 업데이트
        /// </summary>
        public void UpdateHeadRotation(ArrowDirection direction)
        {
            if (_headRenderer == null)
                return;

            float rotation = direction switch
            {
                ArrowDirection.Up => 0f,
                ArrowDirection.Down => 180f,
                ArrowDirection.Left => 90f,
                ArrowDirection.Right => -90f,
                _ => 0f
            };
            _headRenderer.transform.rotation = Quaternion.Euler(0, 0, rotation);
        }

        /// <summary>
        /// LineRenderer 숨기기
        /// </summary>
        public void HideLineRenderer()
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
            }
        }

        /// <summary>
        /// Head 스프라이트 알파 설정
        /// </summary>
        public void SetHeadAlpha(float alpha)
        {
            if (_headRenderer != null)
            {
                var color = _headRenderer.color;
                color.a = alpha;
                _headRenderer.color = color;
            }
        }

        /// <summary>
        /// GameColor → Unity Color 변환
        /// </summary>
        public static Color GetUnityColor(GameColor gameColor)
        {
            return gameColor switch
            {
                GameColor.Red => new Color(0.9f, 0.2f, 0.2f),
                GameColor.Blue => new Color(0.2f, 0.4f, 0.9f),
                GameColor.Green => new Color(0.2f, 0.8f, 0.3f),
                GameColor.Yellow => new Color(0.95f, 0.85f, 0.2f),
                GameColor.Purple => new Color(0.7f, 0.3f, 0.9f),
                GameColor.Orange => new Color(1f, 0.65f, 0f),
                GameColor.Cyan => new Color(0f, 0.9f, 0.9f),
                GameColor.Pink => new Color(1f, 0.75f, 0.8f),
                GameColor.Brown => new Color(0.55f, 0.27f, 0.07f),
                GameColor.Lime => new Color(0.2f, 0.8f, 0.2f),
                GameColor.Navy => new Color(0.1f, 0.1f, 0.5f),
                GameColor.Magenta => new Color(1f, 0f, 1f),
                _ => Color.white
            };
        }

        // ========== 내부 유틸리티 ==========
        private void SetupLineRenderer(Color color)
        {
            if (_lineRenderer == null)
                return;

            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
            _lineRenderer.numCapVertices = _numCapVertices;
            _lineRenderer.numCornerVertices = _numCornerVertices;
            _lineRenderer.textureMode = LineTextureMode.Tile;
        }

        private void SetupHeadRenderer(Color color, ArrowDirection direction)
        {
            if (_headRenderer == null)
                return;

            _headRenderer.color = color;
            _headRenderer.transform.localScale = Vector3.one * _headScale;
            UpdateHeadRotation(direction);
        }

        private Vector2 CalculateTailOffset(List<Vector2> positions, Vector2 headOffset, float offsetAmount)
        {
            if (positions.Count >= 2)
            {
                Vector2 tailDiff = positions[1] - positions[0];
                float tailDist = tailDiff.magnitude;

                if (tailDist > 0.01f)
                {
                    Vector2 tailToSecond = tailDiff / tailDist;
                    return -tailToSecond * offsetAmount;
                }
            }

            return -headOffset;
        }

        private void SetLineRendererPositions(List<Vector2> positions, Vector2 headOffset, Vector2 tailOffset)
        {
            _lineRenderer.positionCount = positions.Count + 2;

            // Tail 돌출점
            _lineRenderer.SetPosition(0, (Vector3)tailOffset);

            // 셀 포인트들 (로컬 좌표)
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 localPos = positions[i] - positions[0];
                _lineRenderer.SetPosition(i + 1, localPos);
            }

            // Head 돌출점
            Vector3 lastCellLocal = positions[positions.Count - 1] - positions[0];
            _lineRenderer.SetPosition(positions.Count + 1, lastCellLocal + (Vector3)headOffset);

            // 두께 유지
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
        }

        private void UpdateHeadPosition(List<Vector2> positions, Vector2 headOffset)
        {
            if (_headRenderer == null || positions.Count == 0)
                return;

            Vector3 lastCellLocal = positions[positions.Count - 1] - positions[0];
            _headRenderer.transform.localPosition = lastCellLocal + (Vector3)headOffset;
        }
    }
}