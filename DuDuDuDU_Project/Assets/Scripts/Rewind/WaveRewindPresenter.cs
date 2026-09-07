using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using OJ.DI;
using OJ.Hunting;
using OJ.Utils;

namespace OJ.Rewind
{
    /// <summary>
    /// 되감기 연출. <b>웨이브의 타임라인을 거꾸로 튼다.</b>
    ///
    /// <b>연출이 아니라 역재생이다.</b> 처음에는 "죽은 순서대로 위로 올린다" 로 만들었는데,
    /// 그건 정해진 예산(1.2초) 안에 전부 밀어 올리는 <b>퇴장 연출</b>이라 되감기로 안 읽혔다.
    /// 지금은 커서 하나가 웨이브 끝에서 0 으로 흐르고, 각 몬스터는 <b>자기가 살아 있던
    /// 구간에서만</b> 화면에 있다:
    ///
    /// <list type="bullet">
    /// <item>커서가 죽은 시각을 지나면 <b>죽은 자리에서 되살아난다</b></item>
    /// <item>커서가 흐르는 동안 <b>나왔던 자리 쪽으로 거슬러 올라간다</b></item>
    /// <item>커서가 나온 시각 밑으로 가면 <b>소환이 취소되듯 사라진다</b></item>
    /// </list>
    ///
    /// 그래서 순서를 따로 매길 필요가 없다. 마지막에 죽은 것이 먼저 살아나는 것은
    /// 그 시각이 가장 크기 때문이지 우리가 줄을 세워서가 아니다.
    ///
    /// <b>등속이다.</b> 감속 곡선을 넣었더니 마리마다 다른 속도로 움직여서, 되감기가 아니라
    /// 저마다 날아가는 것으로 보였다. 되감기는 <b>시간</b>이 일정하게 흐르는 그림이라
    /// 위치 보간도 선형이어야 한다.
    ///
    /// <b>길이는 밖에서 정해 온다</b>(<see cref="OJ.Core.WaveRewindFormula.Duration"/>).
    /// 웨이브가 길수록 길어지되 상한이 있어서, 긴 웨이브는 <b>더 빨리</b> 감긴다.
    /// 여기서는 그 시간 안에 커서를 웨이브 전체에 걸쳐 흘리는 일만 한다 —
    /// 되감는 구간은 언제나 웨이브 전체이고, 달라지는 것은 걸리는 시간뿐이다.
    ///
    /// <b>탄환·이펙트·데미지 텍스트는 되감지 않는다.</b> 그것들은 연출 전에 이미 거둬졌다
    /// (<c>WaveRewindManager.Restore</c>). 날아가던 화살까지 거꾸로 빨려 들어가면 화면이
    /// 읽히지 않고, 무엇보다 유저가 되돌리려는 대상이 아니다.
    ///
    /// <b>전부 unscaled 로 돈다.</b> 이 연출은 <c>Time.timeScale == 0</c> 인 채로 시작한다.
    /// </summary>
    public sealed class WaveRewindPresenter
    {
        /// <summary>
        /// 되감기가 이보다 짧으면 아예 돌리지 않는다.
        ///
        /// 웨이브를 시작하자마자 되돌린 경우다. 몇 프레임짜리 연출은 화면에서 깜빡임으로만
        /// 보이고, 그 깜빡임은 되감기가 아니라 고장으로 읽힌다.
        /// </summary>
        private const float MinimumDuration = 0.15f;

        private readonly IBattleRefs battle;
        private readonly RewindGhostPool ghosts = new RewindGhostPool();
        private readonly List<Actor> actors = new List<Actor>();

        /// <summary>
        /// 되감기는 것 하나. 살아 있던 몬스터와 죽은 유령이 <b>같은 타입</b>인 것이 요점이다 —
        /// 둘을 따로 굴리면 같은 보간을 두 벌 쓰게 되고, 한쪽만 고치는 사고가 난다.
        ///
        /// 둘의 차이는 <see cref="EndTime"/> 뿐이다. 살아 있던 것은 그 값이 "지금" 이라
        /// 커서가 출발하는 순간부터 보이고, 죽은 것은 죽은 시각이라 커서가 거기 닿아야 보인다.
        /// </summary>
        private struct Actor
        {
            public Transform Target;
            public SpriteRenderer Renderer;

            /// <summary>나온 시각·자리. 되감기가 여기서 끝난다.</summary>
            public float SpawnTime;
            public Vector3 SpawnPosition;

            /// <summary>죽은(또는 지금) 시각·자리. 되감기가 여기서 시작한다.</summary>
            public float EndTime;
            public Vector3 EndPosition;

            /// <summary>살아 있던 몬스터라면 채워진다. 유령이면 null 이다.</summary>
            public Monster LiveMonster;
        }

        public WaveRewindPresenter(IBattleRefs battle)
        {
            this.battle = battle;
        }

