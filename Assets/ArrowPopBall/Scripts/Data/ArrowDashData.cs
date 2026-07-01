namespace Game.Data
{
    /// <summary>
    /// Arrow Dash 아이템 데이터 관리
    /// BoosterData로 위임 (기존 호출부 호환 유지)
    /// </summary>
    public static class ArrowDashData
    {
        // ========== 상수 ==========
        private const string BOOSTER_ID = "ArrowDash";

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 현재 보유 개수 조회
        /// </summary>
        public static int GetCount()
        {
            return BoosterData.GetCount(BOOSTER_ID);
        }

        /// <summary>
        /// 아이템 1개 사용
        /// </summary>
        /// <returns>사용 성공 여부</returns>
        public static bool UseItem()
        {
            return BoosterData.UseItem(BOOSTER_ID);
        }

        /// <summary>
        /// 아이템 추가
        /// </summary>
        public static void AddItem(int amount)
        {
            BoosterData.AddItem(BOOSTER_ID, amount);
        }

        /// <summary>
        /// 아이템 개수 직접 설정 (테스트/치트용)
        /// </summary>
        public static void SetCount(int count)
        {
            BoosterData.SetCount(BOOSTER_ID, count);
        }

        /// <summary>
        /// 데이터 초기화 (리셋)
        /// </summary>
        public static void Reset()
        {
            BoosterData.Reset(BOOSTER_ID);
        }

        /// <summary>
        /// 아이템 보유 여부
        /// </summary>
        public static bool HasItem()
        {
            return BoosterData.HasItem(BOOSTER_ID);
        }
    }
}
