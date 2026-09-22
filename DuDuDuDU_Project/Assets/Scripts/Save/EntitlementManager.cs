using System;
using UnityEngine.Scripting;
using OJ.Core;

namespace OJ.Save
{
    /// <summary>
    /// 계정 단위 권리(현금으로 사서 영구히 남는 것)를 들고 있는 자리.
    ///
    /// <b>지금 있는 것은 광고제거권 하나뿐이고, 그것을 파는 상품도 아직 없다.</b>
    /// 그런데도 클래스를 따로 두는 이유는 <b>소유자를 하나로 못 박기 위해서</b>다 —
    /// 이 플래그는 보상 라운드·상점·다른 광고 지점이 모두 보게 되는데,
    /// <c>ISaveStateOwner</c> 는 자기 몫만 써야 하므로 여러 매니저가 같은 필드를 쓰면
    /// <b>나중에 쓴 쪽이 앞의 것을 덮는다.</b> 그 사고는 조용하고, 유저가 산 것이 사라진다.
    ///
    /// <b>MonoBehaviour 가 아니다.</b> <c>GameContainer</c> 가 만들고 <c>SaveService</c> 가 저장한다.
    /// </summary>
    [Preserve]
    public sealed class EntitlementManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static EntitlementManager Instance { get; internal set; }

        private bool adFree;

        /// <summary>바뀌었을 때. 화면이 광고 버튼과 스킵 버튼을 다시 그린다.</summary>
        public event Action OnChanged;

        /// <summary>
        /// 광고제거권을 샀는가.
        ///
        /// <b>이 값이 true 면 보상 라운드에 「바로 전액 받기」가 뜬다.</b> 판을 막지는 않는다 —
        /// 굴리고 싶은 사람은 굴리고, 귀찮은 날은 한 번에 끝낸다.
        /// </summary>
        public bool AdFree
        {
            get => adFree;
            set
            {
                if (adFree == value)
                    return;

                adFree = value;
                OJ.DI.GameContainer.SaveService?.SaveAll();
                OnChanged?.Invoke();
            }
        }

        public void WriteTo(SaveState state)
        {
            if (state == null)
                return;

            state.AdFree = adFree;
        }

        public void ReadFrom(SaveState state)
        {
            if (state == null)
                return;

            // 여기서 OnChanged 를 쏘지 않는다. 로드는 아직 다른 매니저가 자기 몫을 읽기
            // 전이고, 구독자가 그 상태를 보면 절반만 로드된 게임을 그리게 된다.
            adFree = state.AdFree;
        }
    }
}
