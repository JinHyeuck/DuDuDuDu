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

        /// <summary>
        /// 진화 가능 표시가 보는 보유 판정. MergeSystem 의 진화 검사와 같은 것이다.
        /// 생성 경로가 resolver.Instantiate 라 Awake 이전에 채워진다(위 battle 주석).
        /// </summary>
        [Inject] private DiceOwnershipManager ownership;

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
            SetUpCooldownGauge();
        }

        private void Update()
        {
            UpdateCooldownFill();
            AnimateEvolveBadge();
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
            // 성급이 그림으로 바뀌는 다이스는 숫자를 끈다 — 같은 정보가 두 번 보인다.
            bool showStarUI = DiceMetaDataProvider.ShowStarUI(Type) && !DiceMetaDataProvider.HasStarIcons(Type);
            if (StarText != null)
            {
                StarText.gameObject.SetActive(showStarUI);
                if (showStarUI)
                    StarText.SetText("x{0}", Star);
            }

            DiceType diceType = Type;

            Color typeColor = DiceMetaDataProvider.GetColor(diceType);
            Sprite typeSprite = DiceMetaDataProvider.GetIcon(diceType, Star);

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
            RefreshEvolveBadge();
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

        // ── 쿨타임 게이지(테두리) · 진화 가능 표시 ─────────────────────────

        /// <summary>
        /// 진화 가능 화살표의 크기와, 우상단 모서리에서 안쪽으로 들어온 거리. 다이스 루트 사각형은 보이는 틀보다 크다 -
        /// 모서리에 붙이면 틀 밖으로 나가 옆 다이스에 가려지므로 틀 안쪽까지 들인다.
        /// </summary>
        private const float EvolveBadgeSize = 52f;
        private const float EvolveBadgeInset = 26f;

        /// <summary>
        /// 링을 다이스 틀에 맞추려 루트 사각형에서 들이는 거리. 루트 사각형(170)은 눈에 보이는 틀(칸 크기 120)보다 커서
        /// 가장자리에 그리면 링이 틀 밖으로 나간다. 틀은 가운데에 있으므로 양쪽으로 (170 - 120) / 2 만큼 들인다.
        /// </summary>
        private const float CooldownRingInset = 25f;

        private static Sprite cooldownRingSprite;
        private static Sprite evolveArrowSprite;
        private GameObject evolveBadge;
        private RectTransform evolveBadgeRect;

        /// <summary>
        /// 쿨타임 표시를 테두리 게이지로 바꾼다. 예전에는 칸 전체를 반투명 검정으로 덮고 숫자까지 올려 어떤 다이스인지
        /// 가렸다. 이제 숫자는 없고, 다이스는 그대로 보이며, 테두리만 시계 방향으로 차오른다.
        /// 프리팹은 건드리지 않고 코드로 바꾼다 - 이미 Filled/Radial360 이라 그림과 색만 갈아 끼우면 된다.
        /// </summary>
        private void SetUpCooldownGauge()
        {
            if (CooldownText != null)
                CooldownText.gameObject.SetActive(false);

            if (CooldownFill == null)
                return;

            CooldownFill.sprite = GetCooldownRingSprite();
            CooldownFill.type = Image.Type.Filled;
            CooldownFill.fillMethod = Image.FillMethod.Radial360;
            CooldownFill.fillOrigin = (int)Image.Origin360.Top;
            // 밝은 금색 호가 쿨타임이 찰수록 시계 방향으로 늘어난다. 어두운 색은 검은 배경에서 안 보이고
            // 흰색은 연한 노멀 다이스 틀에서 안 보인다. 발사 가능해지면(다 차면) 사라진다.
            CooldownFill.color = new Color(1f, 0.9f, 0.3f, 0.95f);
            CooldownFill.raycastTarget = false;

            // 링을 눈에 보이는 틀에 맞춘다(CooldownRingInset 주석).
            var rect = (RectTransform)CooldownFill.transform;
            rect.offsetMin = new Vector2(CooldownRingInset, CooldownRingInset);
            rect.offsetMax = new Vector2(-CooldownRingInset, -CooldownRingInset);
        }

        /// <summary>둥근 사각 테두리(속이 빈 틀) 한 장. 프로세스에서 한 번만 만든다.</summary>
        private static Sprite GetCooldownRingSprite()
        {
            if (cooldownRingSprite != null)
                return cooldownRingSprite;

            const int size = 128;
            const float outerRadius = 14f;
            const float thickness = 8f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "DiceCooldownRing",
            };

            float half = size * 0.5f;
            float innerRadius = Mathf.Max(2f, outerRadius - thickness);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;

                    float outer = RoundedBoxDistance(px, py, half, outerRadius);
                    float inner = RoundedBoxDistance(px, py, half - thickness, innerRadius);

                    // 안쪽은 양수(구멍), 바깥은 음수. 가장자리를 한 픽셀 폭으로 부드럽게 한다.
                    float alpha = Mathf.Clamp01(0.5f - outer) * Mathf.Clamp01(inner + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            // FullRect 가 중요하다. 기본값(Tight)은 투명한 가운데를 도려낸 모양으로 메시를 만들어,
            // Image 가 그 모양의 경계 상자를 기준으로 그리면서 링이 다이스 밖으로 밀려난다.
            cooldownRingSprite = Sprite.Create(
                texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return cooldownRingSprite;
        }

        /// <summary>중심이 원점인 둥근 사각형까지의 부호 있는 거리(안쪽이 음수).</summary>
        private static float RoundedBoxDistance(float x, float y, float halfExtent, float radius)
        {
            float qx = Mathf.Abs(x) - (halfExtent - radius);
            float qy = Mathf.Abs(y) - (halfExtent - radius);
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>
        /// 진화할 수 있는 다이스의 우상단에 위로 향한 화살표를 띄운다. 글자 대신 그림이라 칸을 가리지 않는다.
        /// 기본 다이스는 3성이 되고 대상(특수) 다이스를 갖고 있을 때, 레어(특수) 다이스는 대상(킹)을 갖고 있을 때다 -
        /// 실제로 진화할 수 있을 때만 보인다(<see cref="MergeSystem"/> 과 같은 판정). 탑에는 진화가 없어 끈다.
        /// </summary>
        private void RefreshEvolveBadge()
        {
            bool show = battle != null
                && !battle.Tower.IsActive
                && DiceEvolution.CanEvolve(Type, Star, ownership != null ? ownership.IsOwned : null);

            if (!show)
            {
                if (evolveBadge != null)
                    evolveBadge.SetActive(false);

                return;
            }

            if (evolveBadge == null)
            {
                evolveBadge = CreateEvolveBadge();
                evolveBadgeRect = (RectTransform)evolveBadge.transform;
            }

            evolveBadge.SetActive(true);
        }

        /// <summary>화살표가 위아래로 살짝 떠다니며 눈에 띄게 한다. 보일 때만 돈다.</summary>
        private void AnimateEvolveBadge()
        {
            if (evolveBadge == null || !evolveBadge.activeSelf)
                return;

            float bob = Mathf.Sin(Time.unscaledTime * 5f) * 4f;
            evolveBadgeRect.anchoredPosition = new Vector2(-EvolveBadgeInset, -EvolveBadgeInset + bob);
        }

        private GameObject CreateEvolveBadge()
        {
            var badge = new GameObject("EvolveBadge", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)badge.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-EvolveBadgeInset, -EvolveBadgeInset);
            rect.sizeDelta = new Vector2(EvolveBadgeSize, EvolveBadgeSize);

            var image = badge.GetComponent<Image>();
            image.sprite = GetEvolveArrowSprite();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return badge;
        }

        /// <summary>
        /// 위로 향한 화살표(금색 + 검은 외곽선) 한 장. 프로세스에서 한 번만 만든다.
        /// 외곽선은 화살표 모양을 한 번 부풀린 것이라 어떤 다이스 배경 위에서도 읽힌다.
        /// </summary>
        private static Sprite GetEvolveArrowSprite()
        {
            if (evolveArrowSprite != null)
                return evolveArrowSprite;

            const int size = 96;
            const float outline = 6f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "DiceEvolveArrow",
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = ArrowDistance(x + 0.5f, y + 0.5f, size);

                    float fill = Mathf.Clamp01(0.5f - d);
                    float edge = Mathf.Clamp01(0.5f - (d - outline));

                    // 위쪽이 밝은 금색이라 입체감이 생긴다.
                    float t = y / (float)(size - 1);
                    var gold = Color.Lerp(new Color(1f, 0.66f, 0.1f), new Color(1f, 0.95f, 0.45f), t);

                    float alpha = Mathf.Max(fill, edge);
                    Color c = Color.Lerp(new Color(0.08f, 0.04f, 0f), gold, fill);
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.RoundToInt(c.r * 255f),
                        (byte)Mathf.RoundToInt(c.g * 255f),
                        (byte)Mathf.RoundToInt(c.b * 255f),
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            evolveArrowSprite = Sprite.Create(
                texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return evolveArrowSprite;
        }

        /// <summary>
        /// 위로 향한 화살표(삼각형 머리 + 몸통)까지의 부호 있는 거리 근사(안쪽이 음수). 좌표는 픽셀, y 가 위쪽이다.
        /// </summary>
        private static float ArrowDistance(float px, float py, int size)
        {
            float cx = size * 0.5f;
            float x = px - cx;

            // 머리: 꼭짓점 위(0.84), 밑변 아래(0.46) 의 이등변삼각형. 몸통: 폭 0.30, 아래 0.14 ~ 머리 밑변.
            float headTop = size * 0.84f;
            float headBottom = size * 0.46f;
            float headHalfWidth = size * 0.36f;
            float stemHalfWidth = size * 0.15f;
            float stemBottom = size * 0.14f;

            // 삼각형: 높이에 따라 반폭이 줄어드는 선 사이의 거리.
            float headSpan = headTop - headBottom;
            float headHalfAtY = headHalfWidth * Mathf.Clamp01((headTop - py) / headSpan);
            float headSide = Mathf.Abs(x) - headHalfAtY;
            float headVertical = Mathf.Max(headBottom - py, py - headTop);
            float head = Mathf.Max(headSide * 0.8f, headVertical);

            float stemSide = Mathf.Abs(x) - stemHalfWidth;
            float stemVertical = Mathf.Max(stemBottom - py, py - (headBottom + 2f));
            float stem = Mathf.Max(stemSide, stemVertical);

            return Mathf.Min(head, stem);
        }

        private void UpdateCooldownFill()
        {
            if (CooldownFill == null)
                return;

            float fill = 0f;

            // Update 마다 도는 자리다. 원래 있던 null 검사는 유지한다 — Init 이 부르는
            // Refresh 경로와 씬 종료 프레임이 이 검사에 기대고 있다.
            PlayerController player = battle.Player;
            if (player != null)
            {
                fill = player.GetDiceCooldownFill(this);
            }

            // fill 은 <b>남은</b> 비율이다. 게이지는 찬 비율로 보여 준다 - 발사 직후 비어 있다가 차오르고, 다 차면 꺼진다.
            CooldownFill.fillAmount = 1f - fill;
            CooldownFill.enabled = fill > 0.001f;
        }

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isMerging || battle.Game.IsBoardLockedByWave)
                return;

            originalParent = transform.parent;
            originalPos = transform.localPosition;

            transform.SetParent(canvas.transform, true);
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (battle.Game.IsBoardLockedByWave)
                return;

            if (canvas == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, eventData.position, canvas.worldCamera, out Vector2 pos);
            transform.position = canvas.transform.TransformPoint(pos);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (battle.Game.IsBoardLockedByWave)
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
            if (eventData.button != PointerEventData.InputButton.Left || isMerging)
                return;

            GameManager game = battle.Game;
            InGameState state = game != null ? game.inGameState : InGameState.None;

            if (state == InGameState.Wave && !game.IsContinuousWave)
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

            if (game != null && game.CanManageBoard)
                HandleSingleClick(++clickSequence).Forget();
        }

        private void TryAutoMergeSameDice()
        {
            if (isMerging || battle.Game.IsBoardLockedByWave)
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
                FlyAndMerge(target).Forget();
        }

        /// <summary>날아가는 시간(초). 짧아야 "슉" 이다 — 더블탭은 빠르게 정리하려는 동작이다.</summary>
        private const float MergeFlyDuration = 0.14f;

        /// <summary>합쳐지는 중인가(대상으로 날아가는 동안). 그동안 이 다이스는 드래그·클릭을 받지 않는다.</summary>
        private bool isMerging;

        /// <summary>
        /// 더블탭 자동 머지의 중간 연출 — 누른 다이스가 대상 자리로 빠르게 날아가 닿는 순간 합쳐진다.
        ///
        /// <b>머지는 도착한 뒤에 한다.</b> 날아가는 동안 보드는 그대로이고, 도착했을 때 대상이 사라졌거나
        /// 조건이 바뀌어 머지가 거절되면 다이스는 제자리로 돌아간다(허공에 뜬 채 남지 않게).
        /// 시간은 실제 시간이라 배속에 상관없이 같은 속도로 날아간다.
        /// </summary>
        private async UniTaskVoid FlyAndMerge(UIDice target)
        {
            if (isMerging || canvas == null)
            {
                battle.Merge.TryMerge(this, target);
                return;
            }

            isMerging = true;

            Transform homeParent = transform.parent;
            Vector3 homeLocalPosition = transform.localPosition;

            // 다른 다이스 위로 날아가야 하니 캔버스 맨 위로 올린다(드래그와 같은 방식).
            transform.SetParent(canvas.transform, true);
            canvasGroup.blocksRaycasts = false;

            Vector3 start = transform.position;
            Vector3 baseScale = transform.localScale;

            for (float t = 0f; t < MergeFlyDuration;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                // 기다리는 사이 씬이 내려갔거나 이 다이스가 사라졌다.
                if (this == null)
                    return;

                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / MergeFlyDuration);

                // 처음엔 천천히 떠서 끝에서 가속한다 — 꽂히는 느낌.
                float eased = k * k * (3f - 2f * k);
                eased = Mathf.Lerp(eased, k * k, 0.6f);

                Vector3 end = target != null ? target.transform.position : start;
                transform.position = Vector3.LerpUnclamped(start, end, eased);
                transform.localScale = baseScale * Mathf.Lerp(1f, 0.82f, k);
            }

            bool merged = target != null && battle.Merge.TryMerge(this, target);
            if (merged)
                return;

            // 합쳐지지 않았다 — 제자리로.
            transform.localScale = baseScale;
            transform.SetParent(homeParent, false);
            transform.localPosition = homeLocalPosition;
            canvasGroup.blocksRaycasts = true;
            isMerging = false;
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
            if (game == null || !game.CanManageBoard)
                return;

            battle.Board?.OpenBattleDiceDetail(this);
        }

        #endregion
    }
}
