#if UNITY_EDITOR || DEV_DEFINE
namespace OJ.Dev
{
    /// <summary>
    /// 치트가 켜 둔 스위치. <b>게임 코드가 읽는 값이라 창과 갈라 둔다.</b>
    ///
    /// 예전에는 <c>PointCheatController</c>(치트 <i>창</i>)가 이 값을 들고 있었고
    /// <c>Wall.TakeDamage</c> 가 그 창을 참조했다 — 전투 코드가 UI 클래스를 아는 방향이다.
    /// 창을 갈아엎을 때마다 그 참조가 따라 움직여야 하고, 실제로 이번에 그랬다.
    ///
    /// <b>읽는 쪽도 <c>#if</c> 안에 둔다.</b> 그래야 릴리스 빌드에서는 무적 검사 자체가
    /// 코드에서 사라진다 — 꺼진 채로 남아 매 피격마다 분기를 타는 것과 다르다.
    /// </summary>
    internal static class DevCheatFlags
    {
        /// <summary>벽이 피해를 받지 않는다. 전투 밖에서 미리 켜 둘 수 있다.</summary>
        internal static bool WallInvincible { get; set; }
    }
}
#endif
