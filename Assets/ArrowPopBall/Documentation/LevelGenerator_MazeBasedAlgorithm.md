# Level Generator: Maze-based Dependency Chain Generation

## 문서 정보
- **작성일**: 2026-01-19
- **목적**: Arrow Pop Escape 게임의 자동 레벨 생성 알고리즘 설계
- **목표**: Fill Rate 100% + 100% Solvable + Reference Game 수준의 퍼즐

---

## 1. 개요

### 1.1 알고리즘 이름
**"Maze-based Dependency Chain Generation"** (미로 기반 의존성 체인 생성)

### 1.2 핵심 아이디어
1. 미로 알고리즘으로 그리드를 100% 채우는 경로 생성
2. 경로를 정돈된 패턴(ㄱ, ㄴ, ㄷ, ㅁ)으로 분할하여 화살표 생성
3. 의존성 비율에 따라 Head 방향을 내부로 설정하여 의존성 체인 생성
4. 위상 정렬로 해답 순서 계산

### 1.3 요구사항 체크리스트

| 요구사항 | 해결 방법 | 보장 여부 |
|---------|----------|----------|
| Fill Rate 100% | 미로 알고리즘 | ✅ |
| 100% Solvable | 역방향 원리 + 위상 정렬 | ✅ |
| 정돈된 패턴 | 패턴 기반 분할 | ✅ |
| Head 내부 배치 | 의존성 체인 생성 | ✅ |
| 난이도 조절 | 의존성 비율 파라미터 | ✅ |
| Reference Game 느낌 | 위 모든 요소 조합 | ✅ |

---

## 2. 기존 알고리즘의 문제점

### 2.1 현재 알고리즘: "Grow-Then-Orient" (스마트 백트래킹)

```
문제점:
1. Fill Rate 70~85% 한계 (파편화 발생)
2. 100% Solvable 보장 안됨
3. 화살표가 뱀처럼 구불구불 (정돈되지 않음)
4. 랜덤 배치로 인한 빈 공간 발생
```

### 2.2 기본 역방향 생성의 문제점

```
문제점:
- Head가 항상 그리드 경계에 배치됨
- 모든 화살표가 즉시 탈출 가능
- 의존성 없음 = 퍼즐이 아님 (너무 쉬움)
```

### 2.3 해결해야 할 핵심 과제

```
1. 그리드를 100% 채우면서
2. 정돈된 패턴(미로 느낌)으로 만들고
3. Head를 내부에 배치하여 의존성을 만들고
4. 100% Solvable을 보장해야 함
```

---

## 3. Reference Game 분석

### 3.1 화면 특징

```
┌─────────────────────────────────────┐
│ →→→↓  ←←←↑     ↓←←←←              │
│    ↓  ↑  ↑     ↓    ↑              │
│    →→→↑  ↑←←←  →→→→→↑              │
│       ↑     ↓                      │
│    ←←←↑  →→→↓  ↑←←←←←              │
│          ↓    ↑                    │
│       →→→↓    ↑←←←←←←              │
└─────────────────────────────────────┘

특징:
✅ Fill Rate: 거의 100%
✅ 정돈된 패턴 (ㄱ, ㄴ, ㄷ, ㅁ, 나선형)
✅ Head가 내부에 많이 배치됨
✅ 의존성 체인 존재 (순서대로 풀어야 함)
✅ 미로처럼 보임
```

### 3.2 발견한 패턴 종류

| 패턴 | 이름 | 형태 | 꺾임 수 |
|------|------|------|--------|
| ㄱ/ㄴ | L-Shape | `→→↓↓` | 1 |
| ㄷ/ㄹ | U-Shape / S-Shape | `→→↓↓←←` | 2 |
| ㅁ | Box / C-Shape | `→↓←↑` 3면 | 3 |
| 나선 | Spiral | 감싸는 형태 | 4+ |
| 직선 | Straight | `→→→→→→` | 0 |
| Nested | 겹 Box | ㅁ 안에 ㅁ | 다수 |

### 3.3 스타일 분류

