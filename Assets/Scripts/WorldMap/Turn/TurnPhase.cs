namespace Overlap.WorldMap
{
    /// <summary>
    /// 현재 게임 진행 단계를 나타냅니다.
    /// </summary>
    public enum TurnPhase
    {
        /// <summary>아직 시작 전</summary>
        None = 0,

        /// <summary>
        /// 계획 단계 (턴제).
        /// 플레이어가 타워 배치, 거점 점령 시도, 침략 대응 중 하나를 선택합니다.
        /// </summary>
        Planning = 1,

        /// <summary>
        /// 디펜스 단계 (실시간).
        /// WaveSpawner가 적을 스폰하며, 메인 거점 CoreZone을 지켜야 합니다.
        /// </summary>
        Defense = 2,

        GameOver = 3,
    }

    /// <summary>
    /// 계획 단계에서 플레이어가 선택할 수 있는 행동 유형입니다.
    /// </summary>
    public enum TurnAction
    {
        /// <summary>행동 미선택</summary>
        None = 0,

        /// <summary>타일맵에 타워를 배치합니다</summary>
        PlaceTower = 1,

        /// <summary>빈 거점을 선택하여 점령을 시도합니다 → 디펜스 페이즈로 전환</summary>
        OccupyHub = 2,

        /// <summary>침략 이벤트에 대응합니다 → 디펜스 페이즈로 전환</summary>
        HandleInvasion = 3,
    }
}
