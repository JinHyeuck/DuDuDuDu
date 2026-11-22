using OJ.Dice;
using OJ.DI;
using OJ.UI;
using UnityEngine;

namespace OJ.Lobby
{
    /// <summary>
    /// 끝난 이벤트의 남은 재화를 핀볼로 환급했을 때 로비에서 한 번 알려 주는 알림.
    ///
    /// 환급은 앱 시작(로드 직후)과 날짜가 바뀌는 순간에 돈다. 앱 시작 때는 이 화면이 아직 없어서 이벤트를 못 받으므로
    /// <see cref="PromotionManager.TakePendingRefund"/> 에 쌓인 값을 <c>Start</c> 에서 꺼내 보고,
    /// 로비에 머무는 동안 날짜가 바뀌면 이벤트로 바로 띄운다. 전투 중에 환급됐다면 쌓여 있다가 로비로 돌아올 때 뜬다.
    /// 씬 YAML 을 고치지 않으려고 <c>LobbySceneController.Awake</c> 가 코드로 붙인다.
    /// </summary>
    public sealed class LobbyRefundNotice : MonoBehaviour
    {
        private void Start()
        {
            PromotionManager manager = PromotionManager.Instance;
            if (manager == null)
                return;

            manager.OnEventCurrencyRefunded += OnRefunded;
            Show(manager);
        }

        private void OnDestroy()
        {
            PromotionManager manager = PromotionManager.Instance;
            if (manager != null)
                manager.OnEventCurrencyRefunded -= OnRefunded;
        }

        private void OnRefunded(int shots)
        {
            PromotionManager manager = PromotionManager.Instance;
            if (manager != null)
                Show(manager);
        }

        private static void Show(PromotionManager manager)
        {
            if (manager.PendingRefundShots <= 0)
                return;

            UIConfirmDialog dialog = GameContainer.UI?.Get<UIConfirmDialog>();
            if (dialog == null)
                return;

            int shots = manager.TakePendingRefund();
            dialog.transform.SetAsLastSibling();
            dialog.Open("이벤트 재화 환급",
                "끝난 이벤트의 남은 재화를\n핀볼 " + shots + "발로 돌려드렸어요.\n(이벤트 재화 3개 = 핀볼 1발)",
                "확인", null, null);
        }
    }
}
