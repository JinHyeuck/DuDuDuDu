using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using OJ.DI;
using OJ.Hunting;
using VContainer;

namespace OJ.Dice
{
    public class UIDice : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        // 8.3b: 배틀 스코프가 채운다. 이 다이스는 UIBoard.SpawnDice 가 런타임에 찍는
        // 프리팹이라 스코프의 씬 순회에는 잡히지 않고, 생성부의 resolver.Instantiate 가
        // 찍는 그 순간에 주입된다.
        //
        // <b>이 경로는 Awake 에서도 안전하다.</b> VContainer 의 부모 있는 Instantiate 는
        // 프리팹을 SetActive(false) 로 껐다 찍고, 주입한 뒤에 다시 켠다
        // (ObjectResolverUnityExtensions.cs:78-91). 클론이 꺼진 채로 태어나므로
        // Awake 는 주입이 끝난 뒤에야 돈다.
        //
        // <b>씬에 놓인 컴포넌트는 반대다</b> — 그쪽은 스코프가 sceneLoaded 에서 훑으므로
        // 자기 Awake 뒤에 채워진다. 같은 [Inject] 라도 태어난 경로에 따라 시점이 갈린다.
        // 여기 사용처는 전부 Init·Update·드래그/클릭 핸들러라 어느 쪽이든 지난 뒤다.
        // null 이면 그것은 사고이니 ?. 를 새로 붙이지 않는다.
        [Inject] private IBattleRefs battle;

        private const float DoubleTapThreshold = 0.35f;

        public Image BGImage;
        public Image Icon;
        public Transform ShootEffectTrans;
        public Animator ShootEffectAni;
        public Image ShootEffectImage;
        public Image CooldownFill;
        public TMP_Text CooldownText;
        public Image StarImage;
        public TMP_Text StarText;
        public TMP_Text TypeText;
        public Animator animator;

        public DiceType Type { get; private set; }
        public int Star { get; private set; }
        public int SlotIndex { get; private set; }

        private Transform originalParent;
        private Vector3 originalPos;
        private CanvasGroup canvasGroup;
        private Canvas canvas;
        private float lastPointerClickTime = -10f;
        private int clickSequence;

        public void Init(DiceType type, int star, int slotIndex)
        {
            Type = type;
            Star = star;
            SlotIndex = slotIndex;
            Refresh();
        }

