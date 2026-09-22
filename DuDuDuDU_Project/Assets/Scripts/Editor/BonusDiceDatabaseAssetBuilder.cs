using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Pinball;
using OJ.Pinball;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="BonusDiceDatabase"/> 에셋을 만들고 검사한다.
    /// <see cref="PinballDatabaseAssetBuilder"/> 와 같은 자리의 도구다.
    ///
    /// <b>덮어쓰지 않는다.</b> 이미 있으면 검사만 하고 끝낸다 — 손으로 맞춘 수치를 도구가
    /// 조용히 되돌리면 값이 왜 바뀌었는지 아무도 못 찾는다.
    /// 기본값으로 되돌리려면 에셋을 지우고 다시 돌릴 것.
    ///
    /// <b>검사가 이 도구의 본체다.</b> 이 컨텐츠는 배선이 어긋나도 게임이 멀쩡히 굴러간다 —
    /// 보상 라운드가 영영 안 열리거나, 열려도 채울 수 없는 핀이 남거나, 채워도 아무것도
    /// 안 나오거나. 셋 다 예외를 내지 않고 조용하다. 그래서 여기서 미리 말해 준다.
    /// </summary>
    public static class BonusDiceDatabaseAssetBuilder
    {
        private const string AssetPath = "Assets/ScriptableObject/BonusDiceDatabase.asset";
        private const string PinballDatabasePath = "Assets/ScriptableObject/PinballRewardDatabase.asset";

        /// <summary>수치표 에셋이 아직 없을 때 쓸 트리거 태그. 에셋이 있으면 그쪽이 정본이다.</summary>
        private const int DefaultTriggerTag = 3;

        /// <summary>
        /// 센터핀을 몇 번 맞히면 라운드가 열리는가.
        ///
        /// 황금핀 10 · 폭탄핀 15 보다 높게 잡았다 — 이 핀의 보상이 한 판 전체이기 때문이다.
        /// "정말 많이 맞혔다"는 느낌이 나야 칭찬이 되고, 너무 자주 열리면 보상 라운드가
        /// 특별하지 않게 된다. 조정은 에셋에서 한다.
        /// </summary>
        private const int DefaultTriggerHits = 25;

        [MenuItem("OJ/개발/보상라운드/수치표 에셋 만들기")]
        private static void CreateOrValidate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BonusDiceDatabase>(AssetPath);
            if (existing != null)
            {
                UpgradeIfNeeded(existing);
                Report("[보상라운드] 이미 있다: " + AssetPath, existing);
                return;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));

            var database = ScriptableObject.CreateInstance<BonusDiceDatabase>();
            database.PopulateDefaults();

            AssetDatabase.CreateAsset(database, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report("[보상라운드] 새로 만들었다: " + AssetPath + System.Environment.NewLine +
                   "  StaticResource 빈 슬롯 채우기가 다음 컴파일에 자동으로 잇는다.", database);
        }

        /// <summary>
        /// 센터핀(트리거 태그)을 핀볼 경품표에 등록한다. <b>이것이 없으면 라운드가 영영 안 열린다.</b>
        ///
        /// <c>PinballManager.ResolveSpecialHit</c> 은 경품표에 없는 태그를 만나면 게이지를
        /// 올리지도 않고 조용히 돌아간다. 판에 핀을 박아 둬도 소용이 없다.
        ///
        /// <b>보상은 비운다.</b> 센터핀의 보상은 재화가 아니라 보상 라운드 그 자체다.
        /// 그래서 <c>ResolveSpecialHit</c> 이 돌려주는 목록은 비게 되고, 그 대신
        /// <c>completions</c> 로 "방금 다 찼다"를 알린다.
        /// </summary>
        [MenuItem("OJ/개발/보상라운드/센터핀을 핀볼 경품표에 등록")]
        private static void RegisterTriggerPin()
        {
            var pinballDatabase = AssetDatabase.LoadAssetAtPath<PinballRewardDatabase>(PinballDatabasePath);
            if (pinballDatabase == null)
            {
                Debug.LogError("[보상라운드] 핀볼 경품표가 없다: " + PinballDatabasePath);
                return;
            }

            var bonus = AssetDatabase.LoadAssetAtPath<BonusDiceDatabase>(AssetPath);
            int tag = bonus != null ? bonus.pinballTriggerTag : DefaultTriggerTag;

            if (pinballDatabase.GetSpecial(tag) != null)
            {
                Debug.Log("[보상라운드] 태그 " + tag + " 는 이미 핀볼 경품표에 있다. 그대로 둔다.",
                          pinballDatabase);
                return;
            }

            pinballDatabase.specialRewards.Add(new PinballSpecialReward
            {
                tag = tag,
                label = "센터핀",
                requiredHits = DefaultTriggerHits,
                rewards = new List<PinballReward>(),     // 보상은 라운드 그 자체다
            });

            EditorUtility.SetDirty(pinballDatabase);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[보상라운드] 센터핀(태그 " + tag + ")을 핀볼 경품표에 등록했다. " +
                "requiredHits = " + DefaultTriggerHits + " — 센터핀을 이만큼 맞히면 보상 라운드가 열린다. " +
                "값은 PinballRewardDatabase.asset 에서 바꾸면 된다.",
                pinballDatabase);
        }

        /// <summary>
        /// 비어 있는 항목만 채운다. <b>덮어쓰지 않는다.</b>
        ///
        /// <c>Create &gt; OJ &gt; Bonus Dice Database</c> 로 손수 만든 에셋은 <c>pinRewards</c> 가
        /// 빈 목록인데, 그 상태로 라운드를 열면 <b>채울 핀이 하나도 없어 열리자마자 끝난다</b> —
        /// 보상도 안 나오고 에러도 안 난다. 손으로 맞춘 값이 있는 경우와 구별하기 위해
        /// <i>비어 있을 때만</i> 채운다.
        /// </summary>
        private static void UpgradeIfNeeded(BonusDiceDatabase database)
        {
            if (database.pinRewards != null && database.pinRewards.Count > 0)
                return;

            var defaults = ScriptableObject.CreateInstance<BonusDiceDatabase>();
            defaults.PopulateDefaults();
            database.pinRewards = defaults.pinRewards;
            Object.DestroyImmediate(defaults);

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[보상라운드] pinRewards 가 비어 있어 기본 " + database.pinRewards.Count +
                "핀으로 채웠다. 보상 수량은 에셋에서 조정할 것.", database);
        }

        [MenuItem("OJ/개발/보상라운드/수치표 검사")]
        private static void ValidateOnly()
        {
            var database = AssetDatabase.LoadAssetAtPath<BonusDiceDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError("[보상라운드] 수치표 에셋이 없다: " + AssetPath +
                               " — 지금은 코드 기본값으로 돌아가고 있다.");
                return;
            }

            Report("[보상라운드] 검사: " + AssetPath, database);
        }

        // ────────────────────────────────────────────────

        private static void Report(string header, BonusDiceDatabase database)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);

            string issues = database.Validate();
            if (!string.IsNullOrEmpty(issues))
                sb.Append(issues);

            AppendTriggerCheck(sb, database);
            AppendBonusBoardCheck(sb, database);
            AppendEasinessCheck(sb, database);

            string text = sb.ToString();
            bool healthy = !string.IsNullOrEmpty(header) && text.TrimEnd() == header.TrimEnd();

            if (healthy)
                Debug.Log(text + "  이상 없음.", database);
            else
                Debug.LogWarning(text, database);
        }

        /// <summary>
        /// 핀볼에서 이 라운드로 오는 길이 실제로 뚫려 있는가.
        ///
        /// <b>가장 조용한 고장이 여기다.</b> <c>PinballManager.ResolveSpecialHit</c> 는 경품표에
        /// 없는 태그를 만나면 <i>게이지를 올리지도 않고</i> 그냥 돌아간다. 그러면 센터핀을
        /// 아무리 맞혀도 라운드가 영영 안 열리는데, 로그도 예외도 없다.
        /// </summary>
        private static void AppendTriggerCheck(StringBuilder sb, BonusDiceDatabase database)
        {
            var pinballDatabase = AssetDatabase.LoadAssetAtPath<PinballRewardDatabase>(PinballDatabasePath);
            if (pinballDatabase == null)
            {
                sb.AppendLine("핀볼 경품표를 못 찾아 트리거 태그를 확인하지 못했다: " + PinballDatabasePath);
                return;
            }

            PinballSpecialReward rule = pinballDatabase.GetSpecial(database.pinballTriggerTag);
            if (rule == null)
            {
                sb.AppendLine(
                    "트리거 태그 " + database.pinballTriggerTag + " 가 핀볼 경품표에 없다. " +
                    "ResolveSpecialHit 이 모르는 태그의 게이지는 올리지 않으므로 " +
                    "<b>보상 라운드가 영영 안 열린다.</b> PinballRewardDatabase 의 specialRewards 에 " +
                    "그 태그를 requiredHits 와 함께 추가할 것(보상은 비워도 된다 — 보상이 곧 이 라운드다).");
                return;
            }

            if (rule.requiredHits < 1)
            {
                sb.AppendLine("트리거 태그 " + database.pinballTriggerTag +
                              " 의 requiredHits 가 1 미만이라 게이지가 안 찬다.");
            }

            AppendTriggerPegCheck(sb, database.pinballTriggerTag);
        }

        /// <summary>판에 그 태그를 단 핀이 실제로 박혀 있는가. 표에만 있고 판에 없으면 못 맞힌다.</summary>
        private static void AppendTriggerPegCheck(StringBuilder sb, int triggerTag)
        {
            PinballBoard board = FindBoard("PinballBoard");
            if (board == null)
            {
                sb.AppendLine("핀볼 판(PinballBoard.asset)을 못 찾아 센터핀 유무를 확인하지 못했다.");
                return;
            }

            if (!board.SpecialTags().Contains(triggerTag))
            {
                sb.AppendLine(
                    "핀볼 판에 specialTag " + triggerTag + " 인 핀이 없다. 경품표에만 있고 판에는 없으면 " +
                    "맞힐 대상 자체가 없어 라운드가 안 열린다. Sim Lab 의 Edit Board 에서 센터핀을 지정할 것.");
            }
        }

        /// <summary>
        /// 보상표의 태그와 보상판의 핀이 짝이 맞는가.
        ///
        /// 표에만 있는 태그는 <b>영원히 못 채우는 핀</b>이 되어 "광고를 아무리 봐도 안 끝나는"
        /// 라운드를 만든다 — 무한 재도전의 상한이 거기서 깨진다.
        /// </summary>
        private static void AppendBonusBoardCheck(StringBuilder sb, BonusDiceDatabase database)
        {
            PinballBoard board = FindBoard("BonusBoard");
            if (board == null)
            {
                sb.AppendLine("보상판(BonusBoard.asset)을 아직 못 찾았다 — 핀 짝 검사를 건너뛴다.");
                return;
            }

            List<int> boardTags = board.SpecialTags();

            if (database.pinRewards != null)
            {
                for (int i = 0; i < database.pinRewards.Count; i++)
                {
                    BonusPinReward pin = database.pinRewards[i];
                    if (pin == null || !boardTags.Contains(pin.tag))
                    {
                        sb.AppendLine("표의 태그 " + (pin != null ? pin.tag.ToString() : "?") +
                                      " 가 보상판에 없다. <b>영원히 못 채우는 핀</b>이라 라운드가 안 끝난다.");
                    }
                }
            }

            for (int i = 0; i < boardTags.Count; i++)
            {
                if (database.GetPin(boardTags[i]) == null)
                {
                    sb.AppendLine("보상판의 태그 " + boardTags[i] +
                                  " 에 보상이 안 걸려 있다. 맞아도 아무 일도 안 일어난다.");
                }
            }
        }

        /// <summary>
        /// <b>"쉬운 칭찬"이 실제로 성립하는가.</b> 이 컨텐츠의 목적이 그것이라 수치로 본다.
        ///
        /// 최악의 라운드(노페어만 계속 나오는 경우)에도 핀이 다 채워져야 한다. 못 채우면
        /// 운 나쁜 유저는 <b>광고를 봐야만</b> 끝낼 수 있게 되는데, 그건 이 컨텐츠가 하려던
        /// 일의 반대다. 광고는 안전망이지 필수 경로가 아니다.
        ///
        /// <b>"공 한 발 = 적중 한 번"이 아니다.</b> 핀마다 목표 적중 확률이 따로 있어
        /// 한 발이 여러 핀을 동시에 맞힐 수 있다. 그래서 합계가 아니라 <b>핀마다</b> 본다.
        /// </summary>
        private static void AppendEasinessCheck(StringBuilder sb, BonusDiceDatabase database)
        {
            if (database.pinRewards == null || database.pinRewards.Count == 0)
                return;

            int worstBalls = BonusDiceRules.BallCountFor(DiceHand.None, database.ballTable)
                             * Mathf.Max(1, database.cyclesPerRound);

            sb.AppendLine("  최악의 라운드(노페어만): 공 " + worstBalls + "발");

            PinballBoard bonus = FindBoard("BonusBoard");
            if (bonus == null)
            {
                sb.AppendLine("  보상판이 없어 핀별 기대 적중을 계산하지 못했다.");
                return;
            }

            if (bonus.specialHitPolicy != SpecialHitPolicy.Declared)
            {
                sb.AppendLine(
                    "  보상판의 specialHitPolicy 가 Natural 이라 적중 확률이 제어되지 않는다. " +
                    "쉬운 정도를 수치로 보증할 수 없으니 Declared 로 두고 targetHitProbability 를 적을 것.");
                return;
            }

            for (int i = 0; i < database.pinRewards.Count; i++)
            {
                BonusPinReward pin = database.pinRewards[i];
                if (pin == null || pin.requiredHits <= 0)
                    continue;

                float probability = TargetProbability(bonus, pin.tag);
                string name = string.IsNullOrEmpty(pin.label) ? "태그 " + pin.tag : pin.label;

                if (probability <= 0f)
                {
                    sb.AppendLine("  " + name + ": 목표 적중 확률이 0 이라 영원히 못 채운다.");
                    continue;
                }

                float expected = worstBalls * probability;
                sb.AppendLine("  " + name + ": 기대 적중 " + expected.ToString("0.0") +
                              "회 / 필요 " + pin.requiredHits + "회");

                if (expected < pin.requiredHits)
                {
                    sb.AppendLine("    최악의 경우 모자란다. requiredHits 를 " +
                                  Mathf.FloorToInt(expected) + " 이하로 낮추거나 " +
                                  "targetHitProbability 를 올릴 것.");
                }
            }
        }

        /// <summary>판이 그 태그에 걸어 둔 목표 적중 확률. 규칙이 없으면 0.</summary>
        private static float TargetProbability(PinballBoard board, int tag)
        {
            if (board.specialRules == null)
                return 0f;

            for (int i = 0; i < board.specialRules.Length; i++)
            {
                if (board.specialRules[i].tag == tag)
                    return board.specialRules[i].targetHitProbability;
            }
            return 0f;
        }

        /// <summary>이름으로 판을 찾는다. 경로를 박으면 폴더를 옮기는 순간 조용히 못 찾는다.</summary>
        private static PinballBoard FindBoard(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets(assetName + " t:" + nameof(PinballBoard));
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != assetName)
                    continue;

                var board = AssetDatabase.LoadAssetAtPath<PinballBoard>(path);
                if (board != null)
                    return board;
            }
            return null;
        }
    }
}
