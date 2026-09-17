using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.UI;
using OJ.Utils;

namespace OJ.Point
{
    public class UIPointItem : MonoBehaviour
    {
        [Header("Resource Type")]
        public PointType pointType = PointType.Gold;

        [Header("UI Refs")]
        public Image BGImage;
        public Image Icon;
        public TMP_Text Amount;

        /// <summary>펀치가 얼마나 커지는가. 1.0 이면 두 배가 되므로 이 정도가 상한이다.</summary>
        private const float PunchStrength = 0.35f;

        private const float PunchDuration = 0.28f;

        /// <summary>
        /// 직전에 그린 값. <b>늘었을 때만</b> 펀치하려고 들고 있는다.
        /// 쓸 때마다 튀면 소비가 성과처럼 보이고, 상점에서 연타하면 화면이 계속 요동친다.
        /// </summary>
        private int lastValue;
        private bool hasLastValue;

        private int punchSequence;

        private void OnEnable()
        {
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;

            RefreshAll();
        }

        private void OnDisable()
        {
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;

            // 펀치 도중에 꺼지면 커진 채로 굳는다. 다음에 켜질 때 그 크기로 뜬다.
            punchSequence++;
            if (Amount != null)
                Amount.transform.localScale = Vector3.one;
        }

        private void OnPointChanged(PointType changedType, int value)
        {
            if (changedType != pointType)
                return;

            bool increased = hasLastValue && value > lastValue;

            RefreshValue();

            if (increased)
                PlayPunch().Forget();
        }

        public void RefreshAll()
        {
            RefreshMetadata();
            RefreshValue();
        }

        public void RefreshMetadata()
        {
            PointMetadataDatabase db = StaticResource.Instance.PointMetadataDatabase;
            PointMetadataDatabase.PointMetadata metadata = db != null ? db.Get(pointType) : null;

            if (Icon != null)
                Icon.sprite = metadata != null ? metadata.icon : null;
        }

        public void RefreshValue()
        {
            if (Amount == null || PointManager.Instance == null)
                return;

            int value = PointManager.Instance.Get(pointType);
            Amount.SetText("{0}", value);

            lastValue = value;
            hasLastValue = true;
        }

        /// <summary>
        /// 숫자가 잠깐 커졌다 돌아온다.
        ///
        /// <b>숫자만 키우고 아이콘은 두는 이유.</b> 칸 전체가 커지면 옆 칸과 간격이 흔들려
        /// 상단 HUD 가 통째로 출렁인다. 바뀐 것은 숫자뿐이니 숫자만 움직이면 된다.
        /// </summary>
        private async UniTaskVoid PlayPunch()
        {
            if (Amount == null)
                return;

            int mine = ++punchSequence;
            Transform target = Amount.transform;
            float elapsed = 0f;

            while (elapsed < PunchDuration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                // 파괴됐거나, 또 늘었거나(새 펀치가 시작됐다), 꺼졌다.
                if (this == null || target == null || mine != punchSequence)
                    return;

                // 배속과 일시정지를 타지 않는다. 전투를 3배로 돌려도 읽는 속도는 같다.
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / PunchDuration);
                target.localScale = Vector3.one * (1f + UIEase.Punch(t) * PunchStrength);
            }

            if (this == null || target == null || mine != punchSequence)
                return;

            target.localScale = Vector3.one;
        }
    }
}
