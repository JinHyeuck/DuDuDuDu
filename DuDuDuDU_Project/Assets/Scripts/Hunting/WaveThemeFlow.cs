using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using OJ.Analytics;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Economy;

namespace OJ.Hunting
{
    /// <summary>
    /// 웨이브 테마의 진행(2026-10-27). 웨이브가 끝나면 <see cref="WaveTheme.After"/> 가 정한 보상을 <b>유저가 고르게</b> 하고,
    /// 다 끝나면 다음 웨이브를 연다. 그 사이 게임은 멈추지 않는다 — 몬스터가 없는 빈 필드라 멈출 이유가 없고,
    /// 보드 정리(소환·머지)는 선택을 기다리는 동안에도 된다.
    ///
    /// <b>화면 요소(상단 현황·시작 문구·체력바)도 여기서 만들고 쥔다.</b> GameManager 는 "웨이브가 열렸다 / 끝났다" 만 알린다.
    /// </summary>
    public sealed class WaveThemeFlow
    {
        private readonly GameManager game;
        private readonly IBattleRefs battle;
        private readonly TMP_FontAsset font;
        private readonly Transform canvas;

        /// <summary>선택 창을 올리는 캔버스. 기여도 패널 같은 다이얼로그가 사는 캔버스라 그 위에 덮인다(전투 캔버스에 올리면 다이얼로그가 위로 보인다).</summary>
        private readonly Transform popupRoot;

        private readonly System.Func<float> bossTimeLeft;
        private UIChoicePopup popup;
        private int adUses;

        /// <summary>보스전 이벤트의 남은 시간(초). 이벤트 중이 아니면 -1.</summary>
        private float eventTimeLeft = -1f;

        /// <summary>이번 판의 이벤트 계획.</summary>
        public WaveEventPlan Plan { get; private set; }

        public UIWaveStrip Strip { get; private set; }
        public UIWaveStartBanner Banner { get; private set; }
        public UIEnemyBar EnemyBar { get; private set; }

        public WaveThemeFlow(GameManager game, IBattleRefs battle, TMP_FontAsset font, Transform canvas, Transform popupRoot, int totalWaves, System.Func<float> bossTimeLeft)
        {
            this.game = game;
            this.battle = battle;
            this.font = font;
            this.canvas = canvas;
            this.popupRoot = popupRoot != null ? popupRoot : canvas;
            this.bossTimeLeft = bossTimeLeft;

            // 이벤트 자리마다 무슨 이벤트가 나올지는 판이 시작될 때 한 번 정한다(상단 보상 아이콘과 실제가 같도록).
            Plan = WaveEventPlan.Build(totalWaves, UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2));
            Strip = UIWaveStrip.Create(canvas, font, totalWaves, Plan.After);
            // 문구는 선택 창과 같은 캔버스에 올린다(기여도 패널 같은 다이얼로그에 가려지지 않게).
            Banner = UIWaveStartBanner.Create(this.popupRoot, font);
            EnemyBar = UIEnemyBar.Create(canvas, font, battle.Monsters, MonsterInfo);
            Strip.SetWave(1);
        }

        /// <summary>체력바가 보여 줄 대상의 이름과 시간. 이벤트 보스는 30초, 스테이지 보스는 60초, 현상금은 시간이 없다.</summary>
        private (string name, float left, float limit) MonsterInfo(Monster monster)
        {
            if (battle.Bounty != null && battle.Bounty.IsEventBoss(monster))
                return ("보스전", eventTimeLeft, WaveTheme.EventBossSeconds);

            if (monster != null && monster.IsStageBoss)
                return ("BOSS", bossTimeLeft != null ? bossTimeLeft() : -1f, WaveTheme.BossTimeLimitSeconds);

            return ("현상금", -1f, 0f);
        }

        /// <summary>웨이브가 열렸다.</summary>
        public void OnWaveStarted(int wave)
        {
            Strip.SetWave(wave);
            Banner.Play(wave, WaveTheme.IsBossWave(wave));
        }

        /// <summary>떠 있는 창·문구를 거둔다(판이 끝났거나 되돌렸을 때).</summary>
        public void CloseTransient()
        {
            if (popup != null)
                popup.Close();

            popup = null;
            eventTimeLeft = -1f;
            Banner?.HideNow();
        }

        public void Destroy()
        {
            CloseTransient();
            if (Strip != null) Object.Destroy(Strip.gameObject);
            if (Banner != null) Object.Destroy(Banner.gameObject);
            if (EnemyBar != null) Object.Destroy(EnemyBar.gameObject);
        }

