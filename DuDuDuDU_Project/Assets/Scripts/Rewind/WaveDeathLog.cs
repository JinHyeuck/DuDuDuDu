using System.Collections.Generic;
using UnityEngine;
using OJ.Hunting;
using OJ.Utils;

namespace OJ.Rewind
{
    /// <summary>
    /// 되감을 몬스터 하나의 겉모습과 두 좌표.
    ///
    /// <b>프리팹을 기억하지 않고 스프라이트만 기억한다.</b> 되감기는 <b>죽는 순간의 그림</b>이
    /// 거꾸로 올라가는 연출이라 애니메이션도 로직도 필요 없다. 프리팹을 다시 찍으면
    /// <c>Monster</c> 가 딸려 와서 이동·공격·충돌·처치 카운트를 전부 막아야 하고,
    /// 그 분기는 802줄짜리 클래스 안에 영원히 남는다.
    /// </summary>
    public struct RewindGhostSpec
    {
        public Sprite Sprite;
        public Color Color;
        public bool FlipX;
        public string SortingLayerName;
        public int SortingOrder;

        /// <summary>
        /// 몬스터가 있던 <b>게임오브젝트 레이어</b>. 정렬 레이어와 다른 것이다.
        ///
        /// <b>이걸 안 옮기면 유령이 아예 안 보인다.</b> 새 <c>GameObject</c> 는 레이어 0
        /// (<c>Default</c>)으로 태어나는데, 전투 카메라의 컬링 마스크에 그 레이어가 없다 —
        /// 몬스터는 12(<c>IFF_Foe</c>)에 있다. 화면에 아무것도 안 나오면서 오류도 안 나는,
        /// 원인을 눈으로 못 찾는 종류의 사고다.
        /// </summary>
        public int Layer;

        /// <summary>웨이브 시작으로부터 몇 초에 나왔나(실제 시간). 되감기의 <b>끝</b> 시점.</summary>
        public float SpawnTime;

        /// <summary>웨이브 시작으로부터 몇 초에 죽었나(실제 시간). 되감기의 <b>시작</b> 시점.</summary>
        public float DeathTime;

        /// <summary>죽은 자리. 여기서 출발한다.</summary>
        public Vector3 DeathPosition;

        /// <summary>소환됐던 자리. 여기로 돌아간다.</summary>
        public Vector3 SpawnPosition;

        public Vector3 Scale;
    }

    /// <summary>
    /// 이번 웨이브에 죽은 몬스터의 목록. 되돌리기 연출이 읽는다.
    ///
    /// <b>웨이브가 시작될 때 비운다.</b> 판 전체로 쌓으면 되돌릴 때 지난 웨이브에서 죽은
    /// 것까지 같이 올라간다 — 되감는 것은 <b>이번 웨이브</b>이지 판 전체가 아니다.
    ///
    /// <b>정렬하지 않는다.</b> 죽은 순서대로 들어오므로 목록 자체가 곧 타임라인이고,
    /// 연출은 목록 순서가 아니라 <see cref="RewindGhostSpec.DeathTime"/> 을 보고 되살릴
    /// 때를 정한다 — 순서에 기대지 않으므로 여기서 줄 세울 이유가 없다.
    /// </summary>
    public sealed class WaveDeathLog
    {
        /// <summary>
        /// 기억할 최대 마리 수.
        ///
        /// 한 웨이브의 몬스터 수가 이보다 많아질 일은 지금 없지만(스테이지 데이터 기준),
        /// 탑의 분열이나 나중에 늘어날 웨이브를 생각하면 상한이 없는 목록은 언젠가 샌다.
        /// <b>넘치면 오래된 것부터 버린다</b> — 연출이 먼저 보여 주는 것이 최근에 죽은
        /// 것들이라, 잘려 나가는 쪽은 어차피 화면에 마지막으로 나오는 부분이다.
        /// </summary>
        private const int MaxTracked = 64;

        private readonly List<RewindGhostSpec> deaths = new List<RewindGhostSpec>();

        public int Count => deaths.Count;

        public IReadOnlyList<RewindGhostSpec> Deaths => deaths;

        public void Clear()
        {
            deaths.Clear();
        }

        /// <summary>
        /// 죽은 개체를 적는다. <c>Monster.TakeDamage</c> 가 <b>풀에 돌려보내기 전에</b> 부른다.
        ///
        /// 스프라이트를 못 읽으면 적지 않는다. 그림 없는 유령은 화면에서 아무것도 아니고,
        /// 자리만 차지해 연출 예산을 나눠 가진다.
        /// </summary>
        /// <param name="spawnTime">웨이브 시작으로부터 몇 초에 나왔나.</param>
        /// <param name="deathTime">웨이브 시작으로부터 몇 초에 죽었나.</param>
        public void Record(Monster monster, Vector3 deathPosition, float spawnTime, float deathTime)
        {
            if (monster == null)
                return;

            CharacterAnimation animation = monster.characterAnimation;
            SpriteRenderer renderer = animation != null ? animation.spriteRenderer : null;
            if (renderer == null || renderer.sprite == null)
                return;

            if (deaths.Count >= MaxTracked)
                deaths.RemoveAt(0);

            deaths.Add(new RewindGhostSpec
            {
                Sprite = renderer.sprite,
                Color = renderer.color,
                FlipX = renderer.flipX,
                SortingLayerName = renderer.sortingLayerName,
                SortingOrder = renderer.sortingOrder,

                // 그림을 그리는 <b>그 오브젝트</b>의 레이어를 가져온다. 몬스터 루트가 아니라
                // 렌더러 쪽인 이유는, 카메라가 보는 것이 렌더러가 붙은 오브젝트라서다.
                Layer = renderer.gameObject.layer,

                SpawnTime = spawnTime,
                DeathTime = deathTime,

                DeathPosition = deathPosition,

                // <b>z 는 죽은 자리에서 가져온다.</b> SpawnPosition 은 Vector2 라
                // 그대로 넣으면 z 가 0 이 되고, 유령이 올라가면서 다른 스프라이트
                // 앞뒤로 통과하듯 렌더 순서가 뒤집힌다.
                SpawnPosition = new Vector3(
                    monster.SpawnPosition.x, monster.SpawnPosition.y, deathPosition.z),
                Scale = monster.transform.localScale,
            });
        }
    }
}
