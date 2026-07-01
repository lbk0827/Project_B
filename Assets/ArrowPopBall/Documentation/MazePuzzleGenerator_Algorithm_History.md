# MazePuzzleGenerator 알고리즘 변경 히스토리

## 개요

대형 그리드(20x20+)에서 Maze-based 퍼즐 생성 시 발생하는 문제들을 해결하기 위한 알고리즘 개선 과정을 기록합니다.

**목표**:
- 100% Fill Rate 유지
- Solvability 검증 통과
- 사이클 없는 의존성 그래프 (DAG 보장)
- 미로 형태(정돈된 패턴) 유지

---

## 핵심 규칙

### HEAD 정렬 규칙
```
cells[^2] → cells[^1] 방향 = headDirection
```
화살표의 마지막 두 셀의 방향이 HEAD 방향과 일치해야 함.

### 자기 참조 금지
HEAD 탈출 경로에 자신의 Body 셀이 없어야 함.

### 레이어 기반 의존성
- Layer 0: 경계에 인접한 화살표 → 경계로 직접 탈출
- Layer N: Layer N-1 이하에만 의존 (역방향 의존 금지 → DAG 보장)

---

## 버전별 변경 내역

### v1~v2: 초기 구현
- 기본 Maze-based Dependency Chain Generation 알고리즘
- 10x10 그리드에서 정상 동작
- **문제**: 대형 그리드에서 사이클 발생

---

### v3: 분할 전략 도입

**변경 내용**:
- 실패한 화살표를 경계 셀에서 분할하여 재시도
- `SplitFailedArrowsAtBoundaryCells` 메서드 추가
- `FindMiddleBoundaryCellWithEscape` 메서드 추가
- `SplitArrowAtIndex` 메서드 추가

**파이프라인**:
```
Phase 1 → Phase 2 → Phase 3 (방향 할당 + 분할 재시도) → Phase 4 → Phase 5
```

**문제점**:
```
splitCount=0, skippedTooShort=55
```
- 화살표 길이가 너무 짧아서 분할 불가 (최소 3셀 필요)
- 대부분의 화살표가 2셀로 생성됨

---

### v4: 최소 길이 강제 + 병합 전략

**변경 내용**:

1. **`_minArrowLength` 기본값 변경**: 2 → 3
   ```csharp
   private int _minArrowLength = 3;  // 분할 전략 호환성 (최소 3셀 필요)
   ```

2. **`SetParameters` 검증 강화**:
   ```csharp
   _minArrowLength = Mathf.Max(3, minArrowLength);  // 최소 3셀 강제
   ```

3. **패턴 완성 조건 완화**:
   ```csharp
   int minPatternLength = Mathf.Max(_minArrowLength + 1, 4);
   ```

4. **`CalculateArrowLength` 최소값 보장**:
   ```csharp
   return Mathf.Max(3, Mathf.Clamp(result, _minArrowLength, _maxArrowLength));
   ```

5. **`MergeShortArrows` 메서드 추가** (Phase 2.5):
   - 인접한 짧은 화살표들을 병합
   - 분할 전략 호환성을 위해 최소 길이 보장

**파이프라인**:
```
Phase 1 → Phase 2 → Phase 2.5 (병합) → Phase 3 → Phase 4 → Phase 5
```

**문제점**:
```
[DependencyGraph] Cycle resolution attempt 15: 50 arrows in cycle
[DependencyGraph] All strategies failed at attempt 15
[MazePuzzleGenerator] Phase 4 failed: Could not resolve cycle
```
- 50개 화살표가 사이클 형성
- 70%+ 화살표가 경계로 탈출 불가 (내부에 갇힘)

---

### v5: 경계 접근성 보장 + 사이클 방지 (현재)

**근본 원인 분석**:

`SetHeadToExternalDirection` (line 965-968)에서 모든 방법이 실패해도 자연 방향을 설정:
```csharp
// 모두 실패 → 자연 방향 유지 (Phase 4에서 처리)
arrow.headDirection = naturalDir;  // ← 이 방향이 다른 화살표를 막음!
return false;
```

