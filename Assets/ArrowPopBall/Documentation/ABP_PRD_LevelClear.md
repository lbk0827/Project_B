# Level Clear 연출 기획서 (PRD)

---

## 1. 개요

**기능명**: Level Clear 연출 시스템

**한 줄 소개**: 레벨 클리어 시 플레이어에게 성취감과 만족감을 주는 시각적 연출 시퀀스

**우선순위**: P0 (핵심 기능)

---

## 2. 목표 및 배경

### 2.1 목표
- 레벨 클리어 시 **즉각적인 시각적 피드백**으로 성취감 제공
- **ASMR 계열 만족감** 강화 (하이퍼캐주얼 핵심 요소)
- 플레이어 **리텐션 향상**을 위한 긍정적 경험 제공

### 2.2 배경
현재 구현 상태:
- 레벨 클리어 시 0.8초 딜레이 후 로비 복귀 (연출 없음)
- 풍선 팝 애니메이션만 존재 (Punch Scale + Shake + Fade)
- 클리어 결과 패널 미구현

---

## 3. 연출 시퀀스 상세

### 3.1 전체 흐름도

```
[STAGE 1] 마지막 화살표 남음
    │
    ↓ (유저 탭)
[STAGE 2] Dot Matrix Pulse 시작
    │
    ↓ (0.3~0.5초)
[STAGE 3] Dot Matrix Pulse 진행 (중앙 → 외곽 웨이브)
    │
    ↓ (Pulse 종료 전)
[STAGE 4] 칭찬 텍스트 등장
    │
    ↓ (0.5초 후)
[STAGE 5] Confetti 이펙트 재생
    │
    ↓ (Confetti 종료)
[STAGE 6] 로비 복귀
```

### 3.2 Stage 1: 마지막 화살표 상태

**트리거 조건**
- 그리드에 화살표가 **1개만** 남은 상태
- 해당 화살표 탭 시 Stage 2로 전환

**시각적 변화** (선택적)
- 마지막 화살표 하이라이트 (Glow 또는 Pulse 효과)
- "Last One!" 힌트 텍스트 (선택적)

### 3.3 Stage 2: Dot Matrix Pulse 시작

**트리거 조건**
- 마지막 화살표 탭 (이동 시작)

**동작**
- Dot Grid 전체에 Pulse 애니메이션 준비
- 그리드 중앙 좌표 계산

### 3.4 Stage 3: Dot Matrix Pulse 진행

**핵심 연출**: 중앙에서 외곽으로 퍼지는 **웨이브 애니메이션** + **색상 변화**

**동작 상세**
```
1. 그리드 중앙점 계산
   - centerX = gridWidth / 2
   - centerY = gridHeight / 2

2. 레벨별 Pulse 색상 결정 (랜덤)
   - 레벨 시작 시 PulseColorPalette에서 랜덤 선택
   - 매 레벨마다 다른 색상으로 다양성 제공

3. 각 Dot의 중앙으로부터 거리 계산
   - distance = |dot.x - centerX| + |dot.y - centerY|  // Manhattan Distance
   - 또는 Euclidean Distance 사용

4. 거리 기반 딜레이로 Scale Up + 색상 변화 애니메이션
   - delay = distance * delayPerUnit (예: 0.05초)
   - 각 Dot:
     - Scale: 1.0 → 1.5 → 0 (사라짐)
     - Color: 기본색 → Pulse 색상 (동시 전환)

5. 웨이브 완료 시 모든 Dot 비활성화
```

**Pulse 색상 팔레트 (레벨별 랜덤 선택)**
```json
{
  "pulseColorPalette": [
    { "name": "Golden",   "color": "#FFD700", "description": "골드 (축하)" },
    { "name": "Coral",    "color": "#FF6B6B", "description": "코랄 (따뜻함)" },
    { "name": "Cyan",     "color": "#4ECDC4", "description": "시안 (청량함)" },
    { "name": "Violet",   "color": "#A855F7", "description": "바이올렛 (화려함)" },
    { "name": "Lime",     "color": "#84CC16", "description": "라임 (활력)" },
    { "name": "Pink",     "color": "#EC4899", "description": "핑크 (팝)" },
    { "name": "Orange",   "color": "#F97316", "description": "오렌지 (에너지)" },
    { "name": "Sky",      "color": "#38BDF8", "description": "스카이 (상쾌함)" }
  ]
}
```