        /// <summary>끝난 웨이브의 보상 흐름. 다 끝나면 돌아온다.</summary>
        public IEnumerator RunInterlude(int wave)
        {
            switch (Plan.After(wave))
            {
                case WaveInterlude.SoulBonus:
                    yield return OfferSoulBonus();
                    break;
                case WaveInterlude.BountyChallenge:
                    yield return BountyChallenge();
                    break;
                case WaveInterlude.SlotsOrSoul:
                    yield return SlotsOrSoul();
                    break;
                case WaveInterlude.BossReward:
                    GrantSlots();
                    yield return OfferSoulBonus();
                    break;
                case WaveInterlude.LuckyDice:
                    yield return LuckyDiceEvent();
                    break;
                case WaveInterlude.BossChallenge:
                    yield return BossChallengeEvent(wave);
                    break;
                case WaveInterlude.WallHeal:
                    yield return WallHealEvent();
                    break;
                case WaveInterlude.DiceGiftBasic:
                    yield return DiceGiftEvent(false);
                    break;
                case WaveInterlude.DiceGiftRare:
                    yield return DiceGiftEvent(true);
                    break;
                case WaveInterlude.SpGamble:
                    yield return SpGambleEvent();
                    break;
                case WaveInterlude.StoneGamble:
                    yield return StoneGambleEvent();
                    break;
                case WaveInterlude.FreeSummons:
                    yield return FreeSummonsEvent();
                    break;
                case WaveInterlude.ProbUp:
                    yield return ProbUpEvent();
                    break;
                case WaveInterlude.WallUpgrade:
                    yield return WallUpgradeEvent();
                    break;
                case WaveInterlude.Exchange:
                    yield return ExchangeEvent();
                    break;
                case WaveInterlude.AttackSpeedBuff:
                    yield return BuffEvent("공격 속도", "다음 웨이브 공격 속도 +" + AttackSpeedPercent + "%", "다음 웨이브 동안 모든 다이스가 빨라져요",
                        () => WaveBuffs.AddAttackSpeedPercent(AttackSpeedPercent), false);
                    break;
                case WaveInterlude.SlowBuff:
                    yield return BuffEvent("슬로우", "다음 웨이브 몬스터 이동 -" + SlowPercent + "%", "몬스터가 느리게 내려와요",
                        () => WaveBuffs.AddMonsterSlowPercent(SlowPercent), true);
                    break;
                case WaveInterlude.WeakenBuff:
                    yield return BuffEvent("약화", "다음 웨이브 몬스터 체력 -" + WeakenPercent + "%", "몬스터가 약하게 나와요",
                        () => WaveBuffs.AddMonsterHpPercent(-WeakenPercent), false);
                    break;
                case WaveInterlude.RiskyDeal:
                    yield return RiskyDealEvent();
                    break;
                case WaveInterlude.Merchant:
                    yield return MerchantEvent();
                    break;
            }
        }

        // ── 이벤트 공통 ───────────────────────────────────────────────

        private static readonly Color StoneColor = new Color(0.62f, 0.55f, 0.95f, 1f);
        private static readonly Color SpColor = new Color(0.25f, 0.55f, 0.85f, 1f);
        private static readonly Color GoodColor = new Color(0.86f, 0.6f, 0.12f, 1f);
        private static readonly Color HealColor = new Color(0.2f, 0.62f, 0.4f, 1f);
        private static readonly Color DangerColor = new Color(0.72f, 0.2f, 0.2f, 1f);

        /// <summary>선택 창을 띄우고 고를 때까지 기다린다. 결과는 <paramref name="result"/>[0] — 판이 끝났으면 -1.</summary>
        private IEnumerator Ask(string title, string message, List<UIChoicePopup.Option> options, int[] result)
        {
            int choice = -1;
            popup = UIChoicePopup.Open(popupRoot, font, title, message, options, i => choice = i);
            while (choice < 0 && !game.IsFinished)
                yield return null;

            popup = null;
            result[0] = game.IsFinished ? -1 : choice;
        }

        /// <summary>화면 한가운데에 결과 문구를 잠깐 띄우고 기다린다(받은 것·못 받은 것을 알리는 한 줄).</summary>
        private IEnumerator Toast(string text, Color color)
        {
            Banner?.PlayText(text, color, 0.4f, 0.15f);
            yield return new WaitForSecondsRealtime(0.8f);
        }