| 스타일 | 설명 | 특징 |
|--------|------|------|
| 밀집형 (Dense) | 짧은 화살표, 빽빽함 | 짧은 ㄱ/ㄴ 패턴 |
| 정돈형 (Structured) | 긴 직선, 규칙적 꺾임 | 긴 ㄷ/ㅁ/나선형 |
| 혼합형 (Mixed) | 밀집 + 정돈 혼합 | 상단 밀집, 하단 정돈 등 |

---

## 4. 새 알고리즘 상세

### 4.1 전체 흐름도

```
┌─────────────────────────────────────────────────────────────────┐
│  INPUT                                                          │
│  - gridWidth, gridHeight                                        │
│  - difficulty (1~10)                                            │
│  - patternStyle (밀집형/정돈형/혼합형)                            │
│  - colorCount (색상 수)                                          │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 1: 미로 경로 생성 (Recursive Backtracking)                │
│  ─────────────────────────────────────────────────              │
│  • 그리드 전체를 하나의 연결된 경로로 생성                          │
│  • 결과: 100% 채워진 단일 경로                                    │
│  • Fill Rate: 100% 보장                                         │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 2: 경로를 화살표로 분할                                    │
│  ───────────────────────────────                                │
│  • 경로를 적절한 길이로 분할                                      │
│  • 정돈된 패턴 우선 (ㄱ, ㄴ, ㄷ, ㅁ, 나선형)                       │
│  • 최소/최대 길이 파라미터 적용                                    │
│  • 색상 할당                                                     │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 3: Head 방향 결정 (의존성 생성)                            │
│  ─────────────────────────────────────                          │
│  • 의존성 비율 = f(difficulty)                                   │
│  • 일부 화살표의 Head를 "내부 방향"으로 설정                       │
│  • 내부 Head → 다른 화살표를 막음 → 의존성 체인                    │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 4: 의존성 그래프 생성 및 검증                              │
│  ─────────────────────────────────                              │
│  • 각 화살표의 탈출 경로 분석                                     │
│  • "A가 B를 막음" → B는 A에 의존                                 │
│  • 사이클 검사 (있으면 Phase 3 재시도)                            │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 5: 해답 순서 계산 (위상 정렬)                              │
│  ─────────────────────────────────                              │
│  • 의존성 그래프를 위상 정렬                                      │
│  • 결과 = 해답 순서 (100% Solvable 보장)                         │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│  OUTPUT                                                         │
│  - arrows: 화살표 리스트 (위치, 방향, 색상, 셀 목록)               │
│  - balloons: 풍선 리스트 (색상별 개수)                            │
│  - solution: 해답 순서                                          │
│  - metadata: Fill Rate, 난이도, 최소 이동 횟수                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. 각 Phase 상세 설명

### 5.1 PHASE 1: 미로 경로 생성

#### 알고리즘: Recursive Backtracking

```
원리:
1. 시작점에서 출발
2. 방문하지 않은 이웃 셀 중 하나를 랜덤 선택하여 이동
3. 막다른 길에 도달하면 백트래킹
4. 모든 셀 방문 시 종료

결과: 그리드 전체를 커버하는 단일 연결 경로
```

#### 의사 코드

```python
def generate_maze_path(grid_width, grid_height):
    """
    Recursive Backtracking으로 그리드 전체를 커버하는 경로 생성

    Returns:
        path: List[Vector2Int] - 셀 좌표 리스트 (순서대로 연결됨)
    """
    total_cells = grid_width * grid_height
    visited = [[False] * grid_height for _ in range(grid_width)]
    path = []

    # 시작점 (랜덤 또는 (0,0))
    start = Vector2Int(0, 0)
    stack = [start]
    visited[start.x][start.y] = True
    path.append(start)

    while len(path) < total_cells:
        current = stack[-1]

        # 방문하지 않은 인접 셀 찾기 (상하좌우만, 대각선 X)
        neighbors = []
        for direction in [UP, DOWN, LEFT, RIGHT]:
            next_cell = current + direction
            if is_valid(next_cell, grid_width, grid_height):
                if not visited[next_cell.x][next_cell.y]:
                    neighbors.append(next_cell)

        if neighbors:
            # 랜덤하게 다음 셀 선택
            next_cell = random.choice(neighbors)
            visited[next_cell.x][next_cell.y] = True
            stack.append(next_cell)
            path.append(next_cell)
        else:
            # 막다른 길 - 백트래킹
            stack.pop()
            # 주의: path에서는 제거하지 않음 (전체 경로 유지)

    return path
