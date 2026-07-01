# PRD: 가이드 라인 시스템 (Guide Line System)

## 1. 개요

### 1.1 기능 요약
인게임에서 화살표의 이동 방향을 시각적으로 표시하는 보조 시스템. 플레이어가 화살표가 어느 방향으로 이동할지 미리 파악할 수 있도록 도움을 주는 선택적 힌트 기능.

### 1.2 목적
- 초보 플레이어의 학습 곡선 완화
- 복잡한 퍼즐에서의 의사결정 보조
- 플레이어 편의성 향상

### 1.3 대상 사용자
- 게임을 처음 접하는 플레이어
- 복잡한 퍼즐에서 도움이 필요한 플레이어

---

## 2. UI 명세

### 2.1 토글 버튼

| 항목 | 명세 |
|------|------|
| **위치** | 퍼즐 영역 우측 하단 코너 |
| **크기** | 48x48dp (터치 영역: 56x56dp) |
| **아이콘** | 눈 아이콘 (Eye Icon) |
| **상태 표시** | On: 눈 뜬 아이콘 / Off: 눈 감은 아이콘 또는 사선 표시 |
| **초기 상태** | Off (가이드라인 숨김) |
| **Z-Order** | 퍼즐 요소보다 위, 다른 UI보다 아래 |

### 2.2 버튼 비주얼

```
[Off 상태]          [On 상태]
  ___                 ___
 /   \               /   \
|  X  |      →      |  O  |
 \___/               \___/
(눈 감김)           (눈 뜸)
```

### 2.3 버튼 상호작용
- **탭**: On/Off 토글
- **롱프레스**: 없음 (향후 툴팁 표시 고려)
- **터치 피드백**: 스케일 펀치 애니메이션 (0.9 → 1.0)

---

## 3. 가이드 라인 명세

### 3.1 시각적 속성

