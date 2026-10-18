// 열려 있는 상점 페이지의 스크롤 위치를 0(맨 아래)~1(맨 위) 로 옮긴다.
// ARGS: [0] 0~1
var page = UnityEngine.Object.FindFirstObjectByType<OJ.Shop.UIShopPage>(FindObjectsInactive.Include);
if (page == null) { return "상점 페이지가 없다"; }
var scroll = page.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
if (scroll == null) { return "ScrollRect 가 없다"; }
Canvas.ForceUpdateCanvases();
scroll.verticalNormalizedPosition = float.Parse(ARGS[0]);
Canvas.ForceUpdateCanvases();
return $"pos={scroll.verticalNormalizedPosition} content={scroll.content.rect.size} viewport={scroll.viewport.rect.size}";
