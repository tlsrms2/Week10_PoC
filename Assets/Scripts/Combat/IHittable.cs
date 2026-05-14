namespace Overlap.Combat
{
    /// <summary>
    /// 데미지를 받을 수 있는 모든 대상이 구현하는 계약입니다.
    /// EnemyHealth, CoreHealth 등 구체 구현과 공격자(타워/투사체)를 분리합니다.
    /// </summary>
    public interface IHittable
    {
        bool IsDead { get; }

        /// <param name="amount">0보다 큰 양수 데미지</param>
        void TakeHit(float amount);
    }
}