        /// <summary>
        /// 되감기를 보여 준다. 끝나면 화면에는 아무것도 남지 않고 살아 있던 몬스터는
        /// 전부 풀로 돌아가 있다.
        /// </summary>
        /// <param name="log">이번 웨이브에 죽은 것들.</param>
        /// <param name="waveElapsed">웨이브를 실제로 본 시간(초).</param>
        /// <param name="duration">되감기에 쓸 실제 시간(초).
        ///   <c>WaveRewindFormula.Duration</c> 이 배속과 상한까지 반영해 준 값이다.</param>
        /// <param name="wallHpTo">벽이 차오를 목표치.</param>
        public async UniTask PlayAsync(
            WaveDeathLog log, float waveElapsed, float duration, int wallHpTo)
        {
            CollectLiveMonsters(waveElapsed);
            CollectGhosts(log);

            Wall wall = battle.Game != null ? battle.Game.wall : null;
            int wallHpFrom = wall != null ? wall.CurrentHp : wallHpTo;

            if (duration < MinimumDuration)
            {
                Cleanup();
                return;
            }

            // 연출 도중의 입력을 막는다. 지금 판은 InGameState.None 이라 소환·머지가
            // <b>열려 있고</b>(그 둘은 Wave 만 막는다), 여기서 다이스를 옮기면
            // 곧바로 스냅샷이 덮어써 <b>유저가 한 조작이 소리 없이 사라진다</b>.
            EventSystem events = EventSystem.current;
            bool eventsWereEnabled = events != null && events.enabled;
            if (eventsWereEnabled)
                events.enabled = false;

            try
            {
                await RunAsync(wall, waveElapsed, duration, wallHpFrom, wallHpTo);
            }
            finally
            {
                // finally 인 것이 중요하다. 여기서 새면 <b>게임 전체의 입력이 영영 죽는다</b> —
                // 되돌리기 한 번 쓰고 아무 버튼도 안 눌리는 상태가 되고, 원인은 화면에 없다.
                if (eventsWereEnabled && events != null)
                    events.enabled = true;

                Cleanup();
            }
        }

        private async UniTask RunAsync(
            Wall wall, float waveElapsed, float duration, int wallHpFrom, int wallHpTo)
        {
            // 첫 프레임을 먼저 맞춘다. 이걸 빼면 유령이 전부 켜진 채로 한 프레임 보였다가
            // 사라진다 — 되감기가 시작도 하기 전에 답을 보여 주는 셈이다.
            StepActors(waveElapsed);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                // 기다리는 사이에 씬이 내려갔을 수 있다. 그러면 아래에서 만지는 것이
                // 전부 가짜 null 이 되므로 즉시 나간다.
                if (battle == null || !battle.IsActive)
                    return;

                elapsed += Time.unscaledDeltaTime;

                // 커서는 웨이브 끝에서 0 을 향해 흐른다. 이 한 줄이 "거꾸로 감는다" 의 전부다.
                float cursor = Mathf.Max(0f, waveElapsed - elapsed * (waveElapsed / duration));

                StepActors(cursor);

                // 벽도 같은 커서를 탄다. 커서가 웨이브 끝이면 지금 체력, 0 이면 시작 체력이다.
                if (wall != null)
                {
                    float t = waveElapsed > 0f ? cursor / waveElapsed : 0f;
                    wall.RestoreHp(Mathf.RoundToInt(Mathf.Lerp(wallHpTo, wallHpFrom, t)));
                }
            }

            // 마지막을 정확한 값으로 못박는다. 루프는 duration 을 지나 끝나므로
            // 커서가 0 에 아주 가깝긴 해도 정확히 0 이라는 보장이 없다.
            StepActors(0f);
            if (wall != null)
                wall.RestoreHp(wallHpTo);
        }

        /// <summary>
        /// 커서 위치에 맞춰 전부 다시 그린다.
        ///
        /// <b>보이는 조건이 곧 타임라인이다.</b> 몬스터는 <c>[SpawnTime, EndTime]</c> 동안
        /// 존재했으므로, 커서가 그 구간 안에 있을 때만 화면에 있다. 죽은 것이 되살아나는 것도
        /// 사라지는 것도 이 한 줄에서 나온다 — 따로 등장·퇴장 처리를 두지 않는다.
        /// </summary>
        private void StepActors(float cursor)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                Actor actor = actors[i];
                if (actor.Target == null)
                    continue;

                bool visible = cursor <= actor.EndTime && cursor >= actor.SpawnTime;

                if (actor.Renderer != null)
                    actor.Renderer.enabled = visible;

                if (!visible)
                    continue;

                // 나온 자리와 죽은 자리 사이를 선형으로 되짚는다. 몬스터는 위에서 아래로
                // 일정 속도로 내려오므로 이 직선이 실제 경로와 거의 같다. 경로를 프레임마다
                // 기록해 두는 방법도 있지만, 마리당 수백 개의 좌표를 웨이브 내내 들고 있게 된다.
                float span = actor.EndTime - actor.SpawnTime;
                float t = span > 0.0001f
                    ? Mathf.Clamp01((cursor - actor.SpawnTime) / span)
                    : 1f;

