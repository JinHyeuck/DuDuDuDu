using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Point;
using OJ.UI;
using OJ.Utils;
using VContainer;
using VContainer.Unity;   // resolver.Instantiate 확장 메서드

namespace OJ.Element
{
    public class UIElementUpgradePanel : DialogBase
    {
        // 8.3b: 이 패널은 씬에 놓여 있지 않고 UIService 가 카탈로그 프리팹으로 찍는다.
        // 배틀 스코프의 씬 순회는 그 전에 이미 끝났으므로 순회로는 안 잡히고,
        // 찍히는 그 순간에 주입이 붙는다.
        [Inject] private IBattleRefs battle;

        // 아이템 5줄을 런타임에 찍는 자리라 리졸버가 필요하다. 맨 Instantiate 로 찍으면
        // 새로 태어난 UIElementUpgradeItem 의 [Inject] 가 빈 채로 남는다 —
        // 그 아이템은 창구로만 레벨·비용을 읽으므로 곧바로 사고가 된다.
        [Inject] private IObjectResolver resolver;

        [Header("Header")]
        [SerializeField] private Image coinIconImage;
        [SerializeField] private TMP_Text coinAmountText;

        [Header("List")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIElementUpgradeItem itemPrefab;

        private readonly List<UIElementUpgradeItem> items = new List<UIElementUpgradeItem>();
        private readonly ElementType[] elementOrder =
        {
            ElementType.Normal,
            ElementType.Fire,
            ElementType.Water,
            ElementType.Light,
            ElementType.Dark
        };

        /// <summary>
        /// 목록을 굽는 자리를 <c>OnLoad</c> 에서 <b>Start 로 내렸다.</b> (8.3b)
        ///
        /// <c>OnLoad</c> 는 <c>DialogBase.Awake</c> 가 부른다. <b>주입은 그보다 먼저다</b> —
        /// VContainer 가 프리팹을 <c>SetActive(false)</c> 로 껐다 찍고 주입한 뒤에 켜므로
        /// (<c>ObjectResolverUnityExtensions.cs:78-91</c>) <c>OnLoad</c> 시점에도 채워져 있다.
        /// 그래도 여기서 찍지 않는 것은, 씬에 놓인 컴포넌트는 반대(자기 <c>Awake</c> 뒤)라
        /// 두 규칙을 섞어 기억하는 것이 사고의 원천이기 때문이다.
        /// </summary>
        private void Start()
        {
            BuildIfNeeded();
        }

        protected override void OnEnter()
        {
            // UIService 는 Instantiate 한 그 호출에서 Enter 까지 밀어붙이므로, 처음 열릴 때는
            // Start 가 아직 안 돌았을 수 있다. 굽기는 한 번뿐이니 여기서 한 번 더 확인해
            // "보일 때는 이미 5줄이 있다"는 예전 순서를 지킨다. 이 시점의 resolver 는
            // Instantiate 가 끝난 뒤라 이미 채워져 있다.
            BuildIfNeeded();

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;

            if (battle.ElementUpgrade != null)
                battle.ElementUpgrade.OnElementLevelChanged += OnElementLevelChanged;

            Refresh();
        }

        protected override void OnExit()
        {
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;

            // 여기의 null 검사는 남긴다. OnExit 는 OnDestroy 를 타고도 들어오는데, 씬이
            // 내려가는 중이면 매니저가 먼저 파괴돼 창구 뒤가 가짜 null 이다. 그때 이벤트를
            // 건드리면 MissingReferenceException 이 난다.
            if (battle.ElementUpgrade != null)
                battle.ElementUpgrade.OnElementLevelChanged -= OnElementLevelChanged;
        }

        public void Open()
        {
            Enter();
            Refresh();
        }

        public void Refresh()
        {
            RefreshHeader();

            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();
        }

        private void BuildIfNeeded()
        {
            if (listRoot == null || itemPrefab == null || items.Count > 0)
                return;

            for (int i = 0; i < elementOrder.Length; i++)
            {
                // 부모(listRoot)를 반드시 넘긴다. 부모 없는 오버로드는 스코프 아래 만들었다가
                // 떼어내는 분기를 타서 아이템이 엉뚱한 씬에 남는다.
                UIElementUpgradeItem item = resolver.Instantiate(itemPrefab, listRoot);
                item.Bind(elementOrder[i], OnClickUpgrade);
                items.Add(item);
            }
        }

        private void RefreshHeader()
        {
            if (coinAmountText != null)
                coinAmountText.SetText("{0}", PointManager.Instance != null ? PointManager.Instance.Get(PointType.Coin) : 0);

            if (coinIconImage != null && StaticResource.Instance != null && StaticResource.Instance.PointMetadataDatabase != null)
            {
                PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(PointType.Coin);
                coinIconImage.sprite = metadata != null ? metadata.icon : null;
            }
        }

        private void OnClickUpgrade(ElementType elementType)
        {
            if (battle.ElementUpgrade == null)
                return;

            if (battle.ElementUpgrade.TryLevelUp(elementType))
                Refresh();
        }

        private void OnPointChanged(PointType pointType, int value)
        {
            if (pointType == PointType.Coin)
                Refresh();
        }

        private void OnElementLevelChanged(ElementType elementType, int level)
        {
            Refresh();
        }
    }
}
