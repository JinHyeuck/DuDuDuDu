using System;
using System.Collections.Generic;

namespace OJ.Core
{
    /// <summary>
    /// 스테이지 클리어로 여는 로비 컨텐츠. (Docs/ContentUnlock.md)
    ///
    /// <b>이름을 세이브에 쓴다</b>(<c>ContentUnlockSave.Announced</c>). 결정 2 로 enum 리네임
    /// 금지는 풀렸지만, 이름을 바꾸면 그 컨텐츠의 연출이 이미 본 사람에게 다시 뜬다.
    /// </summary>
    public enum ContentId
    {
        Dice = 0,
        Equipment,
        StageReward,
        IdleReward,
        StageStar,
        MeatFestival,
        Sweep,
        SpecialSevenDays,
        Quest,
        Pinball,
        Shop,
        Membership,
        SeasonPass,
        Relic,
        Tower,
    }

    /// <summary>열렸을 때 유저에게 어떻게 알리는가.</summary>
    public enum UnlockPresentation
    {
        /// <summary>
        /// 아무 연출 없이 입구만 나타난다. 알리는 것은 입구 자신의 레드닷이다
        /// ("받기" 버튼 하나뿐이라 설명이 필요 없는 것).
        /// </summary>
        Quiet = 0,

        /// <summary>해금 연출: 입구가 튀어나오고 딤드 + 말풍선으로 무엇인지 한 번 알린다.</summary>
        Announce,

        /// <summary>해금 연출 뒤 입구를 직접 누르게 하고, 열린 화면을 설명한다.</summary>
        Guide,
    }

    /// <summary>
    /// 컨텐츠 하나의 해금 조건과 알리는 방법.
    ///
    /// <b>Core 에 두는 이유.</b> 기본표·검사·대기열 규칙을 헤드리스 러너에서 돌리려면
    /// SO 밖에 있어야 한다(SO 는 거기서 못 만든다). SO(<c>ContentUnlockDatabase</c>)는
    /// 이 클래스의 목록을 직렬화해 들기만 한다.
    /// </summary>
    [Serializable]
    public sealed class ContentUnlockDefinition
    {
        public ContentId contentId;

        /// <summary>이 스테이지를 클리어하면 열린다. 0 이면 처음부터(튜토리얼 직후) 열려 있다.</summary>
        public int requiredClear;

        public UnlockPresentation presentation;

        /// <summary>해금 연출과 잠긴 탭 안내에 쓰는 이름.</summary>
        public string displayName = string.Empty;

        /// <summary>해금 연출 말풍선의 둘째 줄 — 무엇을 하는 곳인지 한 문장.</summary>
        public string description = string.Empty;

        /// <summary>가이드: 입구를 가리키며 하는 말. <see cref="UnlockPresentation.Guide"/> 일 때만 쓴다.</summary>
        public string guideTapText = string.Empty;

        /// <summary>가이드: 입구를 누른 뒤 열린 화면 위에서 하는 말. 비면 누르는 것으로 끝난다.</summary>
        public string guideDetailText = string.Empty;

        public ContentUnlockDefinition()
        {
        }

        public ContentUnlockDefinition(
            ContentId contentId, int requiredClear, UnlockPresentation presentation,
            string displayName, string description,
            string guideTapText = "", string guideDetailText = "")
        {
            this.contentId = contentId;
            this.requiredClear = requiredClear;
            this.presentation = presentation;
            this.displayName = displayName ?? string.Empty;
            this.description = description ?? string.Empty;
            this.guideTapText = guideTapText ?? string.Empty;
            this.guideDetailText = guideDetailText ?? string.Empty;
        }
    }

    /// <summary>
    /// 컨텐츠 해금의 규칙. 정본 수치는 SO 이고, 여기는 판정과 기본표다. (Docs/ContentUnlock.md)
    ///
    /// <b>열림은 저장하지 않는다.</b> "마지막으로 클리어한 스테이지 ≥ 요구 클리어" 로 매번 계산한다.
    /// 열림까지 세이브에 적으면 요구 스테이지를 고쳤을 때 이미 연 사람과 새로 여는 사람이 갈린다.
    /// 저장하는 것은 <b>연출을 봤는가</b> 하나다.
    /// </summary>
    public static class ContentUnlockRules
    {
        public static bool IsOpen(ContentUnlockDefinition definition, int lastClearedStage)
        {
            return definition != null && lastClearedStage >= definition.requiredClear;
        }

