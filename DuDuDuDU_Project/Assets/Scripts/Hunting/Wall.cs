using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OJ.Core;
using OJ.Point;
using OJ.Relic;

using OJ.DI;
using VContainer;

namespace OJ.Hunting
{
    public class Wall : MonoBehaviour
    {
        /// <summary>
        /// 벽 체력은 <see cref="RunState"/> 가 소유한다. (6.1)
        ///
        /// 예전에는 <c>Wall</c> 과 <c>GameManager</c> 가 각자 들고 있었다 —
        /// <c>GameManager.WallHp</c> 는 최대치, <c>Wall.CurrentHp</c> 는 현재치라
        /// 이름만으로는 어느 쪽이 무엇인지 알 수 없었고, 판을 리셋하려면 둘 다 건드려야 했다.
        ///
        /// 쓰기가 이 파일 안에만 있어서(전수 확인) 위임이 안전하다. 읽기는 8곳이 있는데
        /// 전부 그대로 동작한다.
        ///
        /// 전투가 없으면(배틀 씬 밖, 또는 스코프가 서기 전) 예전처럼 자기 필드를 쓴다 —
        /// 여기서 NRE 를 내면 프리팹 미리보기 같은 곳이 죽는다.
        ///
        /// <b>8.3b: 이 가드는 지우면 안 된다.</b> 이 프로퍼티는 다른 컴포넌트의
        /// <c>Awake</c> 에서도 읽힐 수 있는데, 창구는 씬의 모든 <c>Awake</c> 뒤에
        /// 채워지므로 그 순간에는 아직 비어 있다.
        /// </summary>
        private readonly RunState fallbackRun = new RunState();

        // 8.3b: 배틀 스코프가 씬을 훑으며 채운다.
        [Inject] private IBattleRefs battle;

        private RunState Run =>
            battle != null && battle.IsActive ? battle.Game.Run : fallbackRun;

        public int TotalHp { get => Run.WallMaxHp; set => Run.WallMaxHp = value; }

        public int CurrentHp { get => Run.WallHp; set => Run.WallHp = value; }

        public TMP_Text CurrentHp_Text;
        public RectTransform wallHp_RectTrans;
        public float wallHp_Width = 1025.0f;


        public void SetInit(int hp)
        {
            TotalHp = hp;
            CurrentHp = hp;

            SetHpLabel(hp);
        }

        public void TakeDamage(int dmg)
        {
            if (PointCheatController.IsWallInvincible)
                return;

            // 벽은 몬스터와 식이 다르다 — 방어력도, 상태 피해증가도, CeilToInt 도 없다.
            CurrentHp = IncomingDamageFormula.WallHpAfterDamage(CurrentHp, dmg);

            SetHpLabel(CurrentHp);

            // 피해 경로 전용 비율식이다. TotalHp == 0 이면 NaN 이 나오고 그대로 sizeDelta.x 에
            // 들어간다(SetInit(0) 이 불리면 재현된다). 부활 / Heal 경로와 식이 달라 함수도 다르다 —
            // 합치면 산술 변경이다.
            SetHpBar(IncomingDamageFormula.WallHpBarRatioOnDamage(CurrentHp, TotalHp));

            if (CurrentHp <= 0)
            {
                if (RelicManager.Instance != null && RelicManager.Instance.TryTriggerLastWall())
                {
                    CurrentHp = 1;
                    SetHpLabel(CurrentHp);

                    // 부활 경로만 TotalHp > 0 가드와 Clamp01 을 갖는다. 위 피해 경로에는 둘 다 없다.
                    SetHpBar(IncomingDamageFormula.WallHpBarRatioClamped(CurrentHp, TotalHp));
                    return;
                }

                // <b>여기서 벽을 부수지 않는다.</b> 되돌리기를 제안할 수 있고, 그러려면
                // 벽이 살아 있어야 한다 — 부순 뒤에는 되돌릴 대상 자체가 없다.
                // 무엇을 할지(제안할지, 그대로 끝낼지)와 언제 부술지는 GameManager 가 정한다.
                battle.Game.OnWallDestroyed();
            }
        }

        public void Heal(int value)
        {
            if (value <= 0 || CurrentHp <= 0)
                return;

            CurrentHp += value;
            if (CurrentHp > TotalHp)
                CurrentHp = TotalHp;

            SetHpLabel(CurrentHp);
            SetHpBar(IncomingDamageFormula.WallHpBarRatioClamped(CurrentHp, TotalHp));
        }

        /// <summary>
        /// 체력을 스냅샷 값으로 되돌린다. 웨이브 되돌리기가 부른다.
        ///
        /// <b><see cref="SetInit"/> 를 쓸 수 없다.</b> 저쪽은 <see cref="TotalHp"/> 까지 같이
        /// 덮으므로, 최대 체력을 올리는 유물이 걸린 판에서 되돌리면 <b>그 증가분이 사라진다</b>.
        /// 여기서 움직이는 것은 현재 체력 하나뿐이다.
        ///
        /// <b><see cref="Heal"/> 도 쓸 수 없다.</b> 저쪽은 <c>CurrentHp &lt;= 0</c> 이면
        /// 그냥 돌아간다 — 되돌리기가 가장 필요한 순간(벽이 0 이 된 직후)에 아무 일도
        /// 안 한다는 뜻이다.
        ///
        /// 비율식은 <c>Clamped</c> 쪽을 쓴다. 피해 경로의 식은 <c>TotalHp == 0</c> 가드가
        /// 없어 NaN 이 나올 수 있고, 여기는 피해가 아니라 회복 성격이라 부활·회복과 같은 편이다.
        /// </summary>
        public void RestoreHp(int currentHp)
        {
            CurrentHp = Mathf.Clamp(currentHp, 0, TotalHp);

            SetHpLabel(CurrentHp);
            SetHpBar(IncomingDamageFormula.WallHpBarRatioClamped(CurrentHp, TotalHp));
        }

        // --- 표시 (5.4) ------------------------------------------------------------------
        //
        // 표시 호출을 두 메서드로 모았다. 예전에는 SetText 3곳 · sizeDelta 3곳이 규칙 사이에
        // 흩어져 있어서, 어느 경로가 UI 를 갱신하고 어느 경로가 빠뜨리는지 읽어 내기 어려웠다.
        //
        // <b>비율식은 여기 넣지 않는다.</b> 피해 경로(가드 없음, NaN 가능)와 부활·회복 경로
        // (TotalHp > 0 가드 + Clamp01)가 서로 다른 식이라 호출부가 어느 쪽인지 정해야 한다.
        // 하나로 합치면 산술 변경이다.

        private void SetHpLabel(int hp)
        {
            if (CurrentHp_Text != null)
                CurrentHp_Text.SetText("{0}", hp);
        }

        private void SetHpBar(float ratio)
        {
            // null 가드를 넣지 않는다. 원본이 그랬다 — wallHp_RectTrans 가 비어 있으면 NRE 로
            // 즉시 드러나는 것이 맞다. 여기서 조용히 넘어가게 만들면 2단계에서 봉인한
            // "배선 사고가 침묵으로 흡수되는" 경로를 새로 만드는 셈이다.
            Vector2 size = wallHp_RectTrans.sizeDelta;
            size.x = wallHp_Width * ratio;
            wallHp_RectTrans.sizeDelta = size;
        }
    }

}
