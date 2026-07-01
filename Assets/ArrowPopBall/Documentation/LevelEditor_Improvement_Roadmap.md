# Level Editor 개선 요청 정리 및 실행 계획

## 1) 요청 배경 (2026-03-29)
- ArrowPopBall 프로젝트의 **Level Editor**를 개선하고 싶다.
- 개선 시 참고 기준은 아래 외부 프로젝트의 구현이다.
  - `/Users/bk/Desktop/LBK/2. 더블유게임즈/1. 하이퍼캐주얼/3. 다트 어웨이/ngfe_vc5`
- 단, vc5는 BasketMode를 포함한 게임이므로, ArrowPopBall에는 아래 원칙으로 적용한다.
  - **BasketMode 관련 로직/데이터/UI는 제외**
  - **화살표들이 모두 탈출 가능(풀이 가능)한 퍼즐 생성 품질에 집중**

## 2) 이번 개선의 목표
- `LevelEditorWindow` 사용성을 개선한다.
- 화살표 자동 생성 알고리즘을 개선한다.
- 생성 결과의 검증(풀이 가능성/품질)을 강화한다.
- 결과적으로 “빠르게 생성 + 높은 FillRate + 안정적인 Solvable”을 달성한다.

## 3) 범위 정의
### 포함
- `Assets/Scripts/Editor/LevelEditorWindow.cs`
- `Assets/Scripts/Editor/LevelEditor/*.cs` 중 생성/검증 관련 코드
- Level Editor UI/탭/검증 출력/통계 표시 개선
- 생성 파라미터 프리셋/자동 계산 개선

### 제외
- BasketMode 전용 기능 (basket size, sequence, basket UI, basket validation 등)
- 런타임 Basket 관련 클래스 연동
- 광고/메타/UI 비핵심 기능

## 4) 단계별 실행 계획
### Phase 1. vc5 비교 분석 및 이식 설계
- vc5의 LevelEditor 핵심 모듈을 분해한다.
  - `LevelEditorWindow.cs`
  - `LevelGenerator.cs`
  - `LevelValidator.cs`
  - `ArrowPlacer.cs`
- BasketMode 의존 코드를 분리 표기한다.
  - 재사용 가능: 생성 패턴, 배치 전략, 검증 UX, 통계/디버그 출력
  - 제외 대상: basket 플래그/파라미터/검증 분기
- 산출물: “ArrowPopBall 적용 설계안” 문서 + 작업 체크리스트

### Phase 2. 생성 알고리즘 개선
- 현행 알고리즘(ReverseGrowth/Maze 계열)과 vc5의 강점을 결합한다.
- 우선 적용 후보
  - 패턴 가중치 기반 생성(직선/ㄱ/U/지그재그 등)
  - FillRate 보강용 seed/fallback 전략
  - 경계 탈출 가능성 보장 규칙 강화
  - 생성 후 빠른 사전검증(문제 화살표 식별)
- 산출물: 생성기 코드 개선 + 파라미터 정리

### Phase 3. Level Editor UI/기능 개선
- Generate/Validate/Statistics 영역 UX 개선
- 검증 실패 사유 가시화(문제 화살표 하이라이트/리스트)
- 파라미터 프리셋 저장/불러오기(필요 범위 내)
- 산출물: 창 UI 개선 + 사용 흐름 단축

### Phase 4. 검증/테스트/튜닝
- 기준 케이스(예: 10x10, 20x20, 30x30) 반복 생성 테스트
- 지표 점검
  - Solvable 통과율
  - FillRate
  - 생성 시간
  - 실패 사유 분포
- 산출물: 튜닝 결과 요약 문서 + 권장 기본 파라미터

## 5) 완료 기준 (Definition of Done)
- BasketMode 코드 없이도 Editor가 정상 동작한다.
- 목표 크기 그리드에서 생성 안정성이 개선된다.
- 기존 대비 검증/디버깅 시간이 줄어든다.
- 문서(사용 가이드 + 파라미터 가이드)가 최신 상태로 유지된다.

## 6) 진행 방식
- “분석 → 설계 합의 → 구현 → 검증” 순서로 진행한다.
- 매 단계 종료 시 문서/코드/테스트 결과를 함께 남긴다.

