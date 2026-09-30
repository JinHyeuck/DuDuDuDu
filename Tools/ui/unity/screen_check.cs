// 플레이 중 화면 검사 두 가지 + 스크린샷. 눈으로 못 잡는 것을 잡는다.
// ARGS: [0] (선택) 스크린샷 PNG 절대경로
//
//  1) 사라진 글자 — 원문 글자 수보다 실제 그려진 글자가 적은 TMP.
//     칸 높이가 폰트 x 줄높이보다 작으면 Ellipsis 모드에서 글자가 통째로 사라지고 콘솔은 조용하다.
//     한계: 폰트에 문자 항목은 있지만 그림이 빈 글리프(BM HANNA 의 · × —)는 못 잡는다.
//  2) 탭 가능 — 버튼 중심을 레이캐스트해 맨 위 클릭 핸들러가 그 버튼인지.
//     스크립트 클릭(ExecuteEvents)은 레이캐스트를 우회하므로 가려진 버튼도 눌린 것처럼 보인다.
//     정상 예외: 전체 화면 오버레이가 열려 있을 때 그 아래 버튼.
//
// 대상: 열려 있는 DialogBase 중 형제 순서상 가장 위의 것. 없으면 화면 전체.
// 스크린샷은 프레임 끝에 저장되므로 한 박자 뒤에 파일이 생긴다.
var sb = new System.Text.StringBuilder();

UnityEngine.Transform root = null;
foreach (var m in UnityEngine.Object.FindObjectsByType<OJ.UI.DialogBase>(UnityEngine.FindObjectsSortMode.None))
{
    if (!m.isEnter || !m.isActiveAndEnabled) continue;
    if (m.dialogView != null && !m.dialogView.activeInHierarchy) continue;
    if (root == null || m.transform.GetSiblingIndex() > root.GetSiblingIndex()) root = m.transform;
}
sb.AppendLine("대상: " + (root != null ? root.name : "화면 전체"));

System.Collections.Generic.IEnumerable<TMPro.TMP_Text> texts = root != null
    ? (System.Collections.Generic.IEnumerable<TMPro.TMP_Text>)root.GetComponentsInChildren<TMPro.TMP_Text>(false)
    : UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(UnityEngine.FindObjectsSortMode.None);

int checkedTexts = 0, missing = 0;
foreach (var t in texts)
{
    if (!t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text)) continue;
    checkedTexts++;
    t.ForceMeshUpdate();
    int vis = 0;
    for (int i = 0; i < t.textInfo.characterCount; i++) if (t.textInfo.characterInfo[i].isVisible) vis++;
    // GetParsedText() 도 비어 버리는 경우가 있어 원문에서 태그를 빼고 센다.
    var raw = System.Text.RegularExpressions.Regex.Replace(t.text, "<[^>]*>", "");
    int want = 0; foreach (var ch in raw) if (!char.IsWhiteSpace(ch)) want++;
    if (vis < want && !(t.overflowMode == TMPro.TextOverflowModes.Ellipsis && vis > 0))
    {
        missing++;
        sb.AppendLine("  글자 부족: " + t.name + " '" + raw.Replace("\n", "/") + "' " + vis + "/" + want);
    }
}
sb.AppendLine("글자 " + checkedTexts + "개 검사, 부족 " + missing);

var es = UnityEngine.EventSystems.EventSystem.current;
int buttons = 0, blocked = 0;
if (es != null)
{
    var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
    System.Collections.Generic.IEnumerable<UnityEngine.UI.Button> list = root != null
        ? (System.Collections.Generic.IEnumerable<UnityEngine.UI.Button>)root.GetComponentsInChildren<UnityEngine.UI.Button>(false)
        : UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsSortMode.None);
    foreach (var b in list)
    {
        if (!b.isActiveAndEnabled || !b.interactable) continue;
        var rect = (UnityEngine.RectTransform)b.transform;
        var canvas = b.GetComponentInParent<UnityEngine.Canvas>().rootCanvas;
        var cam = canvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var pos = UnityEngine.RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
        if (pos.x < 0 || pos.y < 0 || pos.x > UnityEngine.Screen.width || pos.y > UnityEngine.Screen.height) continue;
        hits.Clear();
        es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = pos }, hits);
        buttons++;
        var handler = hits.Count > 0
            ? UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject)
            : null;
        if (handler != b.gameObject)
        {
            blocked++;
            sb.AppendLine("  가려짐: " + b.name + " ← " + (hits.Count > 0 ? hits[0].gameObject.name : "없음"));
        }
    }
}
sb.AppendLine("버튼 " + buttons + "개 검사, 가려짐 " + blocked);

if (ARGS.Length > 0)
{
    UnityEngine.ScreenCapture.CaptureScreenshot(ARGS[0]);
    sb.AppendLine("스크린샷(" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + "): " + ARGS[0]);
}
return sb.ToString();