```

#### 시각적 예시

```
생성 과정:
Step 1: (0,0) 시작
┌─────────┐
│ ●       │
│         │
│         │
└─────────┘

Step 2~N: 경로 확장
┌─────────┐
│ →→→↓    │
│    ↓    │
│ ←←←↓    │
└─────────┘

최종: 100% 채워진 경로
┌─────────┐
│ →→→↓    │
│ ↑  ↓    │
│ ↑←←↓    │
│ ↑  ↓    │
│ →→→↓    │
└─────────┘
```

---

### 5.2 PHASE 2: 경로를 화살표로 분할

#### 목표
- 긴 경로를 적절한 길이의 화살표로 분할
- 정돈된 패턴(ㄱ, ㄴ, ㄷ, ㅁ) 우선
- 뱀처럼 구불구불한 형태 방지

#### 패턴 감지 로직

```python
def detect_pattern(cells):
    """
    셀 리스트가 어떤 패턴인지 감지

    Returns:
        pattern_type: STRAIGHT, L_SHAPE, U_SHAPE, BOX, SPIRAL
    """
    if len(cells) < 2:
        return STRAIGHT

    # 방향 변화 횟수 계산
    direction_changes = count_direction_changes(cells)

    # 방향 변화 패턴 분석
    if direction_changes == 0:
        return STRAIGHT      # 직선: →→→→→
    elif direction_changes == 1:
        return L_SHAPE       # ㄱ/ㄴ: →→↓↓
    elif direction_changes == 2:
        return U_SHAPE       # ㄷ: →→↓↓←←
    elif direction_changes == 3:
        return BOX           # ㅁ 3면: →↓←↑ (한 면 열림)
    else:
        return SPIRAL        # 나선형: 4+ 꺾임
```

#### 분할 알고리즘

```python
def split_path_into_arrows(path, min_length, max_length, pattern_style):
    """
    경로를 화살표로 분할

    Args:
        path: 전체 경로
        min_length: 최소 화살표 길이
        max_length: 최대 화살표 길이
        pattern_style: DENSE(밀집) / STRUCTURED(정돈) / MIXED(혼합)

    Returns:
        arrows: List[EditorArrow]
    """
    arrows = []
    current_cells = []
    arrow_id = 1

    for i, cell in enumerate(path):
        current_cells.append(cell)

        # 분할 조건 체크
        should_split = False

        # 1. 최대 길이 도달
        if len(current_cells) >= max_length:
            should_split = True

        # 2. 정돈된 패턴 완성 (STRUCTURED 모드)
        elif pattern_style in [STRUCTURED, MIXED]:
            pattern = detect_pattern(current_cells)
            if pattern in [U_SHAPE, BOX] and len(current_cells) >= min_length:
                should_split = True

        # 3. 경로 끝
        elif i == len(path) - 1:
            should_split = True

        if should_split and len(current_cells) >= min_length:
            # 화살표 생성
            arrow = create_arrow(arrow_id, current_cells)
            arrows.append(arrow)
            arrow_id += 1

            # 새 화살표 시작 (마지막 셀을 공유하지 않음)
            current_cells = []

    # 남은 셀 처리
    if len(current_cells) >= 1:
        arrow = create_arrow(arrow_id, current_cells)
        arrows.append(arrow)

    return arrows
```

#### 시각적 예시

```
분할 전 (단일 경로):
→→→→→→→↓
↑      ↓
↑ ←←←←←↓
↑ ↓
↑ →→→→→→→

분할 후 (개별 화살표):
[A][A][A][A][A][A][A][B]
[C]            [B]
[C][D][D][D][D][D][B]
[C][D]
[C][E][E][E][E][E][E]

