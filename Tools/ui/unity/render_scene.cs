// ARGS[0] out png. BattleScene 을 열어 UI 캔버스를 1080x1920 으로 찍는다. 씬은 저장하지 않고 다시 연다.
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity");
var rt = new UnityEngine.RenderTexture(1080, 1920, 24);
var camGo = new UnityEngine.GameObject("__Cam"); var cam = camGo.AddComponent<UnityEngine.Camera>();
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor; cam.backgroundColor = new UnityEngine.Color(0.12f,0.11f,0.2f); cam.orthographic = true; cam.targetTexture = rt;
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None)) {
  if (!c.isRootCanvas) continue;
  c.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 10f;
  var s = c.GetComponent<UnityEngine.UI.CanvasScaler>(); if (s) { s.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new UnityEngine.Vector2(1080,1920); s.matchWidthOrHeight = 0; }
}
UnityEngine.Canvas.ForceUpdateCanvases(); cam.Render(); cam.Render();
UnityEngine.RenderTexture.active = rt; var tex = new UnityEngine.Texture2D(1080,1920,UnityEngine.TextureFormat.RGB24,false); tex.ReadPixels(new UnityEngine.Rect(0,0,1080,1920),0,0); tex.Apply();
System.IO.File.WriteAllBytes(ARGS[0], UnityEngine.ImageConversion.EncodeToPNG(tex));
UnityEngine.RenderTexture.active = null;
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/BattleScene.unity"); // 변경 버림(저장 안 함 → 다시 열기)
return "saved";
