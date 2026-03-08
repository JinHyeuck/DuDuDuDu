using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

namespace OJ
{
    public class UIDice : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public Image BGImage;
        public Image Icon;
        public Transform ShootEffectTrans;
        public Animator ShootEffectAni;
        public Image ShootEffectImage;
        public Image CooldownFill;
        public TMP_Text CooldownText;
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
            if (UIBoard.Instance == null || UIBoard.Instance.diceMap == null)
                return;

            if (SlotIndex < 0 || SlotIndex >= UIBoard.Instance.diceMap.Length)
                return;

            if (UIBoard.Instance.diceMap[SlotIndex] == this)
                UIBoard.Instance.diceMap[SlotIndex] = null;
        }

        public void Refresh()
        {
            bool showStarUI = DiceMetaDataProvider.ShowStarUI(Type);
            if (StarText != null)
            {
                StarText.gameObject.SetActive(showStarUI);
                if (showStarUI)
                    StarText.SetText("Lv.{0}", Star);
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
            if (PlayerController.Instance != null)
            {
                fill = PlayerController.Instance.GetDiceCooldownFill(this);
                remain = PlayerController.Instance.GetDiceCooldownRemaining(this);
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
            if (GameManager.Instance.inGameState == InGameState.Wave)
                return;

            originalParent = transform.parent;
            originalPos = transform.localPosition;

            transform.SetParent(canvas.transform, true);
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (GameManager.Instance.inGameState == InGameState.Wave)
                return;

            if (canvas == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, eventData.position, canvas.worldCamera, out Vector2 pos);
            transform.position = canvas.transform.TransformPoint(pos);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (GameManager.Instance.inGameState == InGameState.Wave)
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

                UIDice targetDice = hitObj.GetComponentInParent<UIDice>();
                if (targetDice != null && targetDice != this)
                {
                    bool merged = MergeSystem.Instance.TryMerge(this, targetDice);
                    if (merged)
                        return;

                    if (UIBoard.Instance != null && UIBoard.Instance.TrySwapDice(this, targetDice))
                        return;
                }

                if (UIBoard.Instance != null)
                {
                    int slotIndex = UIBoard.Instance.GetSlotIndexFromObject(hitObj);
                    if (slotIndex >= 0 && UIBoard.Instance.TryMoveDiceToSlot(this, slotIndex))
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

            if (eventData.clickCount < 2)
                return;

            TryAutoMergeSameDice();
        }

        private void TryAutoMergeSameDice()
        {
            if (GameManager.Instance.inGameState == InGameState.Wave)
                return;

            if (MergeSystem.Instance == null || UIBoard.Instance == null || UIBoard.Instance.diceMap == null)
                return;

            if (!DiceMetaDataProvider.CanMerge(Type))
                return;

            if (Star >= MergeSystem.MaxStar)
                return;

            DiceType myType = Type;
            UIDice target = null;
            UIDice[] map = UIBoard.Instance.diceMap;

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
                MergeSystem.Instance.TryMerge(this, target);
        }

        #endregion
    }
}