        /// <summary>
        /// 입구를 보이는가. 열렸어도 <b>연출을 아직 안 봤으면 숨긴다</b> — 연출이 입구를
        /// 튀어나오게 하는데, 이미 보이고 있으면 무엇이 새로 생겼는지 알 수 없다.
        /// </summary>
        public static bool IsVisible(ContentUnlockDefinition definition, int lastClearedStage, ICollection<ContentId> announced)
        {
            if (!IsOpen(definition, lastClearedStage))
                return false;

            return definition.presentation == UnlockPresentation.Quiet || announced.Contains(definition.contentId);
        }

        /// <summary>열렸는데 연출을 아직 안 본 것이 있는가.</summary>
        public static bool HasPending(IReadOnlyList<ContentUnlockDefinition> definitions, int lastClearedStage, ICollection<ContentId> announced)
        {
            return VisitQueue(definitions, lastClearedStage, announced).Count > 0;
        }

        /// <summary>
        /// 이번 로비 진입에서 보여 줄 연출, 표 순서대로.
        ///
        /// <b>가이드 하나에서 끊는다(그 가이드는 포함).</b> 한 번 들어온 로비에서 가이드가 둘이면
        /// 유저는 마지막 것만 기억한다. 뒤에 남은 것은 다음 진입에 이어서 나온다 — 그래서 같은
        /// 클리어에 걸린 것끼리는 <b>표에서 연출을 가이드보다 앞에</b> 둔다.
        /// </summary>
        public static List<ContentUnlockDefinition> VisitQueue(
            IReadOnlyList<ContentUnlockDefinition> definitions, int lastClearedStage, ICollection<ContentId> announced)
        {
            var queue = new List<ContentUnlockDefinition>();
            if (definitions == null)
                return queue;

            for (int i = 0; i < definitions.Count; i++)
            {
                ContentUnlockDefinition d = definitions[i];
                if (d == null || d.presentation == UnlockPresentation.Quiet)
                    continue;

                if (!IsOpen(d, lastClearedStage) || announced.Contains(d.contentId))
                    continue;

                queue.Add(d);
                if (d.presentation == UnlockPresentation.Guide)
                    break;
            }

            return queue;
        }

        /// <summary>
        /// 연출 기록이 없는 세이브를 "이미 본 것" 으로 채워야 하는가.
        ///
        /// 이 기능보다 먼저 생긴 세이브에는 기록이 없다. 그대로 두면 10스테이지까지 깬 사람에게
        /// 연출 열 개가 한꺼번에 쏟아진다. 반대로 <b>한 판도 안 깬 세이브는 채우지 않는다</b> —
        /// 튜토리얼만 마친 신규 유저라, 다이스 가이드를 받아야 한다.
        /// </summary>
        public static bool ShouldBackfill(bool initialized, int lastClearedStage)
        {
            return !initialized && lastClearedStage >= 1;
        }

        /// <summary>
        /// 세이브의 스테이지 기록에서 마지막으로 클리어한 스테이지를 뽑는다.
        /// <c>StageProgressManager.GetLastClearedStageIndex</c> 와 같은 판정이다(최고 등급이 None 보다 크면 클리어).
        /// 키가 숫자가 아니면 건너뛴다.
        /// </summary>
        public static int LastClearedFromSave(SaveState state)
        {
            int last = 0;
            if (state == null)
                return last;

            foreach (KeyValuePair<string, StageRecordSave> pair in state.Stage.Records)
            {
                if (pair.Value == null || pair.Value.BestClearGrade <= 0)
                    continue;

                if (int.TryParse(pair.Key, out int index))
                    last = OJMath.Max(last, index);
            }

            return last;
        }