        /// <summary>
        /// 마석을 주고 <b>날려 보낸다</b>(현상금·영혼 보너스와 같은 연출). 먼저 쥐고 나서 넣어야 화면 숫자가 아이콘이 닿을 때 오른다.
        /// <paramref name="from"/> 은 출발 화면 좌표 — 보통 고른 버튼 자리다.
        /// </summary>
        private void AddStones(int amount, Vector2 from)
        {
            if (amount <= 0)
                return;

            battle.ElementUpgrade?.HoldStone(amount);
            battle.BattlePoints?.Add(BattlePointType.EnhanceStone, amount);
            game.SoulBonus?.FlyEnhanceStone(from, amount);
        }

        /// <summary>SP 를 주고 소환 버튼으로 날려 보낸다.</summary>
        private void AddSp(int amount, Vector2 from)
        {
            if (amount <= 0)
                return;

            battle.Summon?.AddSP(amount);
            game.SoulBonus?.FlySummonPoint(from, amount);
        }

        /// <summary>선택 창에서 방금 고른 버튼의 자리.</summary>
        private static Vector2 Chosen => UIChoicePopup.LastChosenScreen;

        private Vector2 ScreenOf(Vector3 world)
        {
            Camera camera = Camera.main;
            return camera != null ? (Vector2)camera.WorldToScreenPoint(world) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        // ── 행운 주사위 ───────────────────────────────────────────────

        /// <summary>50 SP 를 받는 대신 행운 주사위를 굴려 마석 5~15개를 받을 수 있다.</summary>
        private IEnumerator LuckyDiceEvent()
        {
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option("행운 주사위 굴리기", "마석 " + LuckyDiceRules.EventMinStones + "~" + LuckyDiceRules.EventMaxStones + "개 (평균 약 10개)", StoneColor),
                new UIChoicePopup.Option(WaveTheme.EventSp + " SP 받기", "주사위 없이 SP로 받아요", SpColor),
            };

            var result = new int[1];
            yield return Ask("행운 주사위!", "주사위를 굴려 마석을 받을 수 있어요.\n아니면 " + WaveTheme.EventSp + " SP를 받아요.", options, result);
            if (result[0] < 0)
                yield break;

            if (result[0] == 1)
            {
                AddSp(WaveTheme.EventSp, Chosen);
                yield return new WaitForSecondsRealtime(0.8f);
                yield break;
            }

            // 굴림 연출 창(UILuckyDiceResultPopup)이 마석을 쥐고 보유 칸으로 날려 보낸다 — 여기서는 지급만 한다.
            int stones = LuckyDiceRules.RollEvent(UnityEngine.Random.Range(0, 100));
            battle.BattlePoints?.Add(BattlePointType.EnhanceStone, stones);

            // 굴림 연출(전체 화면, 눌러서 건너뛴다)이 끝나기를 기다린다. 못 열면 날려서 알린다.
            UILuckyDiceResultPopup roll = GameContainer.UI?.Get<UILuckyDiceResultPopup>();
            if (roll == null)
            {
                game.SoulBonus?.FlyEnhanceStone(Chosen, stones);
                yield return new WaitForSecondsRealtime(0.8f);
                yield break;
            }

            roll.Play(stones);
            yield return null;
            while (UILuckyDiceResultPopup.IsPlaying && !game.IsFinished)
                yield return null;
        }

        // ── 보스전 ────────────────────────────────────────────────────

        /// <summary>마석 10개 받고 지나가거나, 30초 안에 보스를 잡으면 마석 20개(못 잡으면 없다. 벽이 닳아도 선택의 몫).</summary>
        private IEnumerator BossChallengeEvent(int wave)
        {
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option("마석 " + WaveTheme.EventBossPassStones + "개 받고 지나가기", "안전하게 받아요", StoneColor),
                new UIChoicePopup.Option("보스전 도전!", WaveTheme.EventBossSeconds.ToString("0") + "초 안에 잡으면 마석 " + WaveTheme.EventBossWinStones + "개\n못 잡으면 없어요", DangerColor),
            };

            var result = new int[1];
            yield return Ask("보스전", "보스를 제한 시간 안에 잡을까요?", options, result);
            if (result[0] < 0)
                yield break;

            if (result[0] == 0)
            {
                AddStones(WaveTheme.EventBossPassStones, Chosen);
                yield return new WaitForSecondsRealtime(0.8f);
                yield break;
            }

            Vector2 chosen = Chosen;
            Monster boss = battle.Bounty.SummonEventBoss(WaveTheme.EventBossGrade(wave));
            if (boss == null)
            {
                // 소환하지 못하면 도전한 값을 못 치르게 된다 — 지나가는 보상으로 갈음한다.
                AddStones(WaveTheme.EventBossPassStones, chosen);
                yield return new WaitForSecondsRealtime(0.8f);
                yield break;
            }