**애니메이션 파라미터**
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| Scale Up 시간 | 0.15초 | 1.0 → 1.5 |
| Scale Down 시간 | 0.1초 | 1.5 → 0 |
| 딜레이 단위 | 0.03~0.05초 | 거리당 딜레이 |
| 이징 | EaseOutBack | Scale Up 시 탄성 효과 |
| **색상 전환 시간** | 0.1초 | 기본색 → Pulse 색상 |
| **색상 이징** | EaseOutQuad | 부드러운 색상 전환 |

**시각적 참고 (색상 변화 포함)**
```
시간 T=0:        시간 T=0.1:      시간 T=0.2:      시간 T=0.3:
  ○ ○ ○ ○ ○        ○ ○ ○ ○ ○        ○ ○ ○ ○ ○        ○ ○ ○ ○ ○
  ○ ○ ○ ○ ○        ○ ○ ○ ○ ○        ○ ● ● ● ○        ◐ ● ● ● ◐
  ○ ○ ● ○ ○   →    ○ ● ● ● ○   →    ● ● ◐ ● ●   →    ● ◐   ◐ ●
  ○ ○ ○ ○ ○        ○ ○ ○ ○ ○        ○ ● ● ● ○        ◐ ● ● ● ◐
  ○ ○ ○ ○ ○        ○ ○ ○ ○ ○        ○ ○ ○ ○ ○        ○ ○ ○ ○ ○

  ○ = 기본 Dot (회색/흰색)
  ● = Scale Up + 색상 변화 (예: 골드)
  ◐ = 사라지는 중 (페이드 아웃)
  (빈 공간) = 사라짐
```

### 3.5 Stage 4: 칭찬 텍스트 등장

**트리거 조건**
- Dot Matrix Pulse가 **50~70% 진행**되었을 때 (Pulse 종료 전)

**동작**
- 화면 중앙에 칭찬 텍스트 표시
- **PraiseWords 테이블/JSON**에서 랜덤 선택

**PraiseWords 목록**
```json
{
  "praiseWords": [
    "대단해!",
    "멋져!",
    "훌륭해!",
    "완벽해!",
    "최고야!",
    "굉장해!",
    "Amazing!",
    "Perfect!",
    "Excellent!",
    "Awesome!",
    "Great!",
    "Wonderful!"
  ]
}
```

**애니메이션**
| 속성 | 값 | 설명 |
|------|-----|------|
| 등장 | Scale 0 → 1.2 → 1.0 | Punch Scale |
| 이징 | EaseOutBack | 탄성 효과 |
| 시간 | 0.3초 | 등장 애니메이션 |
| 폰트 크기 | 72~96pt | 눈에 띄는 크기 |
| 색상 | 골드 + 그라데이션 | 축하 느낌 |

### 3.6 Stage 5: Confetti 이펙트

**트리거 조건**
- 칭찬 텍스트 등장 **0.5초 후**

**동작**
- Confetti 파티클 시스템 재생
- 화면 상단에서 아래로 내려오는 형태
- 다양한 색상의 작은 조각들

**파티클 파라미터**
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| 개수 | 50~100개 | 적당한 밀도 |
| 지속 시간 | 1.5~2초 | 재생 시간 |
| 색상 | 랜덤 (밝은 색상) | 축제 분위기 |
| 중력 | 약한 중력 | 천천히 떨어짐 |
| 회전 | 랜덤 회전 | 자연스러운 움직임 |

### 3.7 Stage 6: 로비 복귀

**트리거 조건**
- Confetti 이펙트 **종료** 후

