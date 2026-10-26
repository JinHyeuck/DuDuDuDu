using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.Dice;

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

        private UIChoicePopup popup;
        private int adUses;

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

            Strip = UIWaveStrip.Create(canvas, font, totalWaves);
            Banner = UIWaveStartBanner.Create(canvas, font);
            EnemyBar = UIEnemyBar.Create(canvas, font, battle.Monsters, bossTimeLeft);
            Strip.SetWave(1);
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
            switch (WaveTheme.After(wave))
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
            }
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
    }
}
