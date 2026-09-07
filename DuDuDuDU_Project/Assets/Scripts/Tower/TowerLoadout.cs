using System;
using System.Collections.Generic;
using OJ.Core;

namespace OJ.Tower
{
    /// <summary>편성에 들어간 다이스 하나. 성급까지 확정된 상태다(기획서 3.2 "확정 시점").</summary>
    public struct TowerLoadoutEntry : IEquatable<TowerLoadoutEntry>
    {
        public DiceType DiceType;

        /// <summary>기본 다이스면 1~4, 스페셜·신화면 항상 1.</summary>
        public int Star;

        public TowerLoadoutEntry(DiceType diceType, int star)
        {
            DiceType = diceType;
            Star = TowerLoadoutRules.NormalizeStar(diceType, star);
        }

        public bool Equals(TowerLoadoutEntry other)
        {
            return DiceType == other.DiceType && Star == other.Star;
        }

        public override bool Equals(object obj)
        {
            return obj is TowerLoadoutEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)DiceType * 397) ^ Star;
        }

        /// <summary>
        /// 세이브에 나가는 표현. <c>"KingFire:1"</c> 처럼 이름과 성급을 콜론으로 잇는다.
        ///
        /// <b>정수로 저장하지 않는다.</b> <c>DiceType</c> 은 0/100/200 구간으로 나뉜
        /// enum 이라 값을 끼워 넣으면 저장된 정수가 조용히 다른 다이스를 가리킨다 —
        /// <see cref="OJ.Core.SaveState"/> 주석이 그 사고 사례를 적어 두고 있다.
        /// </summary>
        public string ToToken()
        {
            return DiceType + ":" + Star;
        }

        /// <summary>
        /// 토큰을 되읽는다. 이름이 없어졌거나 형태가 틀리면 false —
        /// <b>버리는 것이 맞다.</b> 옛 세이브에 사라진 다이스가 있으면 그 칸만 비고,
        /// 나머지 편성은 그대로 복원된다.
        /// </summary>
        public static bool TryParse(string token, out TowerLoadoutEntry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            int separator = token.IndexOf(':');
            if (separator <= 0 || separator >= token.Length - 1)
                return false;

            string namePart = token.Substring(0, separator);
            string starPart = token.Substring(separator + 1);

            if (!Enum.TryParse(namePart, false, out DiceType diceType) || diceType == DiceType.Max)
                return false;

            if (!int.TryParse(starPart, out int star))
                return false;

            entry = new TowerLoadoutEntry(diceType, star);
            return true;
        }
    }

    /// <summary>
    /// 7칸 편성. 기획서 4장의 규칙을 담는 유일한 그릇이다.
    ///
    /// <b>슬롯을 배열이 아니라 계층별 목록으로 들고 있다.</b> 화면(5.3)은 슬롯 바를
    /// 일곱 칸으로 보여 주지만, 규칙은 칸 위치가 아니라 <b>계층과 성급</b>에 걸려 있다.
    /// 배열 인덱스로 규칙을 표현하면 "3번 칸은 2성 기본" 같은 암묵적 약속이 생기고,
    /// 그 약속을 아는 코드가 화면·저장·전투에 흩어진다.
    ///
    /// <b>순서를 유지한다.</b> 화면에 뜨는 순서(신화 → 스페셜 → 4성 → 1성)가 고정이어야
    /// 편성을 바꿔도 칸이 튀지 않는다.
    /// </summary>
    public sealed class TowerLoadout
    {
        /// <summary>신화 칸. 최대 <see cref="TowerFormula.MythicSlotCount"/> 개.</summary>
        private readonly List<TowerLoadoutEntry> mythics = new List<TowerLoadoutEntry>();

        /// <summary>스페셜 칸. 최대 <see cref="TowerFormula.SpecialSlotCount"/> 개.</summary>
        private readonly List<TowerLoadoutEntry> specials = new List<TowerLoadoutEntry>();

        /// <summary>
        /// 성급 → 기본 다이스. 색인 0 이 1성이다.
        /// <c>DiceType.Max</c> 는 "그 성급 칸이 비었다" 는 뜻이다.
        /// </summary>
        private readonly DiceType[] baseByStar = new DiceType[TowerFormula.BaseSlotCount];

        public TowerLoadout()
        {
            Clear();
        }

        /// <summary>전부 비운다. 기획서 5.5 "전체 해제".</summary>
        public void Clear()
        {
            mythics.Clear();
            specials.Clear();
            for (int i = 0; i < baseByStar.Length; i++)
                baseByStar[i] = DiceType.Max;
        }

        /// <summary>지금 편성된 개수. 0~7.</summary>
        public int Count
        {
            get
            {
                int count = mythics.Count + specials.Count;
                for (int i = 0; i < baseByStar.Length; i++)
                {
                    if (baseByStar[i] != DiceType.Max)
                        count++;
                }

                return count;
            }
        }

        /// <summary>슬롯이 하나라도 비어 있는가. 기획서 5.5 의 빈 슬롯 안내 조건이다.</summary>
        public bool HasEmptySlot => Count < TowerFormula.TotalSlotCount;

        /// <summary>이 성급 칸에 들어 있는 기본 다이스. 비었으면 <c>DiceType.Max</c>.</summary>
        public DiceType GetBaseAt(int star)
        {
            int index = star - 1;
            if (index < 0 || index >= baseByStar.Length)
                return DiceType.Max;

            return baseByStar[index];
        }

        public IReadOnlyList<TowerLoadoutEntry> Mythics => mythics;
        public IReadOnlyList<TowerLoadoutEntry> Specials => specials;

        /// <summary>이 다이스가 지금 편성돼 있는가. 기본은 성급까지 같아야 한다.</summary>
        public bool Contains(DiceType diceType, int star)
        {
            switch (TowerLoadoutRules.TierOf(diceType))
            {
                case TowerSlotTier.Mythic:
                    return IndexOf(mythics, diceType) >= 0;
                case TowerSlotTier.Special:
                    return IndexOf(specials, diceType) >= 0;
                default:
                    return GetBaseAt(star) == diceType;
            }
        }

        /// <summary>
        /// 이 칸이 꽉 찼는가. 화면이 "한도 도달 — 흐리게" 상태를 그릴 때 쓴다(기획서 5.4).
        ///
        /// <b>이미 고른 것은 한도에 걸리지 않는다.</b> 걸리게 하면 스페셜을 둘 고른 뒤
        /// 그중 하나를 <b>해제하는 것</b>조차 막힌다.
        /// </summary>
        public bool IsTierFull(DiceType diceType, int star)
        {
            if (Contains(diceType, star))
                return false;

            switch (TowerLoadoutRules.TierOf(diceType))
            {
                case TowerSlotTier.Mythic:
                    return mythics.Count >= TowerFormula.MythicSlotCount;
                case TowerSlotTier.Special:
                    return specials.Count >= TowerFormula.SpecialSlotCount;
                default:
                    // 기본 다이스는 성급 칸이 하나뿐이라 언제나 "찬" 상태가 아니다 —
                    // 같은 성급의 다른 다이스를 고르면 그 자리가 교체된다(아래 Toggle).
                    return false;
            }
        }

        /// <summary>
        /// 고르거나 해제한다. 화면의 짧은 탭 하나가 이 함수다(기획서 5.4).
        ///
        /// <b>기본 다이스는 교체된다.</b> 4성 칸에 Fire 가 있는데 Ice 를 누르면
        /// Ice 로 바뀐다 — 먼저 해제하게 만들면 탭이 두 번이 되고, 4.1 이 정한
        /// "성급마다 1개" 규칙에서는 그 두 번이 언제나 같은 결과로 이어진다.
        ///
        /// <b>스페셜·신화는 한도에서 멈춘다.</b> 여기서 가장 오래된 것을 밀어내면
        /// 무엇이 빠졌는지 모른 채 편성이 바뀐다. 화면이 흐리게 표시하고 토스트를 띄운다.
        /// </summary>
        /// <returns>편성이 실제로 바뀌었으면 true.</returns>
        public bool Toggle(DiceType diceType, int star)
        {
            int normalizedStar = TowerLoadoutRules.NormalizeStar(diceType, star);

            switch (TowerLoadoutRules.TierOf(diceType))
            {
                case TowerSlotTier.Mythic:
                    return ToggleInList(mythics, diceType, normalizedStar, TowerFormula.MythicSlotCount);

                case TowerSlotTier.Special:
                    return ToggleInList(specials, diceType, normalizedStar, TowerFormula.SpecialSlotCount);

                default:
                    int index = normalizedStar - 1;
                    if (index < 0 || index >= baseByStar.Length)
                        return false;

                    baseByStar[index] = baseByStar[index] == diceType ? DiceType.Max : diceType;
                    return true;
            }
        }

        private static bool ToggleInList(List<TowerLoadoutEntry> list, DiceType diceType, int star, int capacity)
        {
            int existing = IndexOf(list, diceType);
            if (existing >= 0)
            {
                list.RemoveAt(existing);
                return true;
            }

            if (list.Count >= capacity)
                return false;

            list.Add(new TowerLoadoutEntry(diceType, star));
            return true;
        }

        private static int IndexOf(List<TowerLoadoutEntry> list, DiceType diceType)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].DiceType == diceType)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 출전 순서대로 늘어놓는다. 화면의 슬롯 바와 보드 배치가 이 순서를 쓴다.
        ///
        /// 신화 → 스페셜 → 4성 → 3성 → 2성 → 1성. 기획서 4.1 표의 순서 그대로이고,
        /// 5.3 목업의 슬롯 바도 같은 순서다.
        /// </summary>
        public List<TowerLoadoutEntry> ToOrderedList()
        {
            var list = new List<TowerLoadoutEntry>(TowerFormula.TotalSlotCount);
            list.AddRange(mythics);
            list.AddRange(specials);

            for (int star = TowerFormula.MaxBaseStar; star >= 1; star--)
            {
                DiceType diceType = GetBaseAt(star);
                if (diceType != DiceType.Max)
                    list.Add(new TowerLoadoutEntry(diceType, star));
            }

            return list;
        }

        /// <summary>세이브에 나갈 토큰 목록.</summary>
        public List<string> ToTokens()
        {
            List<TowerLoadoutEntry> ordered = ToOrderedList();
            var tokens = new List<string>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
                tokens.Add(ordered[i].ToToken());

            return tokens;
        }

        /// <summary>
        /// 토큰 목록에서 복원한다. <paramref name="isUnlocked"/> 가 false 를 돌려주는
        /// 다이스는 <b>조용히 빠진다</b> — 세이브를 만든 뒤 해금이 취소되는 경로는 지금
        /// 없지만, 있게 되는 날 편성 화면이 못 고르는 다이스를 들고 있게 된다.
        ///
        /// 규칙을 어기는 토큰(스페셜 3개 등)도 여기서 걸린다. <see cref="Toggle"/> 이
        /// 한도를 지키므로 넘치는 것은 그냥 안 들어간다.
        /// </summary>
        public void LoadTokens(IReadOnlyList<string> tokens, Func<DiceType, bool> isUnlocked)
        {
            Clear();
            if (tokens == null)
                return;

            for (int i = 0; i < tokens.Count; i++)
            {
                if (!TowerLoadoutEntry.TryParse(tokens[i], out TowerLoadoutEntry entry))
                    continue;

                if (isUnlocked != null && !isUnlocked(entry.DiceType))
                    continue;

                Toggle(entry.DiceType, entry.Star);
            }
        }

        /// <summary>
        /// 다른 편성의 내용을 그대로 가져온다. 화면이 자기 사본을 들고 편집한 뒤
        /// 확정할 때 쓴다 — 참조를 넘기면 취소가 불가능해진다.
        /// </summary>
        public void CopyFrom(TowerLoadout other)
        {
            Clear();
            if (other == null)
                return;

            for (int i = 0; i < other.mythics.Count; i++)
                mythics.Add(other.mythics[i]);

            for (int i = 0; i < other.specials.Count; i++)
                specials.Add(other.specials[i]);

            for (int i = 0; i < baseByStar.Length; i++)
                baseByStar[i] = other.baseByStar[i];
        }
    }
}
