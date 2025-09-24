using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

namespace OJ
{
    public class UIDice : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Image BGImage;          // 다이스 배경
        public Image Icon;
        public Transform ShootEffectTrans;
        public Animator ShootEffectAni;
        public Image ShootEffectImage;
        public TMP_Text StarText;
        public TMP_Text TypeText;
        public Animator animator;

        public List<DiceType> Type { get; private set; }
        public int Star { get; private set; }
        public int SlotIndex { get; private set; }

        private Transform originalParent;
        private Vector3 originalPos;
        private CanvasGroup canvasGroup;
        private Canvas canvas;

        public void Init(List<DiceType> type, int star, int slotIndex)
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

        public void Refresh()
        {
            StarText.SetText("Lv.{0}", Star);
            //TypeText.text = Type.ToString();

            DiceType diceType = Type[0];

            Color typeColor = StaticResource.Instance.DiceTypeResourceManager.GetColor(diceType);
            Sprite typeSprite = StaticResource.Instance.DiceTypeResourceManager.GetIcon(diceType);

            if (ShootEffectImage != null)
                ShootEffectImage.color = typeColor;

            if (ShootEffectTrans != null)
                ShootEffectTrans.gameObject.SetActive(false);

            if (Icon != null)
            {
                if (typeSprite != null) Icon.sprite = typeSprite;
            }

            if (Star >= MergeSystem.MaxStar && animator != null)
                animator.SetBool("MaxStar", true);
            else if (animator != null)
                animator.SetBool("MaxStar", false);
        }

        public void SetStar(int star)
        {
            Star = star;
            Refresh();
        }
        //------------------------------------------------------------------------------------
        private float _hideEffectTime = 0.0f;
        //------------------------------------------------------------------------------------
        public void PlayLevelUpEffect()
        {
            _hideEffectTime = Time.time + 0.5f;
            AutoHideEffect().Forget();
            return;

            if (ShootEffectTrans != null)
            {
                ShootEffectTrans.gameObject.SetActive(true);
                if (ShootEffectAni != null)
                {
                    ShootEffectAni.enabled = false;
                    ShootEffectAni.enabled = true;
                    ShootEffectAni.Play(0);
                }

                if (_hideEffectTime > Time.time)
                {
                    _hideEffectTime = Time.time + 1.0f;
                }
                else
                {

                }
            }
        }
        //------------------------------------------------------------------------------------
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
            {
                ShootEffectTrans.gameObject.SetActive(false);
            }
        }
        //------------------------------------------------------------------------------------

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (GameManager.Instance.inGameState == InGameState.Wave)
                return;


            originalParent = transform.parent;
            originalPos = transform.localPosition;

            transform.SetParent(canvas.transform, true); // 최상위 캔버스로 이동
            canvasGroup.blocksRaycasts = false;          // Raycast 무시해서 자기 자신이 Drop 타겟 막지 않게
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (GameManager.Instance.inGameState == InGameState.Wave)
                return;

            if (canvas == null) return;
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, eventData.position, canvas.worldCamera, out pos);
            transform.position = canvas.transform.TransformPoint(pos);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (GameManager.Instance.inGameState == InGameState.Wave)
                return;

            canvasGroup.blocksRaycasts = true;

            // 머지 시도
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
                        return; // 머지 성공하면 드래그 다이스 파괴됐으므로 종료
                }
            }

            // 드래그 실패 시 원래 자리로 복귀
            transform.SetParent(originalParent);
            transform.localPosition = originalPos;
        }

        #endregion
    }

}