        /// <summary>
        /// 표가 성한지 본다. 비어 있으면 문제 없음.
        /// <list type="bullet">
        /// <item>모든 <see cref="ContentId"/> 가 <b>빠짐없이 한 번씩</b> — 빠진 것은 영영 안 열린다.</item>
        /// <item>요구 클리어는 0 이상, <paramref name="stageCount"/> 이하(0 이면 그 검사는 건너뛴다).</item>
        /// <item>이름이 비면 연출과 잠긴 탭 안내가 빈 글자가 된다.</item>
        /// <item>가이드인데 가리키며 할 말이 없으면 무엇을 누르라는지 모른다.</item>
        /// <item>요구 클리어는 표 순서대로 줄지 않아야 한다 — <see cref="VisitQueue"/> 가 표 순서로 끊기 때문이다.</item>
        /// </list>
        /// </summary>
        public static List<string> Validate(IReadOnlyList<ContentUnlockDefinition> definitions, int stageCount)
        {
            var problems = new List<string>();
            if (definitions == null)
            {
                problems.Add("표가 없다.");
                return problems;
            }

            var seen = new HashSet<ContentId>();
            int previousRequired = 0;

            for (int i = 0; i < definitions.Count; i++)
            {
                ContentUnlockDefinition d = definitions[i];
                if (d == null)
                {
                    problems.Add(i + "번 줄이 비었다.");
                    continue;
                }

                string at = d.contentId + "(" + i + "번 줄)";

                if (!seen.Add(d.contentId))
                    problems.Add(at + ": 중복이다.");

                if (d.requiredClear < 0)
                    problems.Add(at + ": 요구 클리어가 음수다.");
                else if (stageCount > 0 && d.requiredClear > stageCount)
                    problems.Add(at + ": 요구 클리어 " + d.requiredClear + " 가 스테이지 수 " + stageCount + " 를 넘어 영영 안 열린다.");

                if (d.requiredClear < previousRequired)
                    problems.Add(at + ": 요구 클리어 " + d.requiredClear + " 가 앞 줄(" + previousRequired + ")보다 작다. 표는 해금 순서대로 적는다.");
                previousRequired = OJMath.Max(previousRequired, d.requiredClear);

                if (string.IsNullOrEmpty(d.displayName))
                    problems.Add(at + ": 이름이 비었다.");

                if (d.presentation == UnlockPresentation.Guide && string.IsNullOrEmpty(d.guideTapText))
                    problems.Add(at + ": 가이드인데 가리키며 할 말(guideTapText)이 없다.");
            }

            foreach (ContentId id in Enum.GetValues(typeof(ContentId)))
            {
                if (!seen.Contains(id))
                    problems.Add(id + ": 표에 없다. 영영 안 열린다.");
            }

            return problems;
        }