화살표 목록:
A: ㄱ자 (7칸)
B: ㄷ자 (9칸)
C: ㄷ자 (6칸)
D: ㄴ자 (6칸)
E: 직선 (6칸)
```

---

### 5.3 PHASE 3: Head 방향 결정 (의존성 생성)

#### 핵심 원리

```
의존성 체인 = Head가 내부를 향하는 화살표

Head가 경계를 향함 → 즉시 탈출 가능 → 의존성 없음
Head가 내부를 향함 → 다른 화살표에 막힘 → 의존성 있음

∴ 의존성 비율 ↑ = 내부 Head 비율 ↑ = 난이도 ↑
```

#### 의존성 비율 계산

```python
def calculate_dependency_ratio(difficulty):
    """
    난이도에 따른 의존성 비율 계산

    difficulty 1~3:  20~40% (쉬움 - 대부분 경계 Head)
    difficulty 4~6:  50~70% (보통 - 혼합)
    difficulty 7~10: 80~95% (어려움 - 대부분 내부 Head)
    """
    if difficulty <= 3:
        return 0.2 + (difficulty - 1) * 0.1  # 20%, 30%, 40%
    elif difficulty <= 6:
        return 0.4 + (difficulty - 3) * 0.1  # 50%, 60%, 70%
    else:
        return 0.7 + (difficulty - 6) * 0.0625  # 76%, 82%, 88%, 95%
```

#### Head 방향 결정 알고리즘

```python
def determine_head_directions(arrows, dependency_ratio, grid_size):
    """
    각 화살표의 Head 방향 결정

    Args:
        arrows: 화살표 리스트
        dependency_ratio: 의존성 비율 (0.0 ~ 1.0)
        grid_size: 그리드 크기
    """
    for arrow in arrows:
        head_cell = arrow.cells[-1]  # 마지막 셀이 Head

        if random.random() < dependency_ratio:
            # 내부 방향 시도 (의존성 생성)
            internal_dir = find_internal_direction(arrow, arrows, grid_size)
            if internal_dir:
                arrow.head_direction = internal_dir
            else:
                # 내부 방향 없으면 경계 방향
                arrow.head_direction = find_boundary_direction(head_cell, grid_size)
        else:
            # 경계 방향 (즉시 탈출 가능)
            arrow.head_direction = find_boundary_direction(head_cell, grid_size)


def find_internal_direction(arrow, all_arrows, grid_size):
    """
    다른 화살표를 막는 방향 찾기

    Returns:
        direction: 내부 방향 (다른 화살표 막음) 또는 None
    """
    head_cell = arrow.cells[-1]

    for direction in [UP, DOWN, LEFT, RIGHT]:
        # 이 방향으로 탈출 시 경로 계산
        escape_path = calculate_escape_path(head_cell, direction, grid_size)

        # 경로에 다른 화살표가 있는지 확인
        for other_arrow in all_arrows:
            if other_arrow.id == arrow.id:
                continue

            for cell in escape_path:
                if cell in other_arrow.cells:
                    # 이 방향으로 설정하면 other_arrow를 막음
                    return direction

    return None  # 내부 방향 없음


def find_boundary_direction(head_cell, grid_size):
    """
    가장 가까운 경계 방향 찾기

    Returns:
        direction: 경계로 가장 빨리 탈출할 수 있는 방향
    """
    distances = {
        UP: grid_size.height - 1 - head_cell.y,
        DOWN: head_cell.y,
        LEFT: head_cell.x,
        RIGHT: grid_size.width - 1 - head_cell.x
    }

    # 가장 가까운 경계 방향 반환
    return min(distances, key=distances.get)
```

#### 시각적 예시

```
의존성 비율 20% (쉬움):
┌─────────────────┐
│ →→→→→→→→→→→→→→ │  모든 Head가 경계
│ →→→→→→→→→→→→→→ │  → 아무거나 탭해도 OK
│ →→→→→→→→→→→→→→ │
└─────────────────┘

