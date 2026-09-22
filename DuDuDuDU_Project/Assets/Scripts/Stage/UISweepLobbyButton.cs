using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;

namespace OJ.Stage
{
    /// <summary>
    /// 로비 소탕 버튼. <c>Canvas/Content/Stage/Sweep</c> 에 붙는다.
    ///
    /// <b>이 버튼은 원래 배선이 없었다.</b> 오브젝트는 <c>bf89eac</c>("Lobby 작업중 거의다 완료",
    /// 2026-02-28)에 들어왔는데 그때부터 컴포넌트가 RectTransform·CanvasRenderer·Image·Button
    /// 넷뿐이었고 <c>onClick</c> 도 비어 있었다. 리팩토링이 지운 것이 아니라 UI 만 깔아 둔
    /// 상태였다. 그래서 여기에 프리팹을 새로 굽지 않고 <b>이미 있는 버튼에 얹는다</b> —
    /// 붙이는 일은 <c>SweepLobbyEntryInstaller</c> 가 한다.
    ///
    /// <b>이 버튼은 창만 연다.</b> 고기를 빼고 보상을 주는 것은
    /// <see cref="UISweepCountDialog"/> 다. 지불과 지급을 한곳에 모아 두려는 것이다 —
    /// 창이 보여 준 비용과 실제로 빠지는 비용이 두 자리에 있으면 언젠가 어긋난다.
    /// </summary>
    public class UISweepLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("잠금/대상 스테이지를 적는 칸. 비어 있어도 동작한다.")]
        [SerializeField] private TMP_Text stateText;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
                button.onClick.AddListener(OnClickSweep);
        }

        private void OnEnable()
        {
            if (StageProgressManager.Instance != null)
                StageProgressManager.Instance.OnProgressChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (StageProgressManager.Instance != null)
                StageProgressManager.Instance.OnProgressChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClickSweep);
        }

        private void OnClickSweep()
        {
            StageProgressManager progress = StageProgressManager.Instance;
            if (progress == null)
            {
                Debug.LogWarning("[소탕] StageProgressManager 가 없다. 창을 열지 않는다.");
                return;
            }

            if (!SweepRules.IsUnlocked(progress.GetLastClearedStageIndex()))
            {
                // 여기 오면 Refresh 가 버튼을 못 잠근 것이다. 창이 "클리어한 스테이지가 없다"
                // 로 열리는 것보다 안 열리는 편이 낫다.
                Debug.Log("[소탕] 클리어한 스테이지가 없어 잠겨 있다.");
                Refresh();
                return;
            }

            GameContainer.UI?.Show<UISweepCountDialog>();
        }

        private void Refresh()
        {
            StageProgressManager progress = StageProgressManager.Instance;
            int lastCleared = progress != null ? progress.GetLastClearedStageIndex() : 0;
            bool unlocked = SweepRules.IsUnlocked(lastCleared);

            if (button != null)
                button.interactable = unlocked;

            if (stateText != null)
            {
                stateText.SetText(unlocked
                    ? $"{SweepRules.ResolveTargetStageIndex(lastCleared)} 스테이지"
                    : "클리어 필요");
            }
        }
    }
}
