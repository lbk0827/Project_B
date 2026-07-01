namespace Game.Data
{
    /// <summary>
    /// 화살표 방향 (4방향)
    /// </summary>
    public enum ArrowDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    /// <summary>
    /// 화살표/풍선 색상 (13색)
    /// </summary>
    public enum GameColor
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3,
        Purple = 4,
        Orange = 5,
        Cyan = 6,
        Pink = 7,
        Brown = 8,
        Lime = 9,
        Navy = 10,
        Magenta = 11,
        Black = 12
    }

    /// <summary>
    /// 화살표 상태
    /// </summary>
    public enum ArrowState
    {
        Idle,       // 대기 상태
        Dragging,   // 드래그 중
        Moving,     // 이동 중
        Extracted,  // 탈출 완료
        Homing,     // 호밍 중
        Destroyed   // 파괴됨
    }

    /// <summary>
    /// 풍선 상태
    /// </summary>
    public enum BalloonState
    {
        Active,     // 활성 상태
        Targeted,   // 타겟팅됨
        Popping,    // 터지는 중
        Popped      // 터짐 완료
    }

    /// <summary>
    /// 게임 상태
    /// </summary>
    public enum GameState
    {
        Loading,    // 로딩 중
        Ready,      // 준비 완료
        Playing,    // 플레이 중
        Paused,     // 일시정지
        Clear,      // 클리어
        Failed      // 실패
    }

    /// <summary>
    /// 레벨 난이도
    /// </summary>
    public enum Difficulty
    {
        Normal = 0,
        Hard = 1,
        Nightmare = 2
    }
}