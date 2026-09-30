// 이름으로 찾은 활성 오브젝트에 클릭을 보낸다(플레이 중).
// ARGS: [0] GameObject 이름  [1] (선택) "press" 면 Click 대신 PointerDown→PointerUp 을 보낸다
//       — Button 이 아니라 IPointerDown/Up 으로 탭을 받는 것(예: UITowerDiceCell)용.
//
// 레이캐스트를 우회한다 — 가려진 버튼도 눌린다. "누를 수 있는가" 는 screen_check 로 따로 본다.
// 같은 이름이 여럿이면 처음 찾은 것을 누른다.
if (ARGS.Length < 1) return "사용법: click <GameObject 이름> [press]";

UnityEngine.GameObject target = null;
foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.RectTransform>(UnityEngine.FindObjectsSortMode.None))
    if (t.name == ARGS[0] && t.gameObject.activeInHierarchy) { target = t.gameObject; break; }
if (target == null) return "없음(활성 오브젝트): " + ARGS[0];

var es = UnityEngine.EventSystems.EventSystem.current;
var ev = new UnityEngine.EventSystems.PointerEventData(es);
if (ARGS.Length > 1 && ARGS[1] == "press")
{
    UnityEngine.EventSystems.ExecuteEvents.Execute(target, ev, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
    UnityEngine.EventSystems.ExecuteEvents.Execute(target, ev, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
}
else
{
    UnityEngine.EventSystems.ExecuteEvents.Execute(target, ev, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
}

var open = new System.Collections.Generic.List<string>();
foreach (var m in UnityEngine.Object.FindObjectsByType<OJ.UI.DialogBase>(UnityEngine.FindObjectsSortMode.None))
    if (m.isEnter && m.dialogView != null && m.dialogView.activeInHierarchy) open.Add(m.GetType().Name);
return "눌림: " + ARGS[0] + " → 열린 창: [" + string.Join(", ", open) + "]";