**동작**
- 페이드 아웃 전환
- 로비 화면으로 복귀
- (추후) 결과 패널 표시 후 복귀

**타이밍**
- Confetti 종료 후 **0.3~0.5초** 딜레이
- 총 연출 시간: **약 3~4초**

---

## 4. 데이터 구조

### 4.1 PraiseWords 데이터

**파일 위치**: `Assets/Resources/Data/PraiseWords.json`

```json
{
  "praiseWords": [
    { "text": "대단해!", "weight": 1 },
    { "text": "멋져!", "weight": 1 },
    { "text": "훌륭해!", "weight": 1 },
    { "text": "완벽해!", "weight": 0.5 },
    { "text": "최고야!", "weight": 1 },
    { "text": "Amazing!", "weight": 1 },
    { "text": "Perfect!", "weight": 0.5 },
    { "text": "Excellent!", "weight": 1 }
  ]
}
```

**필드 설명**
| 필드 | 타입 | 설명 |
|------|------|------|
| text | string | 표시할 텍스트 |
| weight | float | 선택 가중치 (높을수록 자주 등장) |

### 4.2 PulseColorPalette 데이터

**파일 위치**: `Assets/Resources/Data/PulseColorPalette.json`

```json
{
  "colors": [
    { "name": "Golden",   "hex": "#FFD700" },
    { "name": "Coral",    "hex": "#FF6B6B" },
    { "name": "Cyan",     "hex": "#4ECDC4" },
    { "name": "Violet",   "hex": "#A855F7" },
    { "name": "Lime",     "hex": "#84CC16" },
    { "name": "Pink",     "hex": "#EC4899" },
    { "name": "Orange",   "hex": "#F97316" },
    { "name": "Sky",      "hex": "#38BDF8" }
  ]
}
```

**필드 설명**
| 필드 | 타입 | 설명 |
|------|------|------|
| name | string | 색상 이름 (로깅/디버그용) |
| hex | string | Hex 색상 코드 |

### 4.3 LevelClearConfig

**파일 위치**: `Assets/Resources/Config/LevelClearConfig.asset` (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "LevelClearConfig", menuName = "Config/Level Clear Config")]
public class LevelClearConfig : ScriptableObject
{
    [Header("Dot Matrix Pulse")]
    public float dotScaleUpTime = 0.15f;
    public float dotScaleDownTime = 0.1f;
    public float dotDelayPerDistance = 0.04f;
    public float dotMaxScale = 1.5f;
    public Ease dotScaleEase = Ease.OutBack;
    public float dotColorTransitionTime = 0.1f;  // 색상 전환 시간

    [Header("Praise Text")]
    public float praiseAppearTime = 0.3f;
    public float praisePunchScale = 1.2f;
    public float praiseDelayFromPulseStart = 0.5f;  // Pulse 시작 후 딜레이

    [Header("Confetti")]
    public float confettiDelayAfterPraise = 0.5f;
    public float confettiDuration = 2f;

    [Header("Transition")]
    public float returnToLobbyDelay = 0.5f;  // Confetti 종료 후 딜레이
}
```

---

## 5. 시스템 설계

### 5.1 클래스 다이어그램

```
┌─────────────────────────┐
│    LevelClearManager    │  ← 연출 전체 관리
├─────────────────────────┤
│ - StartClearSequence()  │
│ - OnLastArrowTapped()   │
└───────────┬─────────────┘
            │ 사용
            ↓
┌─────────────────────────┐     ┌─────────────────────────┐
│   DotMatrixPulseEffect  │     │    PraiseTextEffect     │
├─────────────────────────┤     ├─────────────────────────┤
│ - PlayPulse()           │     │ - ShowPraise()          │
│ - CalculateDelays()     │     │ - LoadPraiseWords()     │
└─────────────────────────┘     └─────────────────────────┘
            │                               │
            └───────────┬───────────────────┘
                        ↓
            ┌─────────────────────────┐
            │    ConfettiEffect       │
            ├─────────────────────────┤
            │ - PlayConfetti()        │
            │ - OnConfettiComplete()  │
            └─────────────────────────┘
