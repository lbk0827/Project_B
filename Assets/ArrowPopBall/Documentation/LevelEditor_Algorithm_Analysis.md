# Level Editor 알고리즘 분석 및 문제점

## 1. 현재 알고리즘 개요

### 1.1 알고리즘 이름: "Grow-Then-Orient" (성장 후 방향 결정)

### 1.2 핵심 아이디어
1. 먼저 화살표 몸통(body)을 랜덤하게 성장시킴 (방향 미정)
2. 모든 몸통 생성 후, 각 몸통의 Head/Tail 방향을 결정
3. 의존성 그래프를 분석하여 해답 순서 계산

### 1.3 알고리즘 흐름

```
GenerateArrowsGuaranteedSolvable()
├── 1단계: GrowRandomBody()로 몸통들 생성
│   └── 그리드 내부 어디서든 시작
│   └── 70% 확률로 꺾임 (높은 꺾임 확률)
│   └── 최소/최대 길이 제한 적용
│
├── 2단계: DetermineArrowOrientationWithCycleCheck()로 방향 결정
│   └── 각 몸통의 양 끝점에서 4방향 후보 수집
│   └── 후보별 점수 계산 (막는 화살표 수, 마주봄 여부, 경계 거리)
│   └── WouldCreateCycle()로 사이클 검사
│   └── 사이클 없는 첫 번째 후보 선택
│
├── 3단계: CalculateSolutionWithDependencies()
│   └── 위상 정렬로 해답 순서 계산
│   └── 사이클 감지 시 재시도
│
└── 최종 검증: QuickValidateWithSolutionOrder()
```

---

## 2. 핵심 데이터 구조

### 2.1 화살표 데이터
```csharp
class EditorArrow {
    int id;
    List<Vector2Int> cells;      // cells[0] = Tail, cells[^1] = Head
    ArrowDirection headDirection; // Head가 탈출하는 방향
    List<EditorSegment> segments; // 꺾임 정보
    GameColor color;
}
```

### 2.2 방향 Enum
```csharp
enum ArrowDirection {
    Up = 0,    // (0, 1)
    Down = 1,  // (0, -1)
    Left = 2,  // (-1, 0)
    Right = 3  // (1, 0)
}
```

### 2.3 의존성 관계
- **화살표 A가 화살표 B에 의존한다** = A의 탈출 경로에 B의 셀이 있음
- **즉, B가 먼저 탈출해야 A가 탈출 가능**

---

## 3. 핵심 함수 상세

### 3.1 WouldCreateCycle() - 사이클 검사
```csharp
private bool WouldCreateCycle(EditorArrow newArrow)
{
    // 1. 임시로 새 화살표 추가
    _arrows.Add(newArrow);

    // 2. 의존성 그래프 구축
    var dependencies = new Dictionary<int, HashSet<int>>();
    foreach (var arrow in _arrows)
    {
        var blocking = FindBlockingArrows(arrow);
        dependencies[arrow.id] = blocking;
    }

    // 3. 위상 정렬 (Kahn's Algorithm)
    // - inDegree = 0인 노드부터 처리
    // - 처리된 노드를 의존하는 노드들의 inDegree 감소

    // 4. 모든 노드가 정렬되면 사이클 없음
    return sortedCount != _arrows.Count + 1;
}
```

### 3.2 FindBlockingArrows() - 막는 화살표 찾기
```csharp
private List<int> FindBlockingArrows(EditorArrow arrow)
{
    // Head 위치에서 headDirection으로 직선 탐색
    // 경계 또는 무효 셀에 도달할 때까지
    // 경로상의 모든 다른 화살표 ID 수집
}
```

---

## 4. 문제점 분석

### 4.1 핵심 문제: "2단계에서 몸통 추가 순서가 고정됨"

**현상:**
- 몸통은 랜덤한 순서로 생성됨
- 2단계에서 생성된 순서대로 방향을 결정
- 먼저 방향이 결정된 화살표들이 나중 화살표의 선택지를 제한

**예시:**
```
몸통 생성 순서: A, B, C, D, E
방향 결정 순서: A → B → C → D → E

A 추가: 사이클 없음 ✓
B 추가: A와 사이클 없음 ✓
C 추가: A,B와 사이클 없음 ✓
...
E 추가: 어떤 방향을 선택해도 A,B,C,D 중 하나와 사이클! → null 반환
```

**결과:**
- 일부 몸통이 스킵됨 (null 반환)
- 하지만 스킵된 몸통의 **공간은 이미 점유됨**
- 나중에 추가된 화살표들이 점점 막다른 상황에 놓임

### 4.2 WouldCreateCycle()의 한계

**문제:**
새 화살표를 추가할 때 **기존 화살표들의 의존성도 변할 수 있음**을 고려하지 않음.