        private void Awake()
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvas = GetComponentInParent<Canvas>();
        }

        private void Update()
        {
            UpdateCooldownFill();
        }

        private void OnDestroy()
        {
            // 원래도 null 을 봤다. 창구가 비어서가 아니라 씬을 나갈 때 UIBoard 가 나보다
            // 먼저 파괴되는 정상적인 종료 순서가 있기 때문이다. 그 검사는 그대로 둔다.
            // 한 번만 읽어 지역에 담는 것은, 종료 중에 창구가 비워지면 같은 식이
            // 줄마다 다른 답을 낼 수 있어서다.
            UIBoard board = battle.Board;
            if (board == null || board.diceMap == null)
                return;

            if (SlotIndex < 0 || SlotIndex >= board.diceMap.Length)
                return;

            if (board.diceMap[SlotIndex] == this)
                board.diceMap[SlotIndex] = null;
        }

        public void Refresh()
        {
            bool showStarUI = DiceMetaDataProvider.ShowStarUI(Type);
            if (StarText != null)
            {
                StarText.gameObject.SetActive(showStarUI);
                if (showStarUI)
                    StarText.SetText("x{0}", Star);
            }

            DiceType diceType = Type;

            Color typeColor = DiceMetaDataProvider.GetColor(diceType);
            Sprite typeSprite = DiceMetaDataProvider.GetIcon(diceType);

            if (ShootEffectImage != null)
                ShootEffectImage.color = typeColor;

            if (ShootEffectTrans != null)
                ShootEffectTrans.gameObject.SetActive(false);

            if (Icon != null && typeSprite != null)
                Icon.sprite = typeSprite;

            if (Star >= MergeSystem.MaxStar && animator != null)
                animator.SetBool("MaxStar", true);
            else if (animator != null)
                animator.SetBool("MaxStar", false);

            UpdateCooldownFill();
        }

        public void SetStar(int star)
        {
            Star = star;
            Refresh();
        }

        public void SetSlotIndex(int slotIndex)
        {
            SlotIndex = slotIndex;
        }

        private float _hideEffectTime = 0.0f;

        public void PlayLevelUpEffect()
        {
            _hideEffectTime = Time.time + 0.5f;
            AutoHideEffect().Forget();
        }

        private async UniTask AutoHideEffect()
        {
            if (ShootEffectTrans != null)
                ShootEffectTrans.gameObject.SetActive(false);

            await UniTask.NextFrame();

            if (ShootEffectTrans != null)
                ShootEffectTrans.gameObject.SetActive(true);

            float myhidetime = Time.time + 0.6f;

            while (myhidetime > Time.time)
            {
                await UniTask.NextFrame();
            }

            if (_hideEffectTime > Time.time)
                return;

            if (ShootEffectTrans != null)
                ShootEffectTrans.gameObject.SetActive(false);
        }

        private void UpdateCooldownFill()
        {
            if (CooldownFill == null)
                return;

            float fill = 0f;
            float remain = 0f;

            // Update 마다 도는 자리다. 원래 있던 null 검사는 유지한다 — Init 이 부르는
            // Refresh 경로와 씬 종료 프레임이 이 검사에 기대고 있다.
            PlayerController player = battle.Player;
            if (player != null)
            {
                fill = player.GetDiceCooldownFill(this);
                remain = player.GetDiceCooldownRemaining(this);
            }

            CooldownFill.fillAmount = fill;
            CooldownFill.enabled = fill > 0.001f;

            if (CooldownText != null)
            {
                bool visible = remain > 0.01f;
                CooldownText.gameObject.SetActive(visible);
                if (visible)
                    CooldownText.SetText("{0:0.0}", remain);
            }
        }

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (battle.Game.inGameState == InGameState.Wave)
                return;

            originalParent = transform.parent;
            originalPos = transform.localPosition;

            transform.SetParent(canvas.transform, true);
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (battle.Game.inGameState == InGameState.Wave)
                return;

            if (canvas == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, eventData.position, canvas.worldCamera, out Vector2 pos);
            transform.position = canvas.transform.TransformPoint(pos);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (battle.Game.inGameState == InGameState.Wave)
                return;

            canvasGroup.blocksRaycasts = true;

            GameObject hitObj = eventData.pointerCurrentRaycast.gameObject;
            if (hitObj != null)
            {
                UIRemoveDice uIRemoveDice = hitObj.GetComponentInParent<UIRemoveDice>();
                if (uIRemoveDice != null)
                {
                    uIRemoveDice.RemoveDice(this);
                    return;
                }

                // 원래 있던 board null 검사를 유지하려고 한 번만 읽어 지역에 담는다.
                UIBoard board = battle.Board;

                UIDice targetDice = hitObj.GetComponentInParent<UIDice>();
                if (targetDice != null && targetDice != this)
                {
                    bool merged = battle.Merge.TryMerge(this, targetDice);
                    if (merged)
                        return;

                    if (board != null && board.TrySwapDice(this, targetDice))
                        return;
                }

                if (board != null)
                {
                    int slotIndex = board.GetSlotIndexFromObject(hitObj);
                    if (slotIndex >= 0 && board.TryMoveDiceToSlot(this, slotIndex))
                        return;
                }
            }

            transform.SetParent(originalParent);
            transform.localPosition = originalPos;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            GameManager game = battle.Game;
            InGameState state = game != null ? game.inGameState : InGameState.None;

            if (state == InGameState.Wave)
            {
                battle.Board?.OpenBattleDiceDetail(this);
                return;
            }

            bool isDoubleClick = eventData.clickCount >= 2;
            bool isDoubleTap = Time.unscaledTime - lastPointerClickTime <= DoubleTapThreshold;

            if (isDoubleClick || isDoubleTap)
            {
                clickSequence++;
                lastPointerClickTime = -10f;
                TryAutoMergeSameDice();
                return;
            }

            lastPointerClickTime = Time.unscaledTime;

            if (state == InGameState.Setting)
                HandleSingleClick(++clickSequence).Forget();
        }

        private void TryAutoMergeSameDice()
        {
            if (battle.Game.inGameState == InGameState.Wave)
                return;

            MergeSystem merge = battle.Merge;
            UIBoard board = battle.Board;

            if (merge == null || board == null || board.diceMap == null)
                return;

            if (!DiceMetaDataProvider.CanMerge(Type))
                return;

            if (Star >= MergeSystem.MaxStar)
                return;

            DiceType myType = Type;
            UIDice target = null;
            UIDice[] map = board.diceMap;

            for (int i = 0; i < map.Length; i++)
            {
                UIDice candidate = map[i];
                if (candidate == null || candidate == this)
                    continue;

                if (candidate.Star != Star || candidate.Star >= MergeSystem.MaxStar)
                    continue;

                if (candidate.Type != myType)
                    continue;

                target = candidate;
                break;
            }

            if (target != null)
                merge.TryMerge(this, target);
        }

        private async UniTaskVoid HandleSingleClick(int sequence)
        {
            await UniTask.Delay(
                Mathf.RoundToInt(DoubleTapThreshold * 1000f),
                DelayType.UnscaledDeltaTime,
                PlayerLoopTiming.Update);

            if (sequence != clickSequence)
                return;

            // 0.35초를 기다린 뒤라 그 사이 씬이 내려갔을 수 있다. 원래의 null 검사가
            // 그 경우를 막고 있었으니 창구 뒤를 보는 형태로만 옮긴다.
            GameManager game = battle.Game;
            if (game == null || game.inGameState != InGameState.Setting)
                return;

            battle.Board?.OpenBattleDiceDetail(this);
        }

        #endregion
    }
}