            bool defeated = false;
            Vector2 lastSeen = ScreenOf(boss.transform.position);
            void OnDefeated(Monster m)
            {
                if (m == boss)
                    defeated = true;
            }

            battle.Bounty.OnEventBossDefeated += OnDefeated;
            Banner?.PlayText("보스 출현!", new Color(1f, 0.32f, 0.28f, 1f), 0.4f, 0.15f);
            eventTimeLeft = WaveTheme.EventBossSeconds;
            try
            {
                // 게임 시간으로 줄인다 — 배속·일시정지를 따른다. 잡거나 시간이 다 되거나 판이 끝나면 끝난다.
                while (!defeated && eventTimeLeft > 0f && !game.IsFinished)
                {
                    eventTimeLeft -= Time.deltaTime;
                    if (boss != null)
                        lastSeen = ScreenOf(boss.transform.position);

                    yield return null;
                }
            }
            finally
            {
                battle.Bounty.OnEventBossDefeated -= OnDefeated;
                eventTimeLeft = -1f;
            }

            if (game.IsFinished)
                yield break;

            if (defeated)
            {
                // 보스가 쓰러진 자리에서 마석이 날아오른다.
                AddStones(WaveTheme.EventBossWinStones, lastSeen);
                yield return Toast("보스 처치!", StoneColor);
            }
            else
            {
                yield return Toast("시간 초과… 마석 없음", DangerColor);
            }
        }

        // ── 벽 회복 ───────────────────────────────────────────────────

        /// <summary>벽 체력 10% 를 회복하거나 50 SP 를 받는다.</summary>
        private IEnumerator WallHealEvent()
        {
            Wall wall = game.wall;
            int heal = wall != null ? WaveTheme.WallHealAmount(wall.TotalHp) : 0;
            string status = wall != null ? "벽 체력 " + wall.CurrentHp + " / " + wall.TotalHp : string.Empty;

            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option("벽 체력 " + WaveTheme.WallHealPercent + "% 회복", "+" + heal + " 회복해요", HealColor),
                new UIChoicePopup.Option(WaveTheme.EventSp + " SP 받기", "다이스를 더 뽑아요", SpColor),
            };

            var result = new int[1];
            yield return Ask("벽 수리", status, options, result);
            if (result[0] < 0)
                yield break;

            if (result[0] == 0 && wall != null)
            {
                wall.Heal(heal);
                yield return Toast("벽 체력 +" + heal, new Color(0.45f, 1f, 0.6f, 1f));
            }
            else
            {
                AddSp(WaveTheme.EventSp, Chosen);
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }

        // ── 다이스 선물 ───────────────────────────────────────────────

        private readonly struct GiftDice
        {
            public readonly DiceType Type;
            public readonly int Star;
            public readonly string Name;

            public GiftDice(DiceType type, int star, string name)
            {
                Type = type;
                Star = star;
                Name = name;
            }

            public string Label => Star > 1 ? Name + " " + Star + "성" : Name;
        }

        // 같은 등급대 안에서 무작위 둘을 보여 준다. 기본 다이스는 2성, 특수(레어) 다이스는 합쳐지지 않는 1성이다.
        private static readonly GiftDice[] BasicGifts =
        {
            new GiftDice(DiceType.Normal, 2, "노멀 다이스"),
            new GiftDice(DiceType.Fire, 2, "파이어 다이스"),
            new GiftDice(DiceType.Ice, 2, "아이스 다이스"),
            new GiftDice(DiceType.Thunder, 2, "썬더 다이스"),
            new GiftDice(DiceType.Poison, 2, "독 다이스"),
        };

        private static readonly GiftDice[] RareGifts =
        {
            new GiftDice(DiceType.Tornado, 1, "허리케인 다이스"),
            new GiftDice(DiceType.Stun, 1, "스턴 다이스"),
            new GiftDice(DiceType.ArmorBreak, 1, "아머브레이크 다이스"),
            new GiftDice(DiceType.Wind, 1, "윈드 다이스"),
            new GiftDice(DiceType.Time, 1, "타임 다이스"),
        };

        private int FindEmptySlot()
        {
            UIBoard board = battle.Board;
            if (board == null)
                return -1;

            int total = board.rows * board.cols;
            for (int i = 0; i < total; i++)
            {
                if (board.GetDice(i) == null && board.IsSlotUnlocked(i))
                    return i;
            }

            return -1;
        }

        /// <summary>같은 등급대 풀에서 서로 다른 둘을 무작위로 뽑는다.</summary>
        private static GiftDice[] PickTwo(GiftDice[] pool)
        {
            int first = UnityEngine.Random.Range(0, pool.Length);
            int second = UnityEngine.Random.Range(0, pool.Length - 1);
            if (second >= first)
                second++;

            return new[] { pool[first], pool[second] };
        }

        /// <summary>같은 등급대의 다이스 중 무작위 둘을 보여 주고 하나를 보드에 받는다. 빈 칸이 없으면 SP 로 바꿔 준다.</summary>
        private IEnumerator DiceGiftEvent(bool rare)
        {
            GiftDice[] pair = PickTwo(rare ? RareGifts : BasicGifts);
            string sub = rare ? "특수 다이스를 바로 놓아요" : "보드에 바로 놓아요";
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option(pair[0].Label, sub, GoodColor),
                new UIChoicePopup.Option(pair[1].Label, sub, StoneColor),
            };

            var result = new int[1];
            yield return Ask(rare ? "특수 다이스 선물" : "다이스 선물", "하나를 골라 받아요.", options, result);
            if (result[0] < 0)
                yield break;

            GiftDice gift = pair[result[0]];
            int slot = FindEmptySlot();
            if (slot < 0)
            {
                int sp = WaveTheme.EventSp / 2;
                AddSp(sp, Chosen);
                yield return Toast("자리가 없어 SP로 드려요", new Color(0.55f, 0.9f, 1f, 1f));
                yield break;
            }

            battle.DiceStars.OnDiceSpawn(gift.Type, gift.Star);
            battle.Board.SpawnDice(gift.Type, gift.Star, slot);
            RunHistoryManager.Instance?.RecordSummon(gift.Type, gift.Star, game.CurrentWaveIndex, 0, battle.Summon.currentSP);
            yield return Toast(gift.Label + " 획득!", GoodColor);
        }

        // ── 영혼 보너스 ───────────────────────────────────────────────

        /// <summary>기존 영혼 보너스 창(SP / 마석)을 띄우고 <b>고를 때까지</b> 다시 띄운다. 닫아 버려도 값은 고정이라 꼼수가 없다.</summary>
        private IEnumerator OfferSoulBonus()
        {
            OJ.Economy.SoulBonusManager soul = game.SoulBonus;
            if (soul == null)
                yield break;

            int before = soul.ChoiceCount;
            while (soul.ChoiceCount == before && !game.IsFinished)
            {
                if (!soul.IsPopupOpen)
                {
                    soul.OfferNow();
                    yield return new WaitForSecondsRealtime(0.25f);
                    continue;
                }

                yield return null;
            }
        }

        // ── 현상금 ────────────────────────────────────────────────────

        private IEnumerator BountyChallenge()
        {
            int choice = -1;
            popup = UIChoicePopup.Open(popupRoot, font, "현상금 도전!", "현상금 몬스터를 잡으면 보상을 받아요.\n도전해 볼래요?",
                new List<UIChoicePopup.Option>
                {
                    new UIChoicePopup.Option("도전하기", "강한 현상금 몬스터와 싸워요", new Color(0.62f, 0.25f, 0.7f, 1f)),
                    new UIChoicePopup.Option("패스", "다음 웨이브로 바로 가요", new Color(0.3f, 0.34f, 0.46f, 1f)),
                },
                i => choice = i);

            while (choice < 0 && !game.IsFinished)
                yield return null;

            popup = null;
            if (choice != 0 || game.IsFinished)
                yield break;

            // 기존 현상금 선택 창을 그대로 쓴다. 쿨타임은 도전권이라 무시한다.
            battle.Bounty.ForceReady();
            GameContainer.UI?.Show<UIBountySelectDialog>();

            // 창이 뜨는 데 한두 프레임 걸린다 — 열리기를 기다린 뒤 "닫혔거나 소환됐거나" 를 본다.
            float guard = Time.realtimeSinceStartup + 1f;
            while (!UIBountySelectDialog.IsOpen && Time.realtimeSinceStartup < guard)
                yield return null;

            while (UIBountySelectDialog.IsOpen && battle.Bounty.ActiveCount == 0 && !game.IsFinished)
                yield return null;

            // 소환됐으면 잡을 때까지 기다린다(경고 연출은 소환이 알아서 띄운다). 안 골랐으면 패스와 같다.
            while (battle.Bounty.ActiveCount > 0 && !game.IsFinished)
                yield return null;

            yield return new WaitForSecondsRealtime(0.4f);
        }

        // ── 자리 ──────────────────────────────────────────────────────

        private void GrantSlots()
        {
            int granted = battle.Board != null ? battle.Board.GrantFreeSlots(WaveTheme.SlotsPerGrant) : 0;
            if (granted <= 0)
                Debug.Log("[웨이브 테마] 더 열 자리가 없어 자리 보상을 건너뛴다.");
        }

        private IEnumerator SlotsOrSoul()
        {
            while (!game.IsFinished)
            {
                // 광고 버튼은 늘 보인다. 광고 SDK 가 아직 없어 서비스가 없으면 광고를 본 것으로 친다(아래). 횟수를 다 쓰면 눌리지 않는다.
                bool adLeft = adUses < WaveTheme.AdBonusLimitPerStage;
                var options = new List<UIChoicePopup.Option>
                {
                    new UIChoicePopup.Option("자리 +" + WaveTheme.SlotsPerGrant + "칸", "보드에 놓을 수 있는 자리가 늘어요", new Color(0.86f, 0.6f, 0.12f, 1f)),
                    new UIChoicePopup.Option("영혼 보너스", "SP 또는 마석을 받아요", new Color(0.4f, 0.3f, 0.72f, 1f)),
                };
                options.Add(new UIChoicePopup.Option("광고 보고 둘 다 받기",
                    adLeft ? "남은 횟수 " + (WaveTheme.AdBonusLimitPerStage - adUses) + "/" + WaveTheme.AdBonusLimitPerStage : "이번 스테이지는 다 썼어요",
                    new Color(0.2f, 0.62f, 0.4f, 1f), adLeft));

                int choice = -1;
                popup = UIChoicePopup.Open(popupRoot, font, "보상을 골라요", "5웨이브 클리어!", options, i => choice = i);
                while (choice < 0 && !game.IsFinished)
                    yield return null;

                popup = null;
                if (game.IsFinished)
                    yield break;

                // 다 쓴 광고 칸은 눌리지 않지만, 밸런스 봇처럼 번호로 고르는 쪽이 와도 영혼 보너스로 돌린다.
                if (choice == 2 && !adLeft)
                    choice = 1;

                if (choice == 0)
                {
                    GrantSlots();
                    yield break;
                }

                if (choice == 1)
                {
                    yield return OfferSoulBonus();
                    yield break;
                }

                // 광고: 성공하면 둘 다. 실패하면 고르는 창으로 돌아간다. 에디터에는 광고가 없어 바로 성공으로 친다.
                bool rewarded = false;
                bool failed = false;
                if (GameContainer.RewardedAds != null && GameContainer.RewardedAds.IsAvailable)
                    GameContainer.RewardedAds.Show(() => rewarded = true, () => failed = true);
                else
                    rewarded = true;

                while (!rewarded && !failed && !game.IsFinished)
                    yield return null;

                if (!rewarded)
                    continue;

                adUses++;
                GrantSlots();
                yield return OfferSoulBonus();
                yield break;
            }
        }

        // ── 새 이벤트들 ───────────────────────────────────────────────

        private const int AttackSpeedPercent = 30;
        private const int SlowPercent = 30;
        private const int WeakenPercent = 25;
        private const int RiskyHpPercent = 30;
        private const int RiskyStones = 15;
        private const int SpGambleSure = 50;
        private const int SpGambleWin = 125;
        private const int StoneGambleSure = 6;
        private const int StoneGambleWin = 18;
        private const int FreeSummonCount = 3;
        private const int WallUpgradePercent = 15;
        private const int WallRepairPercent = 25;
        private const int ExchangeSp = 50;
        private const int ExchangeStones = 6;
        private const int MerchantSp = 35;
        private const int MerchantStones = 7;
        private const int MerchantWallPercent = 7;

        private static readonly Color SpToast = new Color(0.55f, 0.9f, 1f, 1f);

        /// <summary>SP 도박: 50 SP 확정 / 50% 로 125 SP.</summary>
        private IEnumerator SpGambleEvent()
        {
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option(SpGambleSure + " SP 받기", "확실하게 받아요", SpColor),
                new UIChoicePopup.Option("도박!", "50% 로 " + SpGambleWin + " SP\n실패하면 아무것도 없어요", DangerColor),
            };

            var result = new int[1];
            yield return Ask("SP 도박", "안전하게 갈까요, 걸어 볼까요?", options, result);
            if (result[0] < 0)
                yield break;

            if (result[0] == 0)
            {
                AddSp(SpGambleSure, Chosen);
                yield return new WaitForSecondsRealtime(0.8f);
                yield break;
            }

            Vector2 from = Chosen;
            if (UnityEngine.Random.Range(0, 100) < 50)
            {
                AddSp(SpGambleWin, from);
                yield return Toast("대성공!", GoodColor);
            }
            else
            {
                yield return Toast("꽝… 아무것도 없어요", DangerColor);
            }
        }

        /// <summary>마석 도박: 6개 확정 / 40% 로 18개.</summary>
        private IEnumerator StoneGambleEvent()
        {
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option("마석 " + StoneGambleSure + "개 받기", "확실하게 받아요", StoneColor),
                new UIChoicePopup.Option("도박!", "40% 로 마석 " + StoneGambleWin + "개\n실패하면 아무것도 없어요", DangerColor),
            };

            var result = new int[1];
            yield return Ask("마석 도박", "안전하게 갈까요, 걸어 볼까요?", options, result);
            if (result[0] < 0)
                yield break;

            if (result[0] == 0)
            {
                AddStones(StoneGambleSure, Chosen);
                yield return new WaitForSecondsRealtime(0.8f);
                yield break;
            }

            Vector2 from = Chosen;
            if (UnityEngine.Random.Range(0, 100) < 40)
            {
                AddStones(StoneGambleWin, from);
                yield return Toast("대성공!", GoodColor);
            }
            else
            {
                yield return Toast("꽝… 아무것도 없어요", DangerColor);
            }
        }

        /// <summary>무료 소환 3회 / 50 SP.</summary>
        private IEnumerator FreeSummonsEvent()
        {
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option("무료 소환 " + FreeSummonCount + "회", "SP 없이 다이스를 뽑아요", GoodColor),
                new UIChoicePopup.Option(WaveTheme.EventSp + " SP 받기", "원하는 때 써요", SpColor),
            };

            var result = new int[1];
            yield return Ask("소환권", "하나를 골라요.", options, result);
            if (result[0] < 0)
                yield break;

            if (result[0] == 0)
            {
                game.Run.FreeSummonCharges += FreeSummonCount;
                yield return Toast("무료 소환 +" + FreeSummonCount, GoodColor);
            }
            else
            {
                AddSp(WaveTheme.EventSp, Chosen);
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }

        /// <summary>확률업 +1레벨(무료) / 50 SP. 이미 최고 레벨이면 앞의 것은 못 고른다.</summary>
        private IEnumerator ProbUpEvent()
        {
            bool canUpgrade = battle.Summon != null && battle.Summon.NextProbUpgradeCost >= 0;
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option("확률업 +1레벨", canUpgrade ? "높은 성급이 더 잘 나와요" : "이미 최고 레벨이에요", GoodColor, canUpgrade),
                new UIChoicePopup.Option(WaveTheme.EventSp + " SP 받기", "원하는 때 써요", SpColor),
            };

            var result = new int[1];
            yield return Ask("확률업", "하나를 골라요.", options, result);
            if (result[0] < 0)
                yield break;

            if (result[0] == 0 && canUpgrade && battle.Summon.GrantProbLevel())
            {
                yield return Toast("확률업 Lv." + battle.Summon.ProbLevel, GoodColor);
            }
            else
            {
                AddSp(WaveTheme.EventSp, Chosen);
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }

        /// <summary>벽 최대 체력 +15%(같은 만큼 회복) / 벽 25% 회복.</summary>
        private IEnumerator WallUpgradeEvent()
        {
            Wall wall = game.wall;
            int bonus = wall != null ? Mathf.Max(1, wall.TotalHp * WallUpgradePercent / 100) : 0;
            int repair = wall != null ? Mathf.Max(1, wall.TotalHp * WallRepairPercent / 100) : 0;
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option("벽 최대 체력 +" + WallUpgradePercent + "%", "+" + bonus + " (그만큼 회복도 해요)", HealColor),
                new UIChoicePopup.Option("벽 " + WallRepairPercent + "% 회복", "+" + repair + " 회복해요", GoodColor),
            };

            var result = new int[1];
            yield return Ask("벽 강화", wall != null ? "벽 체력 " + wall.CurrentHp + " / " + wall.TotalHp : string.Empty, options, result);
            if (result[0] < 0 || wall == null)
                yield break;

            if (result[0] == 0)
            {
                wall.TotalHp += bonus;
                wall.Heal(bonus);
                yield return Toast("벽 최대 체력 +" + bonus, new Color(0.45f, 1f, 0.6f, 1f));
            }
            else
            {
                wall.Heal(repair);
                yield return Toast("벽 체력 +" + repair, new Color(0.45f, 1f, 0.6f, 1f));
            }
        }

        /// <summary>환전: 50 SP 를 내고 마석 6개 / 지나가기.</summary>
        private IEnumerator ExchangeEvent()
        {
            bool canPay = battle.BattlePoints != null && battle.BattlePoints.Get(BattlePointType.SummonPoint) >= ExchangeSp;
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option(ExchangeSp + " SP → 마석 " + ExchangeStones + "개", canPay ? "SP 를 내고 바꿔요" : "SP 가 모자라요", StoneColor, canPay),
                new UIChoicePopup.Option("지나가기", "그대로 가요", new Color(0.3f, 0.34f, 0.46f, 1f)),
            };

            var result = new int[1];
            yield return Ask("환전소", "SP 를 마석으로 바꿀 수 있어요.", options, result);
            if (result[0] != 0 || !canPay)
                yield break;

            if (!battle.BattlePoints.TrySpend(BattlePointType.SummonPoint, ExchangeSp))
                yield break;

            AddStones(ExchangeStones, Chosen);
            yield return new WaitForSecondsRealtime(0.8f);
        }

        /// <summary>
        /// "다음 웨이브 한 번" 효과 / 보상. 효과는 <paramref name="apply"/> 가 건다(<see cref="WaveBuffs"/>).
        /// <paramref name="stonesInstead"/> 가 참이면 다른 쪽이 마석 6개, 아니면 50 SP 다.
        /// </summary>
        private IEnumerator BuffEvent(string title, string buffLabel, string buffSub, System.Action apply, bool stonesInstead)
        {
            string otherLabel = stonesInstead ? "마석 " + StoneGambleSure + "개 받기" : WaveTheme.EventSp + " SP 받기";
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option(buffLabel, buffSub, GoodColor),
                new UIChoicePopup.Option(otherLabel, "지금 받아요", stonesInstead ? StoneColor : SpColor),
            };

            var result = new int[1];
            yield return Ask(title, "하나를 골라요.", options, result);
            if (result[0] < 0)
                yield break;

            if (result[0] == 0)
            {
                apply();
                yield return Toast(buffLabel, GoodColor);
            }
            else if (stonesInstead)
            {
                AddStones(StoneGambleSure, Chosen);
                yield return new WaitForSecondsRealtime(0.8f);
            }
            else
            {
                AddSp(WaveTheme.EventSp, Chosen);
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }

        /// <summary>위험한 거래: 다음 웨이브 몬스터 체력 +30% 를 받으면 마석 15개 / 지나가기.</summary>
        private IEnumerator RiskyDealEvent()
        {
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option("거래한다", "다음 웨이브 몬스터 체력 +" + RiskyHpPercent + "%\n대신 마석 " + RiskyStones + "개", DangerColor),
                new UIChoicePopup.Option("지나가기", "그대로 가요", new Color(0.3f, 0.34f, 0.46f, 1f)),
            };

            var result = new int[1];
            yield return Ask("위험한 거래", "몬스터가 단단해지는 대신 마석을 줘요.", options, result);
            if (result[0] != 0)
                yield break;

            WaveBuffs.AddMonsterHpPercent(RiskyHpPercent);
            AddStones(RiskyStones, Chosen);
            yield return Toast("몬스터 체력 +" + RiskyHpPercent + "%", DangerColor);
        }

        /// <summary>상인: SP 70 / 마석 7개 / 벽 7% 회복 중 하나.</summary>
        private IEnumerator MerchantEvent()
        {
            Wall wall = game.wall;
            int repair = wall != null ? Mathf.Max(1, wall.TotalHp * MerchantWallPercent / 100) : 0;
            var options = new List<UIChoicePopup.Option>
            {
                new UIChoicePopup.Option(MerchantSp + " SP", "다이스를 뽑아요", SpColor),
                new UIChoicePopup.Option("마석 " + MerchantStones + "개", "진화·강화에 써요", StoneColor),
                new UIChoicePopup.Option("벽 " + MerchantWallPercent + "% 회복", "+" + repair + " 회복해요", HealColor),
            };

            var result = new int[1];
            yield return Ask("떠돌이 상인", "하나를 골라 가져가요.", options, result);
            if (result[0] < 0)
                yield break;

            switch (result[0])
            {
                case 0:
                    AddSp(MerchantSp, Chosen);
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                case 1:
                    AddStones(MerchantStones, Chosen);
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                default:
                    if (wall != null)
                    {
                        wall.Heal(repair);
                        yield return Toast("벽 체력 +" + repair, new Color(0.45f, 1f, 0.6f, 1f));
                    }

                    break;
            }
        }
    }
}
