// 프리팹을 빈 씬의 1080x1920 캔버스(폭 맞춤)에 띄워 PNG 로 찍는다. 플레이 모드가 필요 없다.
// ARGS: [0] 프리팹 경로(Assets/...)  [1] 저장할 PNG 절대경로
//
// 주의
//  - 현재 열린 씬을 빈 씬으로 바꾼다. 저장 안 된 씬 변경이 있으면 먼저 저장할 것.
//  - 런타임 데이터가 없다. 목록 템플릿·아이콘은 굽힌 기본 모습 그대로 나온다.
//  - 여기서 Provider·StaticResource 를 깨우는 코드를 부르지 말 것 — 에디터 모드에서 한 번만
//    나오고 잠기는 에러 로그를 밟아, 같은 세션의 진짜 배선 사고를 삼킨다(AGENTS.md).
if (ARGS.Length < 2) return "사용법: render_prefab <Assets/... .prefab> <out.png>";

UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

var rt = new UnityEngine.RenderTexture(1080, 1920, 24);
var camGo = new UnityEngine.GameObject("Cam");
var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor = UnityEngine.Color.magenta;
cam.orthographic = true;
cam.targetTexture = rt;

var canvasGo = new UnityEngine.GameObject("Canvas", typeof(UnityEngine.RectTransform));
var canvas = canvasGo.AddComponent<UnityEngine.Canvas>();
canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
canvas.worldCamera = cam;
canvas.planeDistance = 10f;
var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new UnityEngine.Vector2(1080, 1920);
scaler.matchWidthOrHeight = 0f;

var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(ARGS[0]);
if (asset == null) return "프리팹 없음: " + ARGS[0];
var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(asset, canvasGo.transform);

// 다이얼로그는 DialogView 가 꺼진 채로 구워져 있다.
foreach (var t in go.GetComponentsInChildren<UnityEngine.Transform>(true))
    if (t.name == "DialogView") t.gameObject.SetActive(true);

UnityEngine.Canvas.ForceUpdateCanvases();
UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((UnityEngine.RectTransform)canvasGo.transform);
UnityEngine.Canvas.ForceUpdateCanvases();
cam.Render();

UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(1080, 1920, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, 1080, 1920), 0, 0);
tex.Apply();
UnityEngine.RenderTexture.active = null;
System.IO.File.WriteAllBytes(ARGS[1], tex.EncodeToPNG());

UnityEngine.Object.DestroyImmediate(canvasGo);
UnityEngine.Object.DestroyImmediate(camGo);
rt.Release();
return "저장: " + ARGS[1];
