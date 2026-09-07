using System.Collections.Generic;
using UnityEngine;

namespace OJ.Rewind
{
    /// <summary>
    /// 되감기 연출에 쓰는 <b>그림뿐인 몬스터</b>의 풀.
    ///
    /// <b>프리팹이 없다.</b> 만드는 것은 <c>SpriteRenderer</c> 하나짜리 빈 오브젝트이고,
    /// 겉모습은 죽을 때 적어 둔 <see cref="RewindGhostSpec"/> 에서 통째로 복사한다.
    /// 그래서 몬스터 종류가 늘어도 이 파일은 안 바뀌고, <c>Monster</c> 의 로직이
    /// 한 줄도 딸려 오지 않는다.
    ///
    /// <b>루트를 코드로 만든다.</b> 씬에 놓으려면 <c>BattleScene.unity</c> 를 편집해야
    /// 하고(AGENTS 절대 규칙 3), 만들어 둔 오브젝트는 씬이 내려갈 때 같이 사라진다.
    /// </summary>
    public sealed class RewindGhostPool
    {
        private const string RootName = "__WaveRewindGhosts";

        private Transform root;
        private readonly Stack<SpriteRenderer> idle = new Stack<SpriteRenderer>();
        private readonly List<SpriteRenderer> live = new List<SpriteRenderer>();

        /// <summary>지금 화면에 나와 있는 유령들. 연출이 매 프레임 옮긴다.</summary>
        public IReadOnlyList<SpriteRenderer> Live => live;

        /// <summary>유령 하나를 꺼내 <paramref name="spec"/> 의 모습으로 세운다.</summary>
        public SpriteRenderer Take(in RewindGhostSpec spec)
        {
            SpriteRenderer ghost = Rent();

            ghost.sprite = spec.Sprite;
            ghost.color = spec.Color;
            ghost.flipX = spec.FlipX;
            ghost.sortingLayerName = spec.SortingLayerName;
            ghost.sortingOrder = spec.SortingOrder;

            // <b>정렬 레이어만으로는 안 보인다.</b> 카메라는 먼저 컬링 마스크로 오브젝트를
            // 거르고, 새로 만든 오브젝트는 레이어 0(Default)인데 전투 카메라는 그 레이어를
            // 안 본다. 정렬은 "거른 뒤 무엇을 앞에 그릴까" 라 순서가 다르다.
            ghost.gameObject.layer = spec.Layer;

            Transform t = ghost.transform;
            t.position = spec.DeathPosition;
            t.localScale = spec.Scale;
            t.rotation = Quaternion.identity;

            ghost.gameObject.SetActive(true);
            live.Add(ghost);
            return ghost;
        }

        private SpriteRenderer Rent()
        {
            // 파괴된 것은 버린다. 씬이 내려갔다 다시 올라온 경우에 큐에 가짜 null 이
            // 남아 있을 수 있는데, 그것을 그대로 쓰면 만지는 순간 터진다.
            while (idle.Count > 0)
            {
                SpriteRenderer pooled = idle.Pop();
                if (pooled != null)
                    return pooled;
            }

            EnsureRoot();

            var go = new GameObject("RewindGhost");
            go.transform.SetParent(root, false);
            return go.AddComponent<SpriteRenderer>();
        }

        private void EnsureRoot()
        {
            if (root != null)
                return;

            root = new GameObject(RootName).transform;
        }

        /// <summary>연출이 끝났다. 전부 거둔다.</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < live.Count; i++)
            {
                SpriteRenderer ghost = live[i];
                if (ghost == null)
                    continue;

                ghost.sprite = null;
                ghost.gameObject.SetActive(false);
                idle.Push(ghost);
            }

            live.Clear();
        }
    }
}
