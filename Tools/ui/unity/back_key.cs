// 뒤로가기(Esc) 한 번을 흉내 낸다(플레이 중). ARGS 없음.
//
// 키 입력은 CLI 로 넣을 수 없어서 AOSBackBtnManager.Tick 의 Escape 분기와 똑같이 한다 —
// 스택에서 꺼내 열려 있는(isEnter) 첫 창의 BackKeyCall. 그 로직이 바뀌면 여기도 맞출 것.
// 실제 키가 게임 뷰에 들어가는지(포커스 등)는 이것으로 확인되지 않는다.
var mgr = OJ.Utils.AOSBackBtnManager.Instance;
if (mgr == null) return "AOSBackBtnManager 없음 (TitleScene 부터 재생했는가?)";

var field = typeof(OJ.Utils.AOSBackBtnManager).GetField("m_backBtnActionPool",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
if (field == null) return "스택 필드 이름이 바뀌었다 — back_key.cs 를 AOSBackBtnManager 에 맞출 것";
var stack = (System.Collections.Generic.Stack<OJ.UI.DialogBase>)field.GetValue(mgr);

string handled = "없음";
while (stack.Count > 0)
{
    var a = stack.Pop();
    if (!a.isEnter) continue;
    a.BackKeyCall();
    handled = a.GetType().Name;
    break;
}

var open = new System.Collections.Generic.List<string>();
foreach (var m in UnityEngine.Object.FindObjectsByType<OJ.UI.DialogBase>(UnityEngine.FindObjectsSortMode.None))
    if (m.isEnter && m.dialogView != null && m.dialogView.activeInHierarchy) open.Add(m.GetType().Name);
return "뒤로가기 처리: " + handled + " → 열린 창: [" + string.Join(", ", open) + "]";
