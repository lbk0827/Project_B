using System;

namespace Game.Data
{
    /// <summary>
    /// BoosterTable JSON 레코드
    /// 부스터별 가격 및 초기 보유량 정의
    /// </summary>
    [Serializable]
    public class BoosterTableRecord
    {
        public string BoosterId;    // 부스터 식별자 ("Hint", "Eraser", "ArrowDash")
        public int Price;           // 아이템 1개당 가격
        public int InitialCount;    // 신규 계정 생성 시 기본 지급 개수
    }
}
