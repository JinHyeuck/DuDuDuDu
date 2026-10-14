// 플레이 중인 화면의 UI 캔버스를 1080x1920 RenderTexture 에 직접 찍는다.
// ARGS: [0] 저장할 PNG 절대경로
//
// 왜 capture_game_view 를 안 쓰나: 그쪽은 게임 뷰 창 해상도에 끌려다녀서
// 세로 화면이 가로로 찍히거나 레이아웃이 한 프레임 늦은 상태로 나온다.
// 여기서는 캔버스를 잠시 ScreenSpaceCamera 로 돌려 내 카메라로만 그린다.
if (ARGS.Length < 1) { return "사용법: shot_canvas <out.png>"; }

var targets = new System.Collections.Generic.List<Canvas>();
foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
    if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) targets.Add(c);

targets.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

var rtex = new RenderTexture(1080, 1920, 24);
var camGo = new GameObject("__ShotCam");
var cam = camGo.AddComponent<Camera>();
cam.clearFlags = CameraClearFlags.SolidColor;
cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
cam.orthographic = true;
cam.orthographicSize = 960f;
cam.targetTexture = rtex;
cam.transform.position = new Vector3(0f, 0f, -1000f);

foreach (var c in targets)
{
    c.renderMode = RenderMode.ScreenSpaceCamera;
    c.worldCamera = cam;
    c.planeDistance = 100f;
}

Canvas.ForceUpdateCanvases();
cam.Render();

RenderTexture.active = rtex;
var tex = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
tex.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
tex.Apply();
RenderTexture.active = null;
System.IO.File.WriteAllBytes(ARGS[0], tex.EncodeToPNG());

foreach (var c in targets)
{
    c.renderMode = RenderMode.ScreenSpaceOverlay;
    c.worldCamera = null;
}
Canvas.ForceUpdateCanvases();

UnityEngine.Object.DestroyImmediate(camGo);
rtex.Release();
return "저장: " + ARGS[0] + " (캔버스 " + targets.Count + "개)";