**예시:**
```
기존 상태:
  A → (탈출 경로 비어있음)
  B → (탈출 경로 비어있음)

새 화살표 C 추가:
  A의 탈출 경로에 C가 들어감 → A는 이제 C에 의존
  B의 탈출 경로에 C가 들어감 → B는 이제 C에 의존
  C의 탈출 경로에 A,B가 있음 → C는 A,B에 의존

  결과: A→C→A,B→C 사이클!
```

**현재 코드는 이 상황을 감지하지만, 해결하지 못함.**

### 4.3 의존성 방향 오류 가능성

**FindBlockingArrows() 분석:**
```csharp
Vector2Int head = arrow.cells[^1];  // Head 위치
Vector2Int dirVec = GetDirectionVectorInt(arrow.headDirection);
Vector2Int current = head;

for (...) {
    current = current + dirVec;  // Head 앞으로 탐색
    // current에 있는 다른 화살표 = 이 화살표가 의존
}
```

이 로직은 정확함. 그러나...

### 4.4 실제 게임 동작과 불일치 가능성

**게임 동작:**
- 화살표를 탭하면 Head 방향으로 **전체 몸통이 스네이크처럼 이동**
- 이동 중 다른 화살표와 충돌하면 막힘

**현재 검증:**
- Head에서 직선 경로만 검사
- **이동 중 Tail이 지나가는 경로는 검사 안 함**

```
예시:
  화살표 A: [□][□][→]  (Head가 오른쪽)
  화살표 B: [↓] 위치: A의 Tail 바로 위

  A가 오른쪽으로 이동하면:
  1. Head가 먼저 빠져나감
  2. Tail이 따라오면서 B의 위치를 통과
  3. 만약 B가 아직 있으면? 충돌!
```

**하지만 이건 실제로 문제가 아닐 수 있음:**
- A가 이동할 때 원래 A가 있던 셀은 비워짐
- B가 A의 원래 위치로 이동하는 것이 아니라면 충돌 안 함

---

## 5. 스크린샷 분석

### 5.1 첫 번째 스크린샷
```
원인: 화살표 1, 25, 3, 9, 4, 18, 5, 12, 6, 27, 7, 17, 8, 24, 10, 13, 11, 14, 15, 16, 19, 2, 20, 22, 23, 26, 28가 서로 막고 있습니다
- 28개 중 27개가 사이클에 포함
- 거의 모든 화살표가 서로 의존하는 상황
```

### 5.2 두 번째 스크린샷
```
원인: 화살표 2, 3, 4, 7, 6, 16, 10, 1, 20, 17, 21, 14, 22, 24가 서로 막고 있습니다
- 25개 중 14개가 사이클에 포함
```

### 5.3 공통점
- **사이클에 포함된 화살표 수가 매우 많음**
- 이는 화살표들이 "서로를 막는" 형태로 배치되었음을 의미
- 특히 그리드 중앙에 밀집된 화살표들이 문제

---

## 6. 근본적 문제 요약

### 6.1 알고리즘 설계상 결함

1. **몸통 먼저 생성 → 방향 나중 결정**의 한계
   - 몸통 위치가 고정된 후에는 방향 선택지가 제한적
   - 복잡한 인터락 상황에서 "사이클 없는 방향"이 존재하지 않을 수 있음

2. **순차적 추가의 한계**
   - 먼저 추가된 화살표가 나중 화살표를 제약
   - 전체 최적해를 찾지 못하고 그리디하게 추가

3. **재시도가 비효율적**
   - 사이클 발견 시 전체를 버리고 재생성
   - 같은 문제가 반복될 확률 높음

### 6.2 Reference Game과의 차이

Reference Game (예: Arrow Puzzle류):
- 반드시 해답이 있는 상태에서 퍼즐 디자인
- 역방향 설계: "해답 순서를 먼저 정하고, 그 순서대로 화살표 배치"

현재 알고리즘:
- 순방향 설계: "화살표 먼저 배치하고, 해답 순서 찾기"
- 해답이 없을 수 있음

---

## 7. 해결 방안 제안

### 7.1 방안 A: "역방향 시뮬레이션" (Reverse Simulation)

**아이디어:**
1. 빈 그리드에서 시작
2. 경계에서 안쪽으로 화살표를 "투입"
3. 투입 순서의 역순 = 해답 순서

**장점:**
- 100% Solvable 보장
- 단순하고 예측 가능

**단점:**
- Reference Game 수준의 복잡한 인터락 어려움
- Head가 항상 경계 근처에 배치됨

### 7.2 방안 B: "전역 최적화" (Global Optimization)

**아이디어:**
1. 모든 몸통 생성 후
2. 모든 가능한 방향 조합 탐색 (브루트포스 또는 백트래킹)
3. 사이클 없는 조합 찾기