        /// <summary>
        /// 기본표. SO 에셋을 처음 만들 때와, 에셋이 없을 때의 폴백이 이것을 쓴다 —
        /// <b>기본값이 두 벌이 되지 않게</b> 한 곳에서만 적는다. (Docs/ContentUnlock.md 의 표와 같다)
        /// </summary>
        public static List<ContentUnlockDefinition> BuildDefaults()
        {
            return new List<ContentUnlockDefinition>
            {
                // 0클리어 — 튜토리얼 직후. 가이드(다이스)에서 끊기므로 고기 축제 가이드는 다음 진입에 나온다.
                new ContentUnlockDefinition(ContentId.Dice, 0, UnlockPresentation.Guide,
                    "다이스", "다이스를 강화하면\n전투에서 더 강해져요.",
                    "튜토리얼에서 얻은 다이스를 보러 가요!",
                    "여기서 다이스를 강화하고\n새 다이스를 열 수 있어요."),
                new ContentUnlockDefinition(ContentId.StageReward, 0, UnlockPresentation.Quiet,
                    "스테이지 보상", "스테이지를 깨면 보상을 받아요."),
                new ContentUnlockDefinition(ContentId.StageStar, 0, UnlockPresentation.Quiet,
                    "별의 축제", "별을 모으면 보상을 받아요."),
                new ContentUnlockDefinition(ContentId.MeatFestival, 0, UnlockPresentation.Guide,
                    "고기 축제", "시간이 지나면 고기가 구워져요.\n눌러서 받아요!",
                    "고기가 구워졌어요! 눌러 보세요.",
                    "고기는 시간이 지나면 쌓여요.\n가득 차기 전에 받아 가요."),

                // 1클리어 — 연출(방치 보상)을 가이드(장비)보다 앞에 둬야 한 번에 나온다.
                new ContentUnlockDefinition(ContentId.IdleReward, 1, UnlockPresentation.Announce,
                    "방치 보상", "게임을 꺼 둬도 보상이 쌓여요.\n자주 받으러 와요!"),
                new ContentUnlockDefinition(ContentId.Equipment, 1, UnlockPresentation.Guide,
                    "장비", "장비를 강화하면 다이스가 더 세져요.",
                    "장비가 열렸어요! 눌러 보세요.",
                    "골드로 장비를 강화하면\n모든 다이스의 공격력이 올라요."),

                // 2클리어
                new ContentUnlockDefinition(ContentId.Pinball, 2, UnlockPresentation.Guide,
                    "핀볼", "티켓으로 핀볼을 굴려\n고기와 보상을 받아요.",
                    "핀볼이 열렸어요! 눌러 보세요.",
                    "티켓으로 공을 쏘면\n맞힌 핀만큼 보상을 받아요."),

                // 3클리어 — 킹 썬더가 열리는 스테이지. 클리어 직후 아랫줄이 봉인되고(TowerSealDirector)
                // 탑 1층을 깨야 풀린다. 연출(퀘스트)을 가이드(탑)보다 앞에 둔다.
                new ContentUnlockDefinition(ContentId.Quest, 3, UnlockPresentation.Announce,
                    "퀘스트", "미션을 깨고 보상을 받아요."),
                new ContentUnlockDefinition(ContentId.Tower, 3, UnlockPresentation.Guide,
                    "무한의 탑", "층을 올라 킹 다이스를 얻어요.\n봉인된 아랫줄도 탑에서 풀어요.",
                    "무한의 탑이 열렸어요! 눌러 보세요.",
                    "1층을 깨면 봉인이 풀려요.\n층을 깰수록 보상이 커지고,\n정해진 층에서 킹 다이스가 열려요."),

                // 4클리어
                new ContentUnlockDefinition(ContentId.Membership, 4, UnlockPresentation.Quiet,
                    "멤버십", "멤버십 혜택을 받아요."),
                new ContentUnlockDefinition(ContentId.SpecialSevenDays, 4, UnlockPresentation.Quiet,
                    "특별한 7일", "7일 동안 매일 보석을 받아요."),
                new ContentUnlockDefinition(ContentId.Sweep, 4, UnlockPresentation.Guide,
                    "소탕", "깬 스테이지의 보상을 바로 받아요.",
                    "소탕이 열렸어요! 눌러 보세요.",
                    "고기를 써서 깬 스테이지 보상을\n전투 없이 받아요."),

                // 5클리어
                new ContentUnlockDefinition(ContentId.SeasonPass, 5, UnlockPresentation.Quiet,
                    "시즌 패스", "고기를 쓰면 패스가 올라\n보상을 받아요."),
                new ContentUnlockDefinition(ContentId.Shop, 5, UnlockPresentation.Guide,
                    "상점", "보석을 뽑아 장비에 끼워요.",
                    "상점이 열렸어요! 눌러 보세요.",
                    "보석을 뽑아서 장비 칸에 끼우면\n능력치가 올라요."),

                // 6클리어
                new ContentUnlockDefinition(ContentId.Relic, 6, UnlockPresentation.Guide,
                    "유물", "유물을 소환해\n전투를 유리하게 만들어요.",
                    "유물이 열렸어요! 눌러 보세요.",
                    "유물은 전투 내내 효과를 줘요.\n소환하고 레벨을 올려 봐요."),
            };
        }
    }
}