의존성 비율 80% (어려움):
┌─────────────────┐
│ →→→↓  ←←←↑     │  Head가 서로를 막음
│    ↓  ↑  ↑     │  → 순서대로 풀어야 함
│    →→→↑  ↑←←←  │
└─────────────────┘
```

---

### 5.4 PHASE 4: 의존성 그래프 생성 및 검증

#### 의존성 정의

```
화살표 A가 화살표 B에 "의존"한다 =
  A의 탈출 경로에 B의 셀이 있음 =
  B가 먼저 탈출해야 A가 탈출 가능
```

#### 의존성 그래프 구축

```python
def build_dependency_graph(arrows, grid_size):
    """
    화살표 간 의존성 그래프 구축

    Returns:
        graph: Dict[int, Set[int]]
               graph[A] = {B, C} → A는 B, C에 의존 (B, C가 먼저 빠져야 A 탈출 가능)
    """
    graph = {arrow.id: set() for arrow in arrows}

    for arrow in arrows:
        # 이 화살표의 탈출 경로 계산
        escape_path = calculate_escape_path(
            arrow.cells[-1],  # Head 위치
            arrow.head_direction,
            grid_size
        )

        # 탈출 경로에 있는 다른 화살표 찾기
        for other_arrow in arrows:
            if other_arrow.id == arrow.id:
                continue

            for cell in escape_path:
                if cell in other_arrow.cells:
                    # arrow는 other_arrow에 의존
                    graph[arrow.id].add(other_arrow.id)
                    break

    return graph


def calculate_escape_path(head, direction, grid_size):
    """
    Head에서 direction 방향으로 탈출할 때 지나가는 셀 목록
    """
    path = []
    current = head + direction_to_vector(direction)

    while is_inside_grid(current, grid_size):
        path.append(current)
        current = current + direction_to_vector(direction)

    return path
```

#### 사이클 검사 (Kahn's Algorithm)

```python
def has_cycle(graph):
    """
    의존성 그래프에 사이클이 있는지 검사
    사이클이 있으면 해답이 존재하지 않음!

    Returns:
        bool: True if cycle exists
    """
    # In-degree 계산
    in_degree = {node: 0 for node in graph}
    for node, deps in graph.items():
        for dep in deps:
            in_degree[node] += 1  # node가 dep에 의존

    # In-degree가 0인 노드부터 시작
    queue = [node for node, degree in in_degree.items() if degree == 0]
    processed = 0

    while queue:
        current = queue.pop(0)
        processed += 1

        # current에 의존하는 노드들의 in-degree 감소
        for node, deps in graph.items():
            if current in deps:
                in_degree[node] -= 1
                if in_degree[node] == 0:
                    queue.append(node)

    # 모든 노드가 처리되지 않았으면 사이클 존재
    return processed != len(graph)
```

#### 사이클 발생 시 처리

```python
def handle_cycle(arrows, graph, grid_size):
    """
    사이클 발견 시 일부 화살표의 Head 방향 변경
    """
    # 사이클에 포함된 화살표 찾기
    cycle_arrows = find_cycle_arrows(graph)

    # 사이클 내 화살표 중 하나의 Head를 경계 방향으로 변경
    for arrow_id in cycle_arrows:
        arrow = get_arrow_by_id(arrows, arrow_id)

        boundary_dir = find_boundary_direction(arrow.cells[-1], grid_size)

        # 방향 변경 후 사이클 해소되는지 확인
        old_dir = arrow.head_direction
        arrow.head_direction = boundary_dir

        new_graph = build_dependency_graph(arrows, grid_size)
        if not has_cycle(new_graph):
            return True  # 사이클 해소됨

        # 해소 안 되면 원복하고 다음 시도
        arrow.head_direction = old_dir

    return False  # 사이클 해소 실패 (Phase 2부터 재시도 필요)