**사이클 형성 원인**:
1. Layer 0 화살표 탈출 실패 (HEAD 정렬 규칙/자기 참조로 탈출 불가)
2. 자연 방향이 내부를 향함 (다른 화살표를 막음)
3. 연쇄 의존성 형성 → 사이클

---

#### v5 변경 내용

**1. Phase 2.75 추가 - 경계 접근성 사전 보장**

```csharp
// Phase 2.75: 경계 접근성 보장
arrows = EnsureBoundaryAccessibility(arrows, cellToArrowIdPreCheck);
```

**신규 메서드**:
- `EnsureBoundaryAccessibility`: 모든 화살표가 최소 한 끝점에서 경계로 탈출 가능하도록 보장
- `CanEscapeFromEitherEndpoint`: 양 끝점 중 하나라도 경계로 탈출 가능한지 확인

**2. 자연 방향 폴백 제거**

```csharp
// Before (v4)
arrow.headDirection = naturalDir;
return false;

// After (v5) - 방향 설정 없이 실패 반환
return false;
```

**3. 강제 탈출 로직 추가**

**신규 메서드**:
- `ForceAnyBoundaryEscape`: 모든 셀에서 모든 방향 탐색하여 강제 경계 탈출 시도
- `TryReorderCellsForHead`: 특정 셀을 HEAD로 만들기 위해 셀 순서 재정렬

**4. Layer 0 강제 탈출**

```csharp
if (layer == 0)
{
    bool success = SetHeadToExternalDirection(arrow, cellToArrowId);

    if (!success)
    {
        // 마지막 수단: 모든 4방향에서 경계 탈출 가능한 방향 강제 탐색
        success = ForceAnyBoundaryEscape(arrow, cellToArrowId);
    }

    if (!success)
    {
        Debug.LogError($"Layer 0 Arrow {arrow.id} cannot escape! Critical issue.");
    }
}
```

**5. Layer N 엄격 검증**

```csharp
else // layer > 0
{
    var targetInfo = FindNearestLowerLayerArrow(arrow, arrowLayers, cellToArrowId);

    bool success = false;
    if (targetInfo.HasValue)
    {
        int targetLayer = arrowLayers.GetValueOrDefault(targetInfo.Value.arrowId, int.MaxValue);

        // 엄격한 검증: 타겟 레이어가 현재 레이어보다 낮아야 함
        if (targetLayer < layer)
        {
            success = SetHeadTowardArrow(...);
        }
    }

    // Fallback 1: 경계 탈출 시도
    if (!success)
        success = SetHeadToExternalDirection(arrow, cellToArrowId);

    // Fallback 2: 강제 경계 탈출 시도
    if (!success)
        success = ForceAnyBoundaryEscape(arrow, cellToArrowId);
}
```

---

#### v5 파이프라인

```
Phase 1: 미로 경로 생성 (100% Fill Rate)
    ↓
Phase 2: 경로를 화살표로 분할 (정돈된 패턴)
    ↓
Phase 2.5: 짧은 화살표 병합 (분할 전략 호환성)
    ↓
Phase 2.75: 경계 접근성 사전 보장 (사이클 방지) ← NEW
    ↓
Phase 3: Head 방향 결정 (레이어 기반 + 강제 탈출)
    ↓
Phase 4: 의존성 그래프 구축 및 검증
    ↓
Phase 5: 최종 검증 (Arrow 무결성 + Fill Rate + Solvability)
```

---

### v5.2: HEAD 정렬 규칙 완화 + Fill Rate 보장 (2026-01-20)

**v5 이후 발생한 문제**:
```
[MazePuzzleGenerator] Layer 0 Arrow 26 cannot escape! Critical issue.
[MazePuzzleGenerator] Layer 0 Arrow 34 cannot escape! Critical issue.
...
Fill rate too low (72.0%), retrying...
```
- Layer 0 화살표가 여전히 탈출 실패
- Fill Rate가 70-90%로 저하