```

### 5.2 이벤트 흐름

```csharp
// GameManager.cs에서 호출
public void OnArrowExtracted(ArrowController arrow)
{
    // ... 기존 로직 ...

    // 마지막 화살표인 경우 클리어 체크
    if (_arrows.Count == 0 && AllBalloonsPopped())
    {
        LevelClearManager.Instance.StartClearSequence();
    }
}

// LevelClearManager.cs
public class LevelClearManager : MonoBehaviour
{
    public void StartClearSequence()
    {
        StartCoroutine(ClearSequenceCoroutine());
    }

    private IEnumerator ClearSequenceCoroutine()
    {
        // Stage 3: Dot Matrix Pulse
        _dotMatrixEffect.PlayPulse();

        // Stage 4: Praise Text (Pulse 진행 중)
        yield return new WaitForSeconds(_config.praiseDelayFromPulseStart);
        _praiseTextEffect.ShowPraise();

        // Stage 5: Confetti
        yield return new WaitForSeconds(_config.confettiDelayAfterPraise);
        _confettiEffect.PlayConfetti();

        // Stage 6: Return to Lobby
        yield return new WaitForSeconds(_config.confettiDuration + _config.returnToLobbyDelay);
        SceneManager.LoadScene("Lobby");
    }
}
```

---

## 6. UI/UX 고려사항

### 6.1 반응성
- 모든 애니메이션은 **DOTween** 사용으로 부드러운 이징 적용
- 터치 입력 블로킹 (연출 중 추가 입력 방지)

### 6.2 성능
- Dot Matrix Pulse: Object Pooling 적용 (대량 Dot 처리)
- Confetti: GPU Instancing 활용

### 6.3 접근성
- 칭찬 텍스트: 충분히 큰 폰트 (72pt 이상)
- 고대비 색상 사용

---

## 7. 구현 우선순위

| 순서 | 기능 | 난이도 | 설명 |
|------|------|--------|------|
| 1 | DotMatrixPulseEffect | 중 | 웨이브 애니메이션 핵심 |
| 2 | PraiseTextEffect | 하 | 텍스트 + 애니메이션 |
| 3 | ConfettiEffect | 중 | 파티클 시스템 |
| 4 | LevelClearManager | 하 | 시퀀스 조율 |
| 5 | PraiseWords JSON | 하 | 데이터 파일 |

---

## 8. 테스트 케이스

### 8.1 기능 테스트
- [ ] 마지막 화살표 탭 시 연출 시작
- [ ] Dot Matrix Pulse 중앙→외곽 웨이브 확인
- [ ] **Dot 색상 변화 확인 (기본색 → Pulse 색상)**
- [ ] **레벨별 다른 Pulse 색상 적용 확인**
- [ ] 칭찬 텍스트 랜덤 표시 확인
- [ ] Confetti 이펙트 재생 확인
- [ ] 연출 완료 후 로비 복귀 확인

### 8.2 타이밍 테스트
- [ ] 전체 연출 시간 3~4초 내외
- [ ] Stage 간 자연스러운 전환

### 8.3 예외 케이스
- [ ] 연출 중 앱 백그라운드 전환 시 처리
- [ ] 저사양 기기에서 성능 확인

---

## 9. 향후 확장

### 9.1 별점 연출 추가
- 클리어 후 별점 (1~3성) 애니메이션
- 별이 하나씩 채워지는 연출

### 9.2 결과 패널
- 클리어 시간, 이동 횟수, 별점 표시
- "다음 레벨" / "다시 하기" 버튼

### 9.3 특별 클리어 연출
- Perfect Clear (최소 이동): 특별 이펙트
- 연속 클리어 보너스 연출

---

*문서 버전: 1.0*
*작성일: 2026-01-20*
*작성자: Claude (AI Assistant)*