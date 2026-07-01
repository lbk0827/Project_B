# Level Editor 개선 설계안 (vc5 참조, BasketMode 제외)

## 문서 목적
- vc5 Level Editor의 강점을 ArrowPopBall에 이식하기 위한 설계안.
- BasketMode 의존 기능을 제외하고, **화살표 생성 품질 + 검증 UX + 작업 효율**을 높이는 것이 목표.

## 1) 비교 요약
### ArrowPopBall (현재)
- 강점
  - `ReverseGrowthGenerator` 기반 생성 안정화
  - ShapeMask / ColorMap 지원
  - 난이도(`maxFreeArrows`) 기반 생성 재시도(best-effort) 지원
  - Stage 패널과 기본 solvable 표시 존재
- 한계
  - 검증 실패 사유가 제한적(문제 화살표 타입/목록 가시성 부족)
  - 패턴 생성이 체크박스 중심(가중치 기반 비율 조절 부족)
  - 배치 생성(batch) / 대량 검증 워크플로우가 약함

### vc5 (참고)
- 강점
  - 패턴 확률 + 패턴별 활성/가중치 조합
  - Validation 결과 구조화(문제 타입/문제 화살표 인덱스/사유)
  - Generate/Validate/Statistics 패널의 운영 UX 완성도 높음
  - Batch Generate + 자동 재시도 + 실패 완화(relaxation) 흐름
- 제외 대상
  - Basket sequence UI/검증/자동 생성 로직
  - Basket 크기/버퍼/visible/extra basket 관련 파라미터

## 2) 이식 대상 기능 (BasketMode 제외)
### A. 생성 알고리즘 영역
- 패턴 선택 고도화
  - 현재: on/off 기반
  - 목표: `PatternChance + PatternWeight` 방식 도입
- 생성 실패 완화
  - 현재: best-effort 재시도
  - 목표: 단계별 완화 파라미터(길이/분기/패턴 확률) 적용 가능 구조
- Fill Rate 보강 정책 명시화
  - `Gap Filling`, `Single-cell fallback` 조건을 UI에 노출

### B. 검증/디버깅 영역
- Validation 결과 모델 확장
  - `valid`, `reason`, `problemType`, `problemArrowIndices`
- 문제 화살표 탐색 UX
  - 실패 시 인덱스 버튼 목록 표시
  - 클릭 시 프리뷰에서 해당 화살표 하이라이트
- 검증 재실행 버튼 및 캐시
  - `Revalidate` 버튼, dirty 플래그 기반 재검증

### C. 에디터 UI/운영 영역
- Generate 패널 개선
  - Auto/Manual 파라미터 시각화 강화
  - “현재 설정으로 예상 결과” 미리보기(색상 수, 길이 범위, 목표 밀도)
- Statistics 패널 개선
  - Main/Filler/총 셀 점유율/내부 Head 비율/난이도 지표 표시
- Batch 생성(선택)
  - Stage 범위 지정 일괄 생성
  - 레벨별 성공/실패/재시도 로그

## 3) 구현 순서 (권장)
### Step 1. Validation UX 개선 (저위험, 고효과)
- `LevelEditorWindow`에 검증 결과 상세 UI 추가
- `SolvabilityValidator` 결과 구조 확장(문제 타입/인덱스)
- 목표: “왜 실패했는지”를 즉시 파악 가능

### Step 2. 패턴 생성 가중치 도입
- `ReverseGrowthGenerator`에 pattern weight 입력 인터페이스 추가
- 기존 체크박스는 유지하되 고급 옵션에서 가중치 노출
- 목표: 생성 스타일 재현성과 튜닝 속도 향상

### Step 3. 생성 완화 정책(재시도 전략) 도입
- N회 실패 시 파라미터 완화 단계 적용
- 예: max 길이↓, 꺾임 확률↓, 패턴 확률↓, fallback 우선
- 목표: 대형 그리드에서 생성 성공률 상승

### Step 4. Statistics/Batch 개선
- 통계 패널 + 배치 생성 + 결과 요약 로그 추가
- 목표: 레벨 대량 제작 워크플로우 단축

## 4) 코드 반영 포인트 (초안)
- `Assets/Scripts/Editor/LevelEditorWindow.cs`
  - Generate/Validation/Stats UI 확장
- `Assets/Scripts/Editor/LevelEditor/ReverseGrowthGenerator.cs`
  - 패턴 가중치/완화 전략 입력 지원
- `Assets/Scripts/Editor/LevelEditor/SolvabilityValidator.cs`
  - ValidationResult 확장(문제 타입/인덱스)
- 필요 시 신규 파일
  - `LevelEditorValidationResult.cs` (결과 모델 분리)
  - `LevelEditorGenerationPolicy.cs` (재시도/완화 파라미터)

## 5) 성공 기준
- 생성 실패 시 원인과 문제 화살표를 즉시 식별 가능
- 동일 설정에서 생성 품질 편차 감소(패턴/밀도/난이도 안정화)
- 20x20, 30x30 기준 생성 성공률/소요시간 개선

## 6) 다음 작업 시작점
- Step 1부터 착수:
  - Validation 결과 모델 확장
  - `LevelEditorWindow`에 실패 사유 + 문제 화살표 버튼 UI 추가

