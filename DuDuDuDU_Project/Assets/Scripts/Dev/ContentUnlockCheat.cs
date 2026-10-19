#if UNITY_EDITOR || DEV_DEFINE
using System;
using System.Collections.Generic;
using UnityEngine;
using OJ.ContentUnlock;
using OJ.Core;
using OJ.Dice;
using OJ.Point;
using OJ.Stage;

namespace OJ.Dev
{
    /// <summary>
    /// 개발용 "모든 컨텐츠 언락". 로비의 모든 입구가 열리고 보이며, 모든 스테이지에 들어갈 수 있고, 모든 다이스를 쓸 수 있다.
    ///
    /// <b>새 경로를 뚫지 않는다.</b> 게임이 실제로 쓰는 호출만 부른다 — 그래야 이 치트가 만든 상태가 게임에서 일어날 수 있는 상태다
    /// (<see cref="DevCheatSections"/> 의 규약).
    /// <list type="bullet">
    /// <item><b>스테이지·입구:</b> 컨텐츠가 열렸는가는 <b>마지막 클리어 스테이지</b>의 함수다(<c>ContentUnlockRules</c>). 그래서 아직 못 깬 스테이지를
    ///   <c>RecordStageClear</c>(최소 등급)로 깬 것으로 적는다 — 이미 깬 스테이지는 건드리지 않아 높은 등급이 내려가지 않는다.
    ///   최소 등급이라 별 보상 칸은 그대로 남는다(보상을 가로채지 않는다).</item>
    /// <item><b>연출:</b> 열려도 연출을 안 봤으면 입구가 숨는다. 전부 "봤다" 고 적어(<c>MarkAnnounced</c>) 해금 연출·가이드가 줄줄이 나오지 않고
    ///   입구가 바로 보이게 한다.</item>
    /// <item><b>다이스:</b> 아직 못 가진 다이스를 <c>GrantFromContent</c> 로 준다(환급 목록은 버린다 — 새로 얻는 것만 대상이다).</item>
    /// </list>
    /// 무한의 탑 층수는 올리지 않는다(입구만 열린다). 층은 따로 "클리어 층" 치트가 있다.
    ///
    /// <b>에디터와 DEV_DEFINE 빌드에만 존재한다.</b> 릴리스 빌드에는 컴파일되지 않는다.
    /// </summary>
    internal static class ContentUnlockCheat
    {
        /// <summary>모두 연다. 무엇을 얼마나 열었는지 한 줄로 돌려준다(로그에도 남긴다).</summary>
        internal static string UnlockAll()
        {
            int stagesCleared = 0;
            int totalStages = 0;

            StageProgressManager progress = StageProgressManager.Instance;
            if (progress != null)
            {
                totalStages = Mathf.Max(1, StageDatabaseProvider.GetDatabase().StageCount);
                for (int stage = 1; stage <= totalStages; stage++)
                {
                    if (progress.GetBestClearGrade(stage) != StageClearGrade.None)
                        continue;

                    progress.RecordStageClear(stage, StageClearGrade.Minimum);
                    stagesCleared++;
                }
            }

            int announced = 0;
            ContentUnlockManager content = ContentUnlockManager.Instance;
            if (content != null)
            {
                foreach (ContentId id in Enum.GetValues(typeof(ContentId)))
                {
                    content.MarkAnnounced(id);
                    announced++;
                }
            }

            int dice = 0;
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
            if (ownership != null)
            {
                var ignoredRefunds = new List<PointRewardEntry>();
                foreach (DiceType type in Enum.GetValues(typeof(DiceType)))
                {
                    if (type == DiceType.Max || ownership.IsOwned(type))
                        continue;

                    if (ownership.GrantFromContent(type, ignoredRefunds))
                        dice++;
                }
            }

            string summary = "[치트] 모든 컨텐츠 언락 — 스테이지 " + stagesCleared + "개를 새로 깬 것으로 적음(전체 " + totalStages
                             + "), 입구 " + announced + "개 연출 완료, 다이스 " + dice + "종 지급";
            Debug.Log(summary);
            return summary;
        }
    }
}
#endif
