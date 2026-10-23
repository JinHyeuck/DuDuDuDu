using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;
using OJ.Save;
using OJ.Stage;

namespace OJ.ContentUnlock
{
    /// <summary>
    /// 로비 컨텐츠가 열렸는가, 보이는가, 그리고 <b>해금 연출을 봤는가</b>. (Docs/ContentUnlock.md)
    ///
    /// <b>열림은 저장하지 않는다</b>(<see cref="ContentUnlockRules"/> 주석). 마지막 클리어
    /// 스테이지를 <see cref="StageProgressManager"/> 에서 매번 읽고, 이 매니저가 드는 것은
    /// "연출을 끝까지 봤는가" 하나다.
    ///
    /// <b>MonoBehaviour 가 아니다.</b> <c>GameContainer</c> 가 만들고 <c>SaveService</c> 가 저장한다.
    /// </summary>
    [Preserve]
    public sealed class ContentUnlockManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static ContentUnlockManager Instance { get; internal set; }

        private readonly HashSet<ContentId> announced = new HashSet<ContentId>();

        /// <summary>
        /// 연출 기록을 들고 있는 세이브인가. <b>기본값이 true 다</b> — 세이브 파일이 없는 첫 실행은
        /// <c>ReadFrom</c> 이 안 불리고, 그 사람은 처음부터 기록을 쌓는다. 파일을 읽으면 적힌 값으로 덮인다.
        /// </summary>
        private bool initialized = true;

        /// <summary>
        /// 이 기능보다 먼저 생긴 세이브라, 이미 열린 것을 "본 것" 으로 채워야 한다.
        /// <b>읽을 때 바로 채우지 않는다</b> — 무엇이 열렸는지는 표(SO)가 알고, 표는 씬이 선 뒤에만
        /// 읽을 수 있다(<see cref="ContentUnlockDatabaseProvider"/> 주석). 처음 물을 때 채운다.
        /// </summary>
        private bool pendingBackfill;

        /// <summary>연출을 봤다고 적었을 때. 로비 입구들이 보임을 다시 맞춘다.</summary>
        public event Action OnChanged;

        private static IReadOnlyList<ContentUnlockDefinition> Definitions =>
            ContentUnlockDatabaseProvider.Database.Definitions;

        /// <summary>마지막으로 클리어한 스테이지. 0 이면 아직 한 판도 안 깼다.</summary>
        public int LastClearedStage =>
            StageProgressManager.Instance != null ? StageProgressManager.Instance.GetLastClearedStageIndex() : 0;

        /// <summary>1스테이지에서 깬 가장 높은 웨이브. 웨이브로 여는 컨텐츠(<c>requiredWave</c>)가 본다.</summary>
        public int Stage1BestWave =>
            StageProgressManager.Instance != null ? StageProgressManager.Instance.GetBestClearedWave(1) : 0;

        public ContentUnlockDefinition GetDefinition(ContentId id)
        {
            return ContentUnlockDatabaseProvider.Database.Get(id);
        }

        /// <summary>요구 클리어를 넘었는가. 표에 없으면 열린 것으로 본다 — 검사가 먼저 운다.</summary>
        public bool IsOpen(ContentId id)
        {
            ContentUnlockDefinition definition = GetDefinition(id);
            return definition == null || ContentUnlockRules.IsOpen(definition, LastClearedStage, Stage1BestWave);
        }

        /// <summary>입구를 보이는가. 열렸어도 연출 전이면 숨는다.</summary>
        public bool IsVisible(ContentId id)
        {
            ApplyBackfillIfNeeded();
            ContentUnlockDefinition definition = GetDefinition(id);
            return definition == null || ContentUnlockRules.IsVisible(definition, LastClearedStage, announced, Stage1BestWave);
        }

        /// <summary>이번 로비 진입에 보여 줄 연출(가이드 하나에서 끊긴다).</summary>
        public List<ContentUnlockDefinition> GetVisitQueue()
        {
            ApplyBackfillIfNeeded();
            return ContentUnlockRules.VisitQueue(Definitions, LastClearedStage, announced, Stage1BestWave);
        }

        /// <summary>
        /// 연출을 기다리는 것이 있는가. <b>로비의 자동 팝업(특별한 7일)이 이것을 보고 그 진입을 쉰다</b> —
        /// 해금 연출·가이드 위로 결제 창이 튀어나오면 흐름이 끊긴다.
        /// </summary>
        public bool HasPending()
        {
            ApplyBackfillIfNeeded();
            return ContentUnlockRules.HasPending(Definitions, LastClearedStage, announced, Stage1BestWave);
        }

        /// <summary>
        /// 연출(가이드면 가이드까지)을 끝까지 봤다고 적고 <b>그 자리에서</b> 저장한다.
        /// 가이드 도중에 앱을 끄면 이 줄에 닿지 않으므로 다음 진입에 처음부터 다시 나온다.
        /// </summary>
        public void MarkAnnounced(ContentId id)
        {
            if (!announced.Add(id))
                return;

            initialized = true;
            GameContainer.SaveService?.SaveAll();
            OnChanged?.Invoke();
        }

        /// <summary>개발 치트: 연출 기록을 비운다. 열린 것들의 연출이 다음 로비 진입에 다시 나온다.</summary>
        public void ResetAnnouncements()
        {
            announced.Clear();
            pendingBackfill = false;
            GameContainer.SaveService?.SaveAll();
            OnChanged?.Invoke();
        }

        private void ApplyBackfillIfNeeded()
        {
            if (!pendingBackfill)
                return;

            pendingBackfill = false;
            initialized = true;

            IReadOnlyList<ContentUnlockDefinition> definitions = Definitions;
            int lastCleared = LastClearedStage;
            for (int i = 0; i < definitions.Count; i++)
            {
                ContentUnlockDefinition d = definitions[i];
                if (d != null && ContentUnlockRules.IsOpen(d, lastCleared, Stage1BestWave))
                    announced.Add(d.contentId);
            }
        }

        // ── 저장 ───────────────────────────────────────────────────────

        public void WriteTo(SaveState state)
        {
            // 읽은 값 그대로 쓴다(채우기·연출 기록이 생겨야 true 가 된다). 채우기 전에 저장이 오면
            // "기록 없음" 이 그대로 남아야 다음 실행에서 채운다 — 그리고 SaveService 의 왕복 검사가
            // 읽은 것과 다시 쓴 것을 문자열로 대조한다.
            state.ContentUnlock.Initialized = initialized;

            state.ContentUnlock.Announced.Clear();
            var names = new List<string>();
            foreach (ContentId id in announced)
                names.Add(id.ToString());
            names.Sort(StringComparer.Ordinal);
            state.ContentUnlock.Announced.AddRange(names);
        }

        public void ReadFrom(SaveState state)
        {
            announced.Clear();
            foreach (string name in state.ContentUnlock.Announced)
            {
                // 모르는 이름은 버린다 — 컨텐츠가 표에서 빠졌거나 이름이 바뀐 것이다.
                if (Enum.TryParse(name, out ContentId id) && Enum.IsDefined(typeof(ContentId), id))
                    announced.Add(id);
            }

            initialized = state.ContentUnlock.Initialized;
            pendingBackfill = ContentUnlockRules.ShouldBackfill(initialized, ContentUnlockRules.LastClearedFromSave(state));
        }
    }
}
