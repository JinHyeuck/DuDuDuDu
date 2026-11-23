using System;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 이벤트 화면이 쓰는 "지금". 평소에는 <see cref="DateTime.UtcNow"/> 와 같다.
    /// <see cref="DevOffset"/> 은 <b>개발 확인 전용</b>이다 — 이벤트가 3일마다 도는 탓에 오늘 열리지 않은 이벤트를
    /// 플레이해 볼 수 없어서, 시계를 앞으로 밀어 그 이벤트가 열린 날로 간다. 저장하지 않으므로 앱을 다시 켜면 0 이다.
    /// (핀볼이 이벤트 재화를 주는 쪽은 별도로 실제 시각을 본다 — 재화는 <c>PointManager.Add</c> 로 직접 넣어 확인할 것.)
    /// </summary>
    public static class EventMinigameClock
    {
        public static TimeSpan DevOffset;

        public static DateTime UtcNow
        {
            get { return DateTime.UtcNow + DevOffset; }
        }
    }
}