| 항목 | 명세 |
|------|------|
| **색상** | 회색 (#888888, Alpha: 0.6) |
| **두께** | 2px (가는 선) |
| **스타일** | 실선 (Solid) |
| **길이** | 화살표 Head에서 화면 경계까지 무한 연장 |
| **시작점** | 화살표 Head의 정중앙 |
| **방향** | 화살표 Head가 향하는 방향 (HeadDirection) |

### 3.2 렌더링 우선순위
```
[레이어 순서 - 아래에서 위로]
1. 그리드 배경 (Dots)
2. 가이드 라인 ← 화살표 뒤에 렌더링
3. 화살표 Body (LineRenderer)
4. 화살표 Head (Sprite)
5. UI 요소
```

### 3.3 가이드 라인 연산
```
시작점: Arrow.HeadPosition (월드 좌표)
방향: Arrow.HeadDirection → Vector2 변환
끝점: 화면 경계와의 교차점 계산

// 방향 벡터 예시
Up:    (0, 1)
Down:  (0, -1)
Left:  (-1, 0)
Right: (1, 0)
```

---

## 4. 동작 명세

### 4.1 상태 전이 다이어그램

```
                    ┌──────────────────┐
                    │    레벨 시작     │
                    └────────┬─────────┘
                             │
                             ▼
                    ┌──────────────────┐
                    │  Off 상태 (기본)  │◄──────────────┐
                    └────────┬─────────┘               │
                             │                         │
                       [버튼 탭]                   [버튼 탭]
                             │                         │
                             ▼                         │
                    ┌──────────────────┐               │
                    │    On 상태       │───────────────┘
                    └────────┬─────────┘
                             │
              ┌──────────────┼──────────────┐
              │              │              │
        [화살표 탭]    [충돌 복귀]    [탈출 완료]
              │              │              │
              ▼              ▼              ▼
    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    │ 해당 라인만  │  │ 해당 라인   │  │ 해당 라인   │
    │ 즉시 숨김   │  │ 다시 표시   │  │ 제거       │
    └─────────────┘  └─────────────┘  └─────────────┘
```

### 4.2 이벤트별 동작

| 이벤트 | 가이드라인 동작 |
|--------|----------------|
| **레벨 시작** | 모든 가이드라인 숨김 (Off 기본) |
| **버튼 On** | 모든 화살표의 가이드라인 페이드 인 (0.2초) |
| **버튼 Off** | 모든 가이드라인 페이드 아웃 (0.2초) |
| **화살표 탭 (이동 시작)** | 해당 화살표의 가이드라인 즉시 숨김 |
| **화살표 충돌 복귀** | 해당 화살표의 가이드라인 페이드 인 (0.15초) |
| **화살표 탈출 완료** | 해당 화살표의 가이드라인 제거 (오브젝트 파괴) |
| **레벨 재시작** | 모든 가이드라인 리셋, Off 상태로 초기화 |

### 4.3 애니메이션 명세

| 애니메이션 | 속성 | Duration | Easing |
|-----------|------|----------|--------|
| **페이드 인** | Alpha: 0 → 0.6 | 0.2s | EaseOutQuad |
| **페이드 아웃** | Alpha: 0.6 → 0 | 0.2s | EaseInQuad |
| **충돌 복귀 시 페이드 인** | Alpha: 0 → 0.6 | 0.15s | EaseOutQuad |
| **버튼 터치 피드백** | Scale: 1 → 0.9 → 1 | 0.1s | EaseOutBack |

---

## 5. 기술 명세

### 5.1 컴포넌트 구조

```
[새로 생성할 컴포넌트]
├── GuideLineButton.cs       - UI 토글 버튼 제어
├── GuideLineManager.cs      - 가이드라인 전체 관리 (싱글톤)
└── GuideLineRenderer.cs     - 개별 가이드라인 렌더링

[수정할 컴포넌트]
├── ArrowController.cs       - 가이드라인 연동 이벤트 추가
└── GameUIView.cs            - 버튼 참조 추가
```

### 5.2 GuideLineManager 인터페이스

```csharp
public class GuideLineManager : MonoBehaviour
{
    // 상태
    public bool IsEnabled { get; private set; }

    // 이벤트
    public event Action<bool> OnGuideLineToggled;

    // 공개 메서드
    public void Toggle();                           // On/Off 전환
    public void SetEnabled(bool enabled);           // 상태 설정
    public void ShowGuideLine(ArrowController arrow);   // 특정 화살표 가이드라인 표시
    public void HideGuideLine(ArrowController arrow);   // 특정 화살표 가이드라인 숨김
    public void RefreshAllGuideLines();             // 모든 가이드라인 갱신
    public void Reset();                            // 레벨 리셋 시 초기화
}
```

### 5.3 GuideLineRenderer 명세

```csharp
public class GuideLineRenderer : MonoBehaviour
{
    // 참조
    [SerializeField] private LineRenderer _lineRenderer;

    // 설정
    [SerializeField] private Color _lineColor = new Color(0.53f, 0.53f, 0.53f, 0.6f);
    [SerializeField] private float _lineWidth = 0.02f;  // 월드 단위

    // 공개 메서드
    public void Initialize(ArrowController arrow);
    public void UpdateLine();                       // Head 위치/방향 변경 시 갱신
    public void FadeIn(float duration = 0.2f);
    public void FadeOut(float duration = 0.2f);
    public void SetVisible(bool visible);           // 즉시 표시/숨김
}
```

### 5.4 ArrowController 수정사항

```csharp
// 추가할 이벤트
public event Action<ArrowController> OnMoveStarted;     // 이동 시작 시
public event Action<ArrowController> OnReturnComplete;  // 충돌 복귀 완료 시

// 기존 이벤트 활용
public event Action<ArrowController> OnExtracted;       // 탈출 완료 시 (기존)
```

### 5.5 화면 경계 계산

```csharp
/// <summary>
/// 화살표 Head에서 화면 경계까지의 끝점 계산
/// </summary>
private Vector2 CalculateEndPoint(Vector2 startPos, Vector2 direction)
{
    Camera cam = Camera.main;

    // 화면 경계 (월드 좌표)
    Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, cam.nearClipPlane));
    Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, cam.nearClipPlane));

    float maxDistance = 50f;  // 최대 연장 거리 (안전장치)

    // 방향에 따른 경계 거리 계산
    float distanceToEdge = CalculateDistanceToEdge(startPos, direction, bottomLeft, topRight);

    return startPos + direction * Mathf.Min(distanceToEdge, maxDistance);
}
```

---

## 6. 데이터 저장

### 6.1 PlayerPrefs 키

| 키 | 타입 | 기본값 | 설명 |
|----|------|--------|------|
| `GuideLine_Enabled` | int (0/1) | 0 | 가이드라인 On/Off 상태 저장 (향후 확장용) |

> **MVP에서는 저장하지 않음**: 현재는 레벨 진입 시 항상 Off 상태로 시작

---

## 7. 엣지 케이스

### 7.1 고려할 상황

| 상황 | 처리 방법 |
|------|----------|
| **꺾이는 화살표** | Head 방향(마지막 세그먼트)만 가이드라인 표시 |
| **화살표가 다른 화살표 뒤에 있을 때** | 가이드라인은 항상 화살표 뒤에 렌더링되므로 자연스럽게 처리됨 |
| **카메라 이동/줌 중** | 가이드라인 끝점 실시간 갱신 (매 프레임) |
| **동시에 여러 화살표 이동** | 각 화살표 독립적으로 가이드라인 관리 |
| **레벨 클리어 연출 중** | 가이드라인 비활성화, 버튼 숨김 |
| **게임 오버 시** | 가이드라인 유지 (재시도 가능) |

### 7.2 성능 고려사항

- **최대 화살표 수**: 일반적으로 20개 이하 → LineRenderer 20개 이하
- **갱신 빈도**: 카메라 이동 시에만 끝점 재계산
- **오브젝트 풀링**: 필요시 GuideLineRenderer 풀링 적용

---

## 8. 접근성

### 8.1 시각적 접근성
- 가이드라인 색상은 배경과 충분한 대비 (회색 on 흰색/연한 배경)
- 두께가 너무 얇지 않아 인식 가능

### 8.2 조작 접근성
- 버튼 터치 영역 충분히 확보 (56x56dp)
- 버튼 위치가 주요 게임플레이 영역과 겹치지 않음

---

## 9. 향후 확장 고려

### 9.1 잠재적 확장 기능 (미구현)
- [ ] 가이드라인 색상 커스터마이징
- [ ] 충돌 지점까지만 표시하는 옵션
- [ ] 설정에서 기본 On/Off 상태 저장
- [ ] 튜토리얼에서 자동 On 처리

---

## 10. 구현 우선순위

### Phase 1 (MVP)
1. GuideLineManager 싱글톤 구현
2. GuideLineRenderer 구현 (LineRenderer 기반)
3. GuideLineButton UI 구현
4. ArrowController 이벤트 연동

### Phase 2 (Polish)
1. 페이드 애니메이션 적용
2. 버튼 터치 피드백 추가
3. 카메라 이동 시 끝점 갱신

### Phase 3 (Optional)
1. 설정 저장 기능
2. 성능 최적화 (오브젝트 풀링)

---

## 11. 체크리스트

### 구현 완료 조건
- [ ] 버튼 탭으로 On/Off 토글 동작
- [ ] On 시 모든 화살표에 가이드라인 표시
- [ ] Off 시 모든 가이드라인 숨김
- [ ] 화살표 이동 시작 시 해당 가이드라인 숨김
- [ ] 화살표 충돌 복귀 시 가이드라인 재표시
- [ ] 화살표 탈출 시 가이드라인 제거
- [ ] 레벨 재시작 시 Off 상태로 리셋
- [ ] 페이드 애니메이션 적용
- [ ] 버튼 터치 피드백 동작

---

## 변경 이력

| 날짜 | 버전 | 변경 내용 |
|------|------|----------|
| 2026-01-21 | 1.0 | 초안 작성 |