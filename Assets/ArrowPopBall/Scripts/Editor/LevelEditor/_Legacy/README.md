# _Legacy — 비활성 레벨 생성기 아카이브

이 폴더의 클래스들은 **현재 Level Editor("Generate" 버튼)에서 호출되지 않습니다.**

활성 생성 경로는 다음 하나입니다:

```
LevelEditorWindow.cs → ReverseGrowthGenerator.Generate()
   (+ DependencyGraph, SolvabilityValidator, GeometricPatternGenerator, ArrowValidator, ColorManager, GridUtility, ShapeMaskManager)
```

## 아카이브된 파일

| 파일 | 설명 | 상태 |
|------|------|------|
| `MazePuzzleGenerator.cs` | Maze 기반 5-Phase 생성기 (100% FillRate + 사이클 해소). 히스토리: `Documentation/MazePuzzleGenerator_Algorithm_History.md` | 비활성 |
| `MazeGuidedGenerator.cs` | Maze 경로 + 경계에서 화살표 생성 | 비활성 |
| `MazeGenerator.cs` | Recursive Backtracking 미로 경로 생성 (위 두 생성기 전용) | 비활성 |
| `PuzzleGenerator.cs` | 초기 범용 퍼즐 생성기 | 비활성 |
| `EscapeValidator.cs` | PuzzleGenerator 전용 탈출 검증기 | 비활성 |

## 왜 삭제하지 않고 남겼나

`Documentation/LevelEditor_Improvement_Roadmap.md`의 **Phase 2**에 "현행 ReverseGrowth와 Maze 계열의 강점을 결합"하려는 계획이 있어, 향후 참고를 위해 보존합니다.

되살리려면 파일을 상위 `LevelEditor/` 폴더로 다시 옮기면 됩니다(현재도 컴파일은 됨). 완전히 불필요하다고 판단되면 이 폴더째 삭제하세요(git 이력에 보존됨).
