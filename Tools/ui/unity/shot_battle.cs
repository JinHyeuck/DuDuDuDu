// 전투 화면(월드 + ScreenSpaceCamera 보드 + Overlay 팝업)을 한 장으로 합쳐 찍는다.
// ARGS: [0] 저장할 PNG 절대경로, [1] 가로(선택, 기본 1080), [2] 세로(선택, 기본 1920) — 22:9 는 1080 2640
//
// shot_canvas 는 Overlay 캔버스만 모은다. 전투 보드/데미지 캔버스는 ScreenSpaceCamera 라 빠진다.
// 그래서 활성 카메라를 depth 순으로 한 RenderTexture 에 차례로 그리고(둘째부터 Depth 만 지움),
// Overlay 캔버스는 임시 카메라에 붙여 맨 위에 덧그린다.
// 게임 뷰가 가로(1280x720)면 CanvasScaler 가 그 비율로 짜므로 먼저 세로 해상도를 잡는다.
if (ARGS.Length < 1) { return "사용법: shot_battle <out.png> [가로 세로]"; }

int W = ARGS.Length >= 3 ? int.Parse(ARGS[1]) : 1080;
int H = ARGS.Length >= 3 ? int.Parse(ARGS[2]) : 1920;
UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)W, (uint)H, "portrait");
Canvas.ForceUpdateCanvases();

var rtex = new RenderTexture(W, H, 24);

var cams = new System.Collections.Generic.List<Camera>();
foreach (var c in Camera.allCameras)
    if (c.enabled && c.gameObject.activeInHierarchy && c.cameraType == CameraType.Game) cams.Add(c);
cams.Sort((a, b) => a.depth.CompareTo(b.depth));

var saved = new System.Collections.Generic.List<(Camera cam, RenderTexture target, CameraClearFlags flags)>();
foreach (var c in cams) saved.Add((c, c.targetTexture, c.clearFlags));

for (int i = 0; i < cams.Count; i++)
{
    cams[i].targetTexture = rtex;
    if (i > 0) cams[i].clearFlags = CameraClearFlags.Depth;
    cams[i].Render();
}

var overlays = new System.Collections.Generic.List<(Canvas canvas, Camera cam, float plane)>();
var camGo = new GameObject("__ShotCam");
var overlayCam = camGo.AddComponent<Camera>();
overlayCam.clearFlags = CameraClearFlags.Depth;
overlayCam.cullingMask = ~0;
overlayCam.orthographic = true;
overlayCam.orthographicSize = H * 0.5f;
overlayCam.targetTexture = rtex;
overlayCam.transform.position = new Vector3(100000f, 100000f, -1000f);

var found = new System.Collections.Generic.List<Canvas>();
foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
    if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) found.Add(c);
found.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

foreach (var c in found)
{
    overlays.Add((c, c.worldCamera, c.planeDistance));
    c.renderMode = RenderMode.ScreenSpaceCamera;
    c.worldCamera = overlayCam;
    c.planeDistance = 100f;
}

Canvas.ForceUpdateCanvases();
overlayCam.Render();

RenderTexture.active = rtex;
var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
tex.Apply();
RenderTexture.active = null;
System.IO.File.WriteAllBytes(ARGS[0], tex.EncodeToPNG());

foreach (var o in overlays)
{
    o.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    o.canvas.worldCamera = o.cam;
    o.canvas.planeDistance = o.plane;
}
foreach (var s in saved)
{
    s.cam.targetTexture = s.target;
    s.cam.clearFlags = s.flags;
}
Canvas.ForceUpdateCanvases();

UnityEngine.Object.DestroyImmediate(camGo);
rtex.Release();
return "저장: " + ARGS[0] + " (카메라 " + cams.Count + "개, Overlay 캔버스 " + found.Count + "개)";