                actor.Target.position = Vector3.Lerp(actor.SpawnPosition, actor.EndPosition, t);
            }
        }

        /// <summary>
        /// 아직 살아 있던 몬스터를 액터로 만든다. 이것들의 <c>EndTime</c> 은 "지금" 이라
        /// 커서가 출발하는 순간부터 보이고, 곧바로 거슬러 올라가기 시작한다.
        ///
        /// <b><c>enabled = false</c> 가 이 함수의 핵심이다.</b> 컴포넌트를 끄면 이동·공격이
        /// 멈추는 동시에 <c>Monster.OnDisable</c> 이 돌아 <c>UnregisterMonster</c> 까지
        /// 해 준다 — 마침 필요한 일이고, <c>countAsKill:false</c> 라 처치 수도 안 는다.
        /// <b>오브젝트는 켜 둔 채</b>라 그림은 그대로 남아 되감을 수 있다.
        /// </summary>
        private void CollectLiveMonsters(float waveElapsed)
        {
            List<Monster> active = battle.Monsters != null ? battle.Monsters.activeMonsters : null;
            if (active == null || active.Count == 0)
                return;

            // 배열로 복사한 뒤 돈다. 아래 enabled = false 가 OnDisable 을 통해
            // 지금 순회 중인 그 목록을 고친다.
            Monster[] snapshot = active.ToArray();
            float now = Time.unscaledTime;

            for (int i = 0; i < snapshot.Length; i++)
            {
                Monster monster = snapshot[i];
                if (monster == null)
                    continue;

                CharacterAnimation animation = monster.characterAnimation;
                SpriteRenderer renderer = animation != null ? animation.spriteRenderer : null;

                monster.enabled = false;

                Vector3 endPosition = monster.transform.position;

                actors.Add(new Actor
                {
                    Target = monster.transform,
                    Renderer = renderer,

                    // 웨이브 시작보다 앞설 수는 없다. 분열 자식처럼 도중에 나온 것은
                    // 그 시각이 그대로 들어가고, 그래서 먼저 사라진다.
                    SpawnTime = Mathf.Clamp(waveElapsed - (now - monster.SpawnRealTime), 0f, waveElapsed),

                    // z 는 지금 값을 그대로 쓴다. SpawnPosition 이 Vector2 라 그대로
                    // 넣으면 되감기는 동안 z 가 0 으로 끌려가 렌더 순서가 뒤집힌다.
                    SpawnPosition = new Vector3(
                        monster.SpawnPosition.x, monster.SpawnPosition.y, endPosition.z),

                    EndTime = waveElapsed,
                    EndPosition = endPosition,
                    LiveMonster = monster,
                });
            }
        }

        /// <summary>
        /// 죽은 것들을 유령으로 세운다. <b>줄을 세우지 않는다</b> — 되살아나는 순서는
        /// 각자의 <c>DeathTime</c> 이 정하므로 목록 순서는 아무 뜻이 없다.
        /// </summary>
        private void CollectGhosts(WaveDeathLog log)
        {
            if (log == null || log.Count == 0)
                return;

            IReadOnlyList<RewindGhostSpec> deaths = log.Deaths;
            for (int i = 0; i < deaths.Count; i++)
            {
                RewindGhostSpec spec = deaths[i];
                SpriteRenderer ghost = ghosts.Take(spec);

                // 커서가 죽은 시각에 닿기 전에는 없는 것이다. StepActors 가 곧 켜 준다.
                ghost.enabled = false;

                actors.Add(new Actor
                {
                    Target = ghost.transform,
                    Renderer = ghost,
                    SpawnTime = spec.SpawnTime,
                    SpawnPosition = spec.SpawnPosition,
                    EndTime = spec.DeathTime,
                    EndPosition = spec.DeathPosition,
                    LiveMonster = null,
                });
            }
        }

        private void Cleanup()
        {
            for (int i = 0; i < actors.Count; i++)
            {
                Actor actor = actors[i];
                Monster monster = actor.LiveMonster;
                if (monster == null)
                    continue;

                // 렌더러를 다시 켠다. 연출이 끈 채로 풀에 넣으면 <b>다음 웨이브에
                // 보이지 않는 몬스터가 나온다</b> — 때리지도 못하고 벽만 깎는다.
                if (actor.Renderer != null)
                    actor.Renderer.enabled = true;

                // <b>순서가 있다.</b> 먼저 풀에 넣어 오브젝트를 끄고, 꺼진 뒤에 컴포넌트를
                // 켠다. 반대로 하면 켜진 오브젝트에서 Monster 가 한 프레임 살아나
                // 소환 위치에서 다시 걸어 내려온다.
                if (battle.Spawner != null)
                    battle.Spawner.PoolMonster(monster);

                monster.enabled = true;
            }

            actors.Clear();
            ghosts.ReleaseAll();
        }
    }
}