**장점:**
- 더 다양한 패턴 가능

**단점:**
- 계산 복잡도 매우 높음 (4^n, n=화살표 수)
- 실시간 생성에 부적합

### 7.3 방안 C: "점진적 추가 + 롤백" (Incremental with Rollback)

**아이디어:**
1. 몸통 + 방향을 함께 결정
2. 사이클 발생 시 해당 몸통만 제거 (롤백)
3. 다른 위치/방향으로 재시도

**장점:**
- 부분적 실패에 강건
- 이미 성공한 화살표 유지

**단점:**
- 롤백 로직 복잡
- 공간 낭비 가능

### 7.4 방안 D: "제약 기반 생성" (Constraint-Based Generation)

**아이디어:**
1. 각 화살표가 가질 수 있는 "탈출 방향"을 미리 제약
2. 제약 전파 (Constraint Propagation)로 불가능한 상태 조기 탐지
3. SAT Solver 또는 CSP Solver 활용

**장점:**
- 수학적으로 엄밀한 해결
- 복잡한 패턴도 가능

**단점:**
- 구현 복잡도 높음
- 외부 라이브러리 필요할 수 있음

---

## 8. 권장 해결 방안

### 즉시 적용 가능한 개선: "역방향 시뮬레이션 + 꺾임 강화"

**원리:**
1. 경계에서 안쪽으로 화살표를 "밀어넣는" 시뮬레이션
2. 밀어넣을 때 랜덤하게 꺾임 추가
3. 투입 순서의 역순이 곧 해답

**구현:**
```
1. 진입점 = 그리드 경계의 모든 셀
2. 랜덤 진입점 선택
3. 진입 방향으로 화살표 "투입"
4. 이동 중 랜덤하게 90도 꺾임
5. 충돌 또는 최대 길이 도달 시 멈춤 → Head 위치 확정
6. 반복
7. 투입 순서의 역순 = 해답 순서
```

**왜 Solvable이 보장되는가:**
- 나중에 투입된 화살표는 먼저 투입된 화살표 "위에" 놓임
- 따라서 나중 화살표를 먼저 빼면, 먼저 화살표의 경로가 열림
- 이것이 "역순"이 해답인 이유

---

## 9. 현재 코드의 버그 가능성

### 9.1 DetermineArrowOrientationWithCycleCheck에서 null 반환 후 처리

```csharp
foreach (var body in bodies)
{
    var arrow = DetermineArrowOrientationWithCycleCheck(body, arrowId, occupied);
    if (arrow != null)
    {
        _arrows.Add(arrow);
        arrowId++;
    }
    // arrow == null인 경우: 해당 body의 셀은 이미 occupied에 표시됨
    // 하지만 화살표는 추가 안 됨
    // 결과: 빈 공간처럼 보이지만 다른 화살표가 사용 불가
}
```

**문제:**
- null 반환된 body의 공간이 "유령 점유" 상태
- 이후 화살표들의 성장이나 방향 선택에 영향

### 9.2 candidates 정렬 후 순회

```csharp
candidates.Sort((a, b) => a.score.CompareTo(b.score));

foreach (var candidate in candidates)
{
    if (!WouldCreateCycle(testArrow))
    {
        return arrow;  // 첫 번째 사이클 없는 후보 선택
    }
}
```

**문제:**
- 점수가 낮은 후보부터 시도
- 하지만 점수가 높아도 사이클 안 만드는 후보가 있을 수 있음
- 현재 로직은 괜찮지만, 점수 계산이 "사이클 회피"와 직결되지 않음

---

## 10. 결론

현재 "Grow-Then-Orient" 알고리즘은 **설계상 100% Solvable을 보장할 수 없습니다.**

**근본 원인:**
- 몸통 위치가 먼저 고정되면, 방향 선택만으로는 사이클을 피할 수 없는 상황 발생
- 특히 그리드가 꽉 찰수록 (Fill Rate 100%) 사이클 확률 급증

**권장 해결책:**
- "역방향 시뮬레이션" 알고리즘으로 전환
- 또는 "몸통 + 방향 동시 결정" 방식으로 변경

---

## 부록: 핵심 함수 위치

| 함수명 | 줄 번호 | 설명 |
|--------|---------|------|
| GenerateArrowsGuaranteedSolvable | 986 | 메인 생성 함수 |
| GrowRandomBody | 1090 | 몸통 성장 |
| DetermineArrowOrientationWithCycleCheck | 1239 | 방향 결정 + 사이클 체크 |
| WouldCreateCycle | 1369 | 사이클 존재 여부 검사 |
| FindBlockingArrows | 1818 | 막는 화살표 찾기 |
| CalculateSolutionWithDependencies | 1690 | 해답 순서 계산 |
| QuickValidateWithSolutionOrder | 1858 | 해답 검증 |
