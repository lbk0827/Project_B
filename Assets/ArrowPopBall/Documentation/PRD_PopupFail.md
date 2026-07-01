# PRD: 실패 팝업 (PopupFail)

## 1. 개요

### 1.1 목적
플레이어의 Life(하트)가 0이 되었을 때 표시되는 팝업으로, 게임 재시작 또는 광고 시청 후 계속하기 옵션을 제공합니다.

### 1.2 트리거 조건
- `GameManager`에서 `_currentLife`가 0이 되었을 때
- 화살표 충돌(OnCollided) 또는 자기 몸통 충돌(OnWallHit) 후 Life 차감 시

---

## 2. UI 구성

### 2.1 레이아웃
```
┌─────────────────────────────────────┐
│                                     │
│           Continue?                 │  ← 타이틀 텍스트
│                                     │
│          ❤️  ❤️  ❤️                │  ← HeartContainer (TopBarUI와 동일 Prefab)
│                                     │
│     Refill your lives for free      │  ← 설명 텍스트
│         and keep playing!           │
│                                     │
│      ┌─────────────────────┐        │
│      │      Restart        │        │  ← 테두리만 있는 버튼
│      └─────────────────────┘        │
│                                     │
│      ┌─────────────────────┐        │
│      │      Play On        │        │  ← 초록색 배경 버튼
│      └─────────────────────┘        │
│                                     │
└─────────────────────────────────────┘
```

### 2.2 UI 요소 상세