```

---

### 5.5 PHASE 5: 해답 순서 계산

#### 위상 정렬 (Topological Sort)

```python
def calculate_solution_order(graph):
    """
    위상 정렬로 해답 순서 계산

    Returns:
        solution: List[int] - 화살표 ID 순서 (이 순서로 탈출하면 클리어)
    """
    # In-degree 계산
    in_degree = {node: 0 for node in graph}
    for node, deps in graph.items():
        for dep in deps:
            in_degree[node] += 1

    # In-degree가 0인 노드 = 먼저 탈출 가능
    queue = [node for node, degree in in_degree.items() if degree == 0]
    solution = []

    while queue:
        # 여러 개면 아무거나 선택 (해답이 여러 개일 수 있음)
        current = queue.pop(0)
        solution.append(current)

        # current에 의존하는 노드들 업데이트
        for node, deps in graph.items():
            if current in deps:
                in_degree[node] -= 1
                if in_degree[node] == 0:
                    queue.append(node)

    return solution
```

#### 시각적 예시

```
의존성 그래프:
  D → C → A
       ↘
         B

In-degree:
  D: 0 (의존 없음 - 먼저 탈출 가능)
  C: 1 (D에 의존)
  A: 1 (C에 의존)
  B: 1 (C에 의존)

해답 순서: D → C → A → B (또는 D → C → B → A)
```

---

## 6. 전체 의사 코드

```python
def generate_level(grid_width, grid_height, difficulty, pattern_style, color_count):
    """
    Maze-based Dependency Chain Generation 메인 함수

    Args:
        grid_width: 그리드 가로 크기
        grid_height: 그리드 세로 크기
        difficulty: 난이도 (1~10)
        pattern_style: DENSE / STRUCTURED / MIXED
        color_count: 사용할 색상 수

    Returns:
        LevelData: 생성된 레벨 데이터
    """
    grid_size = Vector2Int(grid_width, grid_height)

    # 파라미터 계산
    min_length, max_length = calculate_arrow_length_range(difficulty, pattern_style)
    dependency_ratio = calculate_dependency_ratio(difficulty)

    max_attempts = 10

    for attempt in range(max_attempts):
        # ═══════════════════════════════════════════════════════
        # PHASE 1: 미로 경로 생성
        # ═══════════════════════════════════════════════════════
        path = generate_maze_path(grid_width, grid_height)
        # 결과: 100% 채워진 단일 경로

        # ═══════════════════════════════════════════════════════
        # PHASE 2: 경로를 화살표로 분할
        # ═══════════════════════════════════════════════════════
        arrows = split_path_into_arrows(path, min_length, max_length, pattern_style)

        # 색상 할당
        assign_colors(arrows, color_count)

        # ═══════════════════════════════════════════════════════
        # PHASE 3: Head 방향 결정 (의존성 생성)
        # ═══════════════════════════════════════════════════════
        determine_head_directions(arrows, dependency_ratio, grid_size)

        # ═══════════════════════════════════════════════════════
        # PHASE 4: 의존성 그래프 생성 및 검증
        # ═══════════════════════════════════════════════════════
        dependency_graph = build_dependency_graph(arrows, grid_size)

        if has_cycle(dependency_graph):
            # 사이클 해소 시도
            if not handle_cycle(arrows, dependency_graph, grid_size):
                continue  # 실패 시 처음부터 재시도

            # 그래프 재구축
            dependency_graph = build_dependency_graph(arrows, grid_size)

        # ═══════════════════════════════════════════════════════
        # PHASE 5: 해답 순서 계산
        # ═══════════════════════════════════════════════════════
        solution_order = calculate_solution_order(dependency_graph)

        # ═══════════════════════════════════════════════════════
        # 풍선 생성 (화살표 색상별 개수)
        # ═══════════════════════════════════════════════════════
        balloons = create_balloons_from_arrows(arrows)

        # ═══════════════════════════════════════════════════════
        # OUTPUT
        # ═══════════════════════════════════════════════════════
        return LevelData(
            grid_width = grid_width,
            grid_height = grid_height,
            arrows = arrows,
            balloons = balloons,
            solution = solution_order,
            par_moves = len(arrows),
            difficulty = difficulty
        )

    # 최대 시도 횟수 초과
    raise Exception("Failed to generate solvable level")