**근본 원인 분석**:
1. `TryReorderCellsForHead`가 HEAD 정렬 규칙(cells[^2]→cells[^1] = headDirection)을 엄격하게 적용
2. 경계 인접 셀이라도 방향이 안 맞으면 `null` 반환
3. Phase 2.75 분할 실패 시 원본 화살표가 누락됨

---

#### v5.2 변경 내용

**1. HEAD 정렬 규칙 완화 - 경계 인접 셀 예외**

```csharp
// v5.2: 경계 인접 셀이고 경계 방향이면 HEAD 정렬 규칙 완화
if (IsCellAdjacentToBoundaryInDirection(targetCell, targetDir))
    return new List<Vector2Int>(cells);  // 방향 불일치도 허용
```

**신규 메서드**:
- `IsCellAdjacentToBoundaryInDirection`: 셀이 특정 방향의 경계에 인접해 있는지 확인

**2. ForceAnyBoundaryEscape 최적화**

```csharp
// v5.2: 끝점 우선 탐색 - 경계 인접한 끝점부터 확인
var endpoints = new List<(Vector2Int cell, int index)>
{
    (arrow.cells[arrow.cells.Count - 1], arrow.cells.Count - 1),
    (arrow.cells[0], 0)
};

foreach (var (cell, cellIndex) in endpoints)
{
    var boundaryDirs = GetBoundaryAdjacentDirections(cell);
    foreach (var dir in boundaryDirs)
    {
        // 경계 인접 방향 우선 탐색
    }
}
```

**신규 메서드**:
- `GetBoundaryAdjacentDirections`: 셀이 인접한 경계 방향들 반환

**3. Phase 2.75 분할 실패 시 원본 유지**

```csharp
// v5.2: 분할 성공 시에만 분할 결과 사용, 실패 시 원본 유지
if (splitSuccess)
{
    splitCount++;
}
else
{
    // 분할 실패 → 원본 유지 (Fill Rate 보장)
    result.Add(arrow);
    noSplitPossible++;
}
```

---

#### v5.2 핵심 개선점

| 항목 | v5 | v5.2 |
|------|-----|------|
| HEAD 정렬 규칙 | 엄격 (항상 적용) | 완화 (경계 인접 셀 예외) |
| ForceAnyBoundaryEscape | 모든 셀/방향 순차 탐색 | 끝점 + 경계 방향 우선 탐색 |
| 분할 실패 처리 | 원본 누락 | 원본 유지 (Fill Rate 보장) |

---

## 수정된 파일

| 파일 | 변경 내용 |
|------|-----------|
| `MazePuzzleGenerator.cs` | v3~v5.2 모든 변경사항 |
| `DependencyGraph.cs` | 사이클 해결 전략 강화 |
| `SolvabilityValidator.cs` | 신규 추가 (검증 로직) |
| `ArrowValidator.cs` | 화살표 무결성 검증 |

---

## 성공 기준

- [ ] 20x20 그리드에서 10회 연속 생성 성공
- [ ] 30x30 그리드에서 10회 연속 생성 성공
- [ ] Fill Rate 100%
- [ ] Solvability 검증 통과
- [ ] 사이클 0개 (Phase 4 사이클 해결 불필요)
- [ ] 미로 형태(정돈된 패턴) 유지

---

## 검증 방법

1. Unity Editor에서 `Tools > Arrow Pop > Level Editor` 열기
2. 20x20 그리드 설정
3. Generate Puzzle 클릭
4. Console 로그 확인:
   - `Phase 2.75` 로그 확인
   - `Layer 0 cannot escape` 에러 없음 확인
   - `Cycle detected` 메시지 없음 확인
   - `Phase 4 failed` 없음 확인
5. 10회 반복 테스트

---

## 관련 커밋

- `b5cd7bd`: fix: 알고리즘 FIX (v5 변경사항 포함)
- `92f62cd`: feat: 신규 알고리즘 (v3 초기 구현)
- `d439c67`: docs: Add Maze-based Dependency Chain Generation algorithm documentation

---

## 작성일

2026-01-19