| 요소 | 설명 |
|------|------|
| **배경 패널** | 둥근 모서리(Radius) 9-Slice 스프라이트, 진한 네이비 색상 (#1E1E2E) |
| **타이틀** | "Continue?" 텍스트, 흰색, Bold |
| **HeartContainer** | TopBarUI의 HeartContainer와 동일한 Prefab 사용, 현재 Life 상태 표시 (0개 = 모두 빈 하트) |
| **설명 텍스트** | "Refill your lives for free and keep playing!", 회색 (#AAAAAA) |
| **Restart 버튼** | 테두리만 있는 스타일, 클릭 시 레벨 처음부터 재시작 |
| **Play On 버튼** | 초록색 (#2ECC71) 배경, 클릭 시 보상형 광고 재생 |

### 2.3 Hierarchy 구조
```
Canvas
└── PopupFail (CanvasGroup)
    ├── Background (Image - Dimming 용)
    └── Panel (Image - 둥근 사각형)
        ├── TXT_Title ("Continue?")
        ├── HeartContainer (Prefab Instance)
        ├── TXT_Description
        ├── BTN_Restart
        │   └── TXT_Restart ("Restart")
        └── BTN_PlayOn
            └── TXT_PlayOn ("Play On")
```

---

## 3. 기능 상세

### 3.1 팝업 표시 조건
```csharp
// GameManager.cs에서 Life가 0이 되었을 때
private void OnLifeChanged(int newLife)
{
    if (newLife <= 0)
    {
        PopupFailManager.Instance.Show();
    }
}
```

### 3.2 Restart 버튼 동작
1. PopupFail 닫기 (페이드 아웃)
2. 현재 레벨 데이터 다시 로드
3. Life를 최대값(3)으로 초기화
4. 게임 상태 초기화 (화살표, 풍선 재배치)

```csharp
public void OnRestartClicked()
{
    Hide(() => {
        GameManager.Instance.RestartLevel();
    });
}
```

### 3.3 Play On 버튼 동작
1. 보상형 광고 요청
2. 광고 시청 완료 시:
   - PopupFail 닫기
   - Life를 최대값(3)으로 회복
   - 사망 시점부터 게임 계속 (레벨 재시작 아님)
3. 광고 시청 실패/취소 시:
   - 팝업 유지, 에러 메시지 표시 (선택적)

```csharp
public void OnPlayOnClicked()
{
    AdManager.Instance.ShowRewardedAd(
        onSuccess: () => {
            Hide(() => {
                GameManager.Instance.ContinueWithFullLife();
            });
        },
        onFailed: () => {
            // 광고 실패 처리 (선택적)
            Debug.Log("Ad failed or cancelled");
        }
    );
}
```

---

## 4. 애니메이션

### 4.1 팝업 등장
1. Background 디밍 (alpha 0 → 0.7, 0.2초)
2. Panel Scale (0 → 1, Ease.OutBack, 0.3초)

### 4.2 팝업 종료
1. Panel Scale (1 → 0.9) + Alpha (1 → 0, 0.2초)
2. Background 디밍 해제 (0.7 → 0, 0.2초)

```csharp
private void PlayShowAnimation(Action onComplete)
{
    _canvasGroup.alpha = 0;
    _panel.localScale = Vector3.zero;

    DOTween.Sequence()
        .Append(_background.DOFade(0.7f, 0.2f))
        .Join(_panel.DOScale(1f, 0.3f).SetEase(Ease.OutBack))
        .Join(_canvasGroup.DOFade(1f, 0.2f))
        .OnComplete(() => onComplete?.Invoke());
}
```

---

## 5. 게임 상태 관리

### 5.1 팝업 활성화 시
- 게임 일시정지 (`Time.timeScale = 0` 또는 로직 정지)
- 입력 차단 (InputHandler에서 체크)

### 5.2 입력 차단
```csharp
// InputHandler.cs
private void OnPointerDown(Vector2 screenPos)
{
    if (PopupFailManager.Instance?.IsShowing == true) return;
    // ...
}
```

---

## 6. Life 시스템 연동

### 6.1 Life 감소 시점
- `ArrowController.OnCollided` 발생 시 (다른 화살표와 충돌)
- `ArrowController.OnWallHit` 발생 시 (자기 몸통과 충돌)

### 6.2 Life 회복
| 상황 | Life 값 |
|------|---------|
| 레벨 시작 | 최대값 (3) |
| Restart 버튼 | 최대값 (3) |
| Play On (광고 시청) | 최대값 (3) |

---

## 7. 광고 연동 (MVP+)

### 7.1 보상형 광고 플로우
```
Play On 클릭 → 광고 로드 확인 → 광고 재생 → 완료 콜백 → Life 회복 → 게임 계속
```

### 7.2 MVP 단계 (광고 없음)
- Play On 버튼 클릭 시 바로 Life 회복 처리 (광고 스킵)
- 추후 AdManager 연동 시 실제 광고 재생

```csharp
// MVP: 광고 없이 바로 처리
public void OnPlayOnClicked()
{
    // TODO: AdManager 연동 시 교체
    Hide(() => {
        GameManager.Instance.ContinueWithFullLife();
    });
}
```

---

## 8. 필요 리소스

### 8.1 스프라이트
- [x] 둥근 사각형 패널 (IMG_RoundedPanel.png) - Figma에서 제작
- [x] HeartContainer Prefab (TopBarUI에서 분리)

### 8.2 스크립트
- [ ] `PopupFailManager.cs` - 팝업 관리 싱글톤
- [ ] `PopupFailUI.cs` - UI 컴포넌트 (버튼 이벤트, 애니메이션)

### 8.3 수정 필요 스크립트
- [ ] `GameManager.cs` - Life 시스템, RestartLevel(), ContinueWithFullLife() 추가
- [ ] `InputHandler.cs` - PopupFail 활성화 시 입력 차단

---

## 9. 구현 우선순위

### Phase 1: 기본 UI
1. PopupFail GameObject 구조 완성
2. HeartContainer Prefab 연결
3. 버튼 이벤트 연결 (Restart)

### Phase 2: 게임 로직 연동
1. Life 시스템 구현 (GameManager)
2. Restart 기능 구현
3. 팝업 표시/숨김 로직

### Phase 3: Continue 기능
1. ContinueWithFullLife() 구현
2. 사망 시점 상태 저장/복원

### Phase 4: 광고 연동 (MVP+)
1. AdManager 연동
2. Play On 버튼 광고 재생 로직

---

*작성일: 2026-01-22*
*버전: 1.0*