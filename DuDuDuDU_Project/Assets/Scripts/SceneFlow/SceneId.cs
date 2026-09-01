namespace OJ.SceneFlow
{
    /// <summary>
    /// 이 게임이 가진 씬. (MIGRATION_BASELINE 9.3)
    ///
    /// <b>왜 문자열이 아니라 enum 인가.</b> 예전에는 <c>SceneNames</c> 의 문자열 상수를
    /// <c>SceneManager.LoadScene</c> 에 그대로 넘겼다. 오타나 씬 이름 변경은 <b>컴파일에서
    /// 걸리지 않고</b> 실행 중 "Scene couldn't be loaded" 로만 드러난다. 게다가 그 이름이
    /// 빌드 세팅에 실제로 들어 있는지는 아무도 확인하지 않았다 — 빌드 목록에서 빠진 씬은
    /// 에디터에서 잘 돌다가 <b>실기에서만</b> 못 연다.
    ///
    /// enum 으로 두면 오타가 컴파일 오류가 되고, 빌드 세팅과의 대조를 한 곳
    /// (<see cref="SceneCatalog"/>)에서 자동으로 할 수 있다.
    /// </summary>
    public enum SceneId
    {
        Title = 0,
        Lobby = 1,
        Battle = 2,
    }
}
