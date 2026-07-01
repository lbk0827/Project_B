# GameScene 성능 분석 보고서

**분석일**: 2026-02-22
**대상**: Arrow Pop Ball - GameScene
**목적**: 모바일(iOS/Android) 출시 전 성능 최적화

---

## 1. 핵심 요약

| 구분 | 현황 |
|------|------|
| 오브젝트 풀링 | **미적용** - 모든 게임 오브젝트가 Instantiate/Destroy |
| 매 프레임 GC 할당 | **ArrowController에서 List 매 프레임 생성** |
| LINQ 사용 | TargetAreaUI, BalloonLayoutManager에서 게임플레이 중 사용 |
| GetComponent 캐싱 | 일부 미캐싱 (TargetAreaUI, BalloonLayoutManager) |

---

## 2. 파일별 상세 이슈

### 2-1. ArrowController.cs (치명적)

| 라인 | 이슈 | 빈도 | 심각도 |
|------|------|------|--------|
| 567 | `new List<Vector2>()` - UpdateSnakeAnimation | 매 프레임 (이동 중) | **치명** |
| 601 | `new List<Vector2>(_previousWorldPositions)` | 매 프레임 (이동 중) | **치명** |
| 346, 352 | UpdateReverseReturnAnimation List 생성 | 매 프레임 (복귀 중) | **치명** |
| 513, 532 | StartSnakeMove/Extract List 복사 | 이동 스텝마다 | 높음 |
| 673, 683 | CompleteOneStep List 복사 | 이동 스텝마다 | 높음 |
| 287-292 | BackupLaunchPosition List 복사 3개 | 발사 시 1회 | 중간 |
| 297 | RecordMovementSnapshot List 복사 | 이동 스텝마다 | 높음 |
| 759 | UpdateCollider - new GameObject 매번 생성 | 콜라이더 업데이트마다 | **치명** |
| 776 | ClearCellColliders - Destroy 매번 호출 | 콜라이더 업데이트마다 | **치명** |

### 2-2. TargetAreaUI.cs (높음)

| 라인 | 이슈 | 빈도 | 심각도 |
|------|------|------|--------|
| 146 | LINQ `.Count()` - GetRemainingCount | 승리 조건 체크마다 | 높음 |
| 154 | LINQ `.Count()` - GetTotalRemainingCount | 승리 조건 체크마다 | 높음 |
| 108 | LINQ `.FirstOrDefault()` - PopBalloon | 풍선 터짐마다 | 중간 |
| 162 | LINQ `.FirstOrDefault()` - GetBalloonWorldPosition | HomingArrow 매 프레임 | 높음 |
| 166 | `GetComponentInParent<Canvas>()` 매번 호출 | HomingArrow 매 프레임 | 높음 |
| 170 | `balloon.GetComponent<RectTransform>()` 매번 호출 | HomingArrow 매 프레임 | 높음 |
| 174, 177 | `Camera.main` 프로퍼티 매번 접근 | HomingArrow 매 프레임 | 중간 |

### 2-3. BalloonLayoutManager.cs (중간)

| 라인 | 이슈 | 빈도 | 심각도 |
|------|------|------|--------|
| 389-392 | LINQ `.Where().OrderByDescending().ToList()` + `GetComponent` | 레이아웃 변경마다 | 중간 |
| 227 | `balloon.GetComponent<RectTransform>()` 루프 내 | 풍선 배치마다 | 중간 |
| 311 | 동일 GetComponent 루프 | 재배치마다 | 중간 |

### 2-4. GridSystem.cs (높음)

| 라인 | 이슈 | 빈도 | 심각도 |
|------|------|------|--------|
| 112 | `Instantiate(_dotPrefab)` 개별 호출 | 레벨 시작 시 30~63개 | 높음 |
| 115 | `GetComponent<SpriteRenderer>()` dot마다 | 레벨 시작 시 30~63회 | 중간 |
| 134, 439 | `Destroy()` 개별 호출 | 레벨 종료/전환 시 | 높음 |

### 2-5. HomingArrow.cs (중간)

| 라인 | 이슈 | 빈도 | 심각도 |
|------|------|------|--------|
| 358 | `Instantiate(_launchParticlePrefab)` | 화살표 발사마다 | 중간 |
| 367 | `Destroy(particle.gameObject, delay)` | 화살표 발사마다 | 중간 |
| 375 | `Instantiate(_hitParticlePrefab)` | 풍선 터짐마다 | 중간 |
| 384 | `Destroy(particle.gameObject, delay)` | 풍선 터짐마다 | 중간 |

### 2-6. HomingArrowSpawner.cs / ArrowAnimationHelper.cs (낮음)

| 파일 | 라인 | 이슈 |
|------|------|------|
| HomingArrowSpawner | 75, 112, 155 | `new WaitForSeconds()` 코루틴마다 할당 |
| ArrowAnimationHelper | 149, 185, 195, 210 | `new WaitForSeconds()` 코루틴마다 할당 |

---

## 3. 기기별 예상 영향

| 기기 등급 | 예상 체감 |
|-----------|-----------|
| 고사양 (iPhone 14+, Galaxy S23+) | 거의 문제 없음 |
| 중사양 (iPhone 11, Galaxy A53) | 풍선 연쇄 터짐 시 순간 끊김 |
| 저사양 (iPhone 8, Galaxy A13) | 레벨 시작/종료 시 눈에 보이는 렉, 플레이 중 간헐적 끊김 |

---

## 4. 개선 방안 요약

| 우선순위 | 개선 항목 | 예상 효과 |
|----------|-----------|-----------|
| 1 | ArrowController List 재사용 | 매 프레임 GC 할당 제거 |
| 2 | ArrowController 콜라이더 풀링 | GameObject 생성/파괴 제거 |
| 3 | TargetAreaUI LINQ 제거 + 캐싱 | 에뮬레이터 할당 + 반복 GetComponent 제거 |
| 4 | GridSystem Dot 풀링 | 레벨 전환 시 GC 스파이크 제거 |
| 5 | BalloonLayoutManager LINQ 제거 | 정렬 시 불필요한 할당 제거 |
| 6 | HomingArrow 파티클 풀링 | 파티클 생성/파괴 제거 |
| 7 | WaitForSeconds 캐싱 | 코루틴 GC 할당 감소 |

---

## 5. 개선 상태

- [x] WaitForSecondsCache 유틸리티 생성
- [x] ArrowController List 재사용
- [x] ArrowController 콜라이더 풀링
- [x] TargetAreaUI LINQ 제거 + 캐싱
- [x] BalloonUIElement RectTransform 프로퍼티 공개
- [x] BalloonLayoutManager LINQ 제거
- [x] GridSystem Dot 풀링
- [x] HomingArrow 파티클 풀링