```

---

## 7. 파라미터 정리

### 7.1 입력 파라미터

| 파라미터 | 타입 | 범위 | 설명 |
|---------|------|------|------|
| `gridWidth` | int | 4~20 | 그리드 가로 크기 |
| `gridHeight` | int | 4~20 | 그리드 세로 크기 |
| `difficulty` | int | 1~10 | 난이도 |
| `patternStyle` | enum | DENSE/STRUCTURED/MIXED | 패턴 스타일 |
| `colorCount` | int | 2~12 | 사용할 색상 수 |

### 7.2 계산되는 파라미터

| 파라미터 | 계산식 | 설명 |
|---------|--------|------|
| `minArrowLength` | 2 + difficulty/3 | 최소 화살표 길이 |
| `maxArrowLength` | 5 + difficulty/2 | 최대 화살표 길이 |
| `dependencyRatio` | f(difficulty) | 의존성 비율 (0.2~0.95) |

### 7.3 난이도별 파라미터 예시

| 난이도 | 의존성 비율 | 최소 길이 | 최대 길이 | 체감 |
|--------|-----------|----------|----------|------|
| 1 | 20% | 2 | 5 | 매우 쉬움 |
| 3 | 40% | 3 | 6 | 쉬움 |
| 5 | 60% | 3 | 7 | 보통 |
| 7 | 76% | 4 | 8 | 어려움 |
| 10 | 95% | 5 | 10 | 매우 어려움 |

---

## 8. 보장 사항

| 항목 | 보장 | 근거 |
|------|------|------|
| Fill Rate 100% | ✅ | 미로 알고리즘이 모든 셀 방문 |
| 100% Solvable | ✅ | 사이클 없는 DAG → 위상 정렬 가능 |
| 정돈된 패턴 | ✅ | 패턴 기반 분할 로직 |
| 난이도 조절 | ✅ | 의존성 비율 파라미터 |
| Reference Game 느낌 | ✅ | 위 모든 요소 조합 |

---

## 9. 구현 체크리스트

### 9.1 필요한 함수 목록

```
Phase 1:
□ generate_maze_path(width, height)
□ get_unvisited_neighbors(cell, visited, size)

Phase 2:
□ split_path_into_arrows(path, min, max, style)
□ detect_pattern(cells)
□ create_arrow(id, cells)
□ assign_colors(arrows, colorCount)

Phase 3:
□ calculate_dependency_ratio(difficulty)
□ determine_head_directions(arrows, ratio, size)
□ find_internal_direction(arrow, arrows, size)
□ find_boundary_direction(cell, size)

Phase 4:
□ build_dependency_graph(arrows, size)
□ calculate_escape_path(head, direction, size)
□ has_cycle(graph)
□ handle_cycle(arrows, graph, size)

Phase 5:
□ calculate_solution_order(graph)
□ create_balloons_from_arrows(arrows)
```

### 9.2 데이터 구조

```csharp
// Unity C# 예시

class EditorArrow {
    int id;
    List<Vector2Int> cells;       // cells[0]=Tail, cells[^1]=Head
    ArrowDirection headDirection;
    GameColor color;
    List<EditorSegment> segments;
}

class DependencyGraph {
    Dictionary<int, HashSet<int>> dependencies;
    // dependencies[A] = {B, C} → A는 B, C에 의존
}

class LevelData {
    int gridWidth, gridHeight;
    ArrowData[] arrows;
    BalloonData[] balloons;
    int[] solutionOrder;
    int parMoves;
    int difficulty;
}
```

---

## 10. 참고 자료

### 10.1 관련 문서
- `Assets/Documentation/LevelEditor_Algorithm_Analysis.md` - 기존 알고리즘 분석
- `Assets/Documentation/ABP_IDEA.md` - 게임 기획
- `.claude/refactoring-rule.md` - 리팩토링 규칙

### 10.2 참고 알고리즘
- Recursive Backtracking (미로 생성)
- Kahn's Algorithm (위상 정렬)
- Topological Sort (DAG 정렬)

---

## 변경 이력

| 날짜 | 버전 | 변경 내용 |
|------|------|----------|
| 2026-01-19 | 1.0 | 최초 작성 |
