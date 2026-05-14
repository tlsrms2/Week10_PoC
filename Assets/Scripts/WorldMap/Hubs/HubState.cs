namespace Overlap.WorldMap
{
    /// <summary>
    /// 거점의 소유 상태를 나타냅니다.
    /// </summary>
    public enum HubState
    {
        /// <summary>플레이어의 메인 거점 (코어 HP 보유)</summary>
        Main = 0,

        /// <summary>플레이어가 점령한 거점</summary>
        Occupied = 1,

        /// <summary>아무도 점령하지 않은 빈 거점 (적 스폰 가능)</summary>
        Empty = 2,

        /// <summary>점령 시도 중 (디펜스 페이즈 진행 중)</summary>
        Contested = 3,
    }
}
