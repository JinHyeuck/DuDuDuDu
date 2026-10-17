using System.Collections.Generic;
using UnityEngine;
using OJ.Core;
using OJ.Dice;
using OJ.Utils;

namespace OJ.Hunting
{
    /// <summary>
    /// 몬스터 머리 위에 <b>상태이상을 건 다이스의 아이콘</b>을 한 줄로 띄운다.
    /// 슬로우면 아이스, 독이면 독, 방깎이면 방깎 다이스의 그림이다 — "무엇이 걸렸나" 를 효과 이름이 아니라
    /// 유저가 이미 아는 다이스 얼굴로 알린다.
    ///
    /// <b>표시일 뿐이다.</b> 효과 판정·만료는 전부 <c>Monster</c> 의 기존 상태가 한다. 여기는 "어느 다이스가 언제까지"
    /// 만 따로 적어 둔다. 같은 다이스가 다시 걸면 새 줄을 만들지 않고 만료 시각만 늘린다(한 종류당 아이콘 하나).
    ///
    /// <b>프리팹을 고치지 않는다</b>(절대 규칙 3) — 처음 필요할 때 자식 오브젝트를 코드로 만든다.
    /// 아이콘은 몬스터의 스케일과 무관하게 같은 월드 크기로 나오도록 부모 스케일을 되돌려 놓는다
    /// (보스·분열 자식이 커도 아이콘이 같이 커지지 않는다). 겹치지 않도록 가로 한 줄로 가운데 정렬한다.
    /// </summary>
    public sealed class MonsterStatusIcons : MonoBehaviour
    {
        private struct Mark
        {
            public DiceType Type;
            public float Until;
        }

        // 월드 단위. 몬스터 한 마리 폭의 1/3 안팎이라 서넛이 붙어도 몸통보다 넓어지지 않는다.
        private const float IconHeight = 0.32f;
        private const float Gap = 0.04f;
        private const float HeadGap = 0f;

        // 스프라이트 정렬은 몬스터 몸통보다 앞에. 같은 레이어·위 순서로 둔다.
        private const int SortingOrderBoost = 50;

        private readonly List<Mark> marks = new List<Mark>();
        private readonly List<SpriteRenderer> icons = new List<SpriteRenderer>();

        // 프레임마다 쓰는 임시 칸. 몬스터가 많아도 새로 할당하지 않는다.
        private const int MaxIcons = 12;
        private readonly float[] widths = new float[MaxIcons];
        private readonly Sprite[] sprites = new Sprite[MaxIcons];

        private Monster owner;
        private Transform root;
        private SpriteRenderer[] bodies;

        private static IClock Clock => UnityClock.Instance;

        /// <summary>몬스터에 붙인다. 몸통 렌더러를 <b>아이콘을 만들기 전에</b> 모아 둔다(우리 것이 섞이지 않게).</summary>
        public static MonsterStatusIcons Attach(Monster monster)
        {
            var go = new GameObject("StatusIcons");
            go.layer = monster.gameObject.layer;
            go.transform.SetParent(monster.transform, false);

            var icons = go.AddComponent<MonsterStatusIcons>();
            icons.owner = monster;
            icons.root = go.transform;
            icons.bodies = monster.GetComponentsInChildren<SpriteRenderer>(true);
            return icons;
        }

        /// <summary>이 다이스가 <paramref name="until"/> (게임 시간)까지 상태이상을 걸고 있다.</summary>
        public void Add(DiceType type, float until)
        {
            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i].Type != type)
                    continue;

                Mark mark = marks[i];
                mark.Until = Mathf.Max(mark.Until, until);
                marks[i] = mark;
                return;
            }

            marks.Add(new Mark { Type = type, Until = until });
        }

        /// <summary>풀로 돌아가거나 죽을 때. 다음 개체에 이전 아이콘이 새지 않게 비운다.</summary>
        public void Clear()
        {
            marks.Clear();
            Hide();
        }

        private void Hide()
        {
            for (int i = 0; i < icons.Count; i++)
                icons[i].gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (marks.Count == 0)
                return;

            float now = Clock.GameTime;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                if (now >= marks[i].Until)
                    marks.RemoveAt(i);
            }

            if (marks.Count == 0)
            {
                Hide();
                return;
            }

            Layout();
        }

        private void Layout()
        {
            // 부모(몬스터) 스케일을 되돌려 아이콘이 항상 같은 월드 크기가 되게 한다.
            Vector3 parentScale = owner.transform.lossyScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
            float sy = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
            root.localScale = new Vector3(1f / sx, 1f / sy, 1f);
            root.rotation = Quaternion.identity;

            float top = owner.transform.position.y;
            SortingInfo sorting = default;
            bool haveSorting = false;

            for (int i = 0; i < bodies.Length; i++)
            {
                SpriteRenderer body = bodies[i];
                if (body == null || !body.enabled || !body.gameObject.activeInHierarchy || body.sprite == null)
                    continue;

                top = Mathf.Max(top, body.bounds.max.y);

                if (!haveSorting)
                {
                    sorting = new SortingInfo { LayerId = body.sortingLayerID, Order = body.sortingOrder };
                    haveSorting = true;
                }
                else if (body.sortingOrder > sorting.Order)
                {
                    sorting.Order = body.sortingOrder;
                }
            }

            root.position = new Vector3(owner.transform.position.x, top + HeadGap + IconHeight * 0.5f, owner.transform.position.z);

            // 아이콘 크기와 전체 폭을 먼저 잰다.
            int count = Mathf.Min(marks.Count, MaxIcons);
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = DiceMetaDataProvider.GetIcon(marks[i].Type);
                sprites[i] = sprite;
                float width = 0f;
                if (sprite != null && sprite.bounds.size.y > 0.0001f)
                    width = sprite.bounds.size.x * (IconHeight / sprite.bounds.size.y);

                widths[i] = width;
                total += width;
            }

            total += Gap * Mathf.Max(0, count - 1);

            float x = -total * 0.5f;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = GetIcon(i);
                if (sprites[i] == null)
                {
                    renderer.gameObject.SetActive(false);
                    continue;
                }

                renderer.gameObject.SetActive(true);
                renderer.sprite = sprites[i];
                float scale = IconHeight / sprites[i].bounds.size.y;
                renderer.transform.localScale = new Vector3(scale, scale, 1f);
                renderer.transform.localPosition = new Vector3(x + widths[i] * 0.5f, 0f, 0f);
                x += widths[i] + Gap;

                if (haveSorting)
                    renderer.sortingLayerID = sorting.LayerId;
                renderer.sortingOrder = sorting.Order + SortingOrderBoost;
            }

            for (int i = count; i < icons.Count; i++)
                icons[i].gameObject.SetActive(false);
        }

        private struct SortingInfo
        {
            public int LayerId;
            public int Order;
        }

        private SpriteRenderer GetIcon(int index)
        {
            while (icons.Count <= index)
            {
                var go = new GameObject("Icon" + icons.Count);
                go.layer = gameObject.layer;
                go.transform.SetParent(root, false);
                icons.Add(go.AddComponent<SpriteRenderer>());
            }

            return icons[index];
        }
    }
}
