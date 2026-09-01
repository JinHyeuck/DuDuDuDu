namespace OJ.DI
{
    /// <summary>
    /// 앱이 백그라운드로 가거나 종료될 때 저장해야 하는 것. (MIGRATION_BASELINE 8.2)
    ///
    /// <b>왜 인터페이스인가.</b> 예전에는 매니저마다 자기 <c>OnApplicationPause</c> /
    /// <c>OnApplicationQuit</c> 를 들고 있었다. 그래서 "앱이 죽을 때 무엇이 저장되는가"를
    /// 알려면 파일 9개를 열어 봐야 했고, <b>새 매니저가 그 콜백을 빠뜨려도 아무도 모른다.</b>
    ///
    /// 이제는 이 인터페이스를 붙이고 인스톨러에서 <c>.As&lt;ISaveOnApplicationLifecycle&gt;()</c>
    /// 로 등록하기만 하면 <see cref="SaveOnApplicationLifecycle"/> 이 알아서 부른다.
    /// 목록이 한 곳에 모이고, 빠뜨리면 등록이 없다는 것이 인스톨러에서 눈에 보인다.
    /// </summary>
    public interface ISaveOnApplicationLifecycle
    {
        /// <summary>
        /// 소유한 것을 전부 저장한다.
        ///
        /// <b>로드 전에 불릴 수 있다는 것을 전제로 짤 것.</b> 구현체는 아직 읽지 않았으면
        /// 저장을 건너뛰어야 한다 — 인메모리 기본값으로 파일을 덮으면 진행도가 사라진다.
        /// 던지지 말 것: 종료 경로에서 예외가 나면 뒤에 있는 다른 구현체가 저장을 못 한다.
        /// </summary>
        void SaveAll();
    }
}
