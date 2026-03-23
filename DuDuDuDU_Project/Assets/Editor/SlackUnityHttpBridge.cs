using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SlackUnityHttpBridge
{
    private static HttpListener listener;
    private static Thread listenerThread;
    private static readonly int Port = 17777;

    private static bool isBuilding = false;
    private static bool pendingBuildRequest = false;

    private static string lastBuildStatus = "idle";
    private static string lastBuildMessage = "";
    private static string lastOutputPath = "";
    private static string lastStartedAt = "";
    private static string lastFinishedAt = "";

    static SlackUnityHttpBridge()
    {
        StartServer();
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.quitting += StopServer;
    }

    private static void StartServer()
    {
        try
        {
            if (listener != null && listener.IsListening)
                return;

            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            listener.Start();

            listenerThread = new Thread(ListenLoop);
            listenerThread.IsBackground = true;
            listenerThread.Start();

            Debug.Log($"[SlackUnityHttpBridge] HTTP server started at http://127.0.0.1:{Port}/");
        }
        catch (Exception ex)
        {
            Debug.LogError("[SlackUnityHttpBridge] Failed to start HTTP server: " + ex);
        }
    }

    private static void StopServer()
    {
        try
        {
            if (listener != null)
            {
                listener.Stop();
                listener.Close();
                listener = null;
            }

            Debug.Log("[SlackUnityHttpBridge] HTTP server stopped.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[SlackUnityHttpBridge] Failed to stop HTTP server: " + ex);
        }
    }

    private static void ListenLoop()
    {
        while (listener != null && listener.IsListening)
        {
            try
            {
                var context = listener.GetContext();
                HandleRequest(context);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SlackUnityHttpBridge] ListenLoop error: " + ex);
            }
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/health")
            {
                WriteJson(response, 200, "{\"ok\":true,\"message\":\"unity-editor-alive\"}");
                return;
            }

            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/build/status")
            {
                WriteJson(response, 200, BuildStatusJson());
                return;
            }

            if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/build/android-dev")
            {
                if (isBuilding || pendingBuildRequest)
                {
                    WriteJson(response, 409, "{\"ok\":false,\"message\":\"Build already in progress or queued.\"}");
                    return;
                }

                pendingBuildRequest = true;
                isBuilding = true;

                lastBuildStatus = "running";
                lastBuildMessage = "Build request accepted and queued.";
                lastOutputPath = "";
                lastStartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                lastFinishedAt = "";

                Debug.Log("[SlackUnityHttpBridge] Build request accepted.");

                WriteJson(response, 202, "{\"ok\":true,\"message\":\"Build request accepted.\"}");
                return;
            }

            WriteJson(response, 404, "{\"ok\":false,\"message\":\"Not found.\"}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[SlackUnityHttpBridge] HandleRequest error: " + ex);
            WriteJson(response, 500, "{\"ok\":false,\"message\":\"Internal server error.\"}");
        }
    }

    private static void OnEditorUpdate()
    {
        if (!pendingBuildRequest)
            return;

        pendingBuildRequest = false;

        try
        {
            Debug.Log("[SlackUnityHttpBridge] Android dev build started.");

            Unity3dBuilder.PerformAndroidDevelopmentBuild();

            string outputPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "_Build",
                "Android",
                "DuDuDuDu.apk"
            );

            if (File.Exists(outputPath))
            {
                lastBuildStatus = "success";
                lastBuildMessage = "Android development build completed.";
                lastOutputPath = outputPath;
                Debug.Log("[SlackUnityHttpBridge] Build finished successfully.");
            }
            else
            {
                lastBuildStatus = "failed";
                lastBuildMessage = "Build finished but APK file was not found.";
                lastOutputPath = outputPath;
                Debug.LogError($"[SlackUnityHttpBridge] Build finished but APK file was not found. Path: {outputPath}");
            }
        }
        catch (Exception ex)
        {
            lastBuildStatus = "failed";
            lastBuildMessage = ex.ToString();
            lastOutputPath = "";
            Debug.LogError("[SlackUnityHttpBridge] Build failed: " + ex);
        }
        finally
        {
            isBuilding = false;
            lastFinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    private static string BuildStatusJson()
    {
        var status = new BuildStatus
        {
            ok = true,
            isBuilding = isBuilding,
            pendingBuildRequest = pendingBuildRequest,
            lastBuildStatus = lastBuildStatus,
            lastBuildMessage = lastBuildMessage,
            lastOutputPath = lastOutputPath,
            lastStartedAt = lastStartedAt,
            lastFinishedAt = lastFinishedAt
        };

        return JsonUtility.ToJson(status, true);
    }

    [Serializable]
    private class BuildStatus
    {
        public bool ok;
        public bool isBuilding;
        public bool pendingBuildRequest;
        public string lastBuildStatus;
        public string lastBuildMessage;
        public string lastOutputPath;
        public string lastStartedAt;
        public string lastFinishedAt;
    }

    private static void WriteJson(HttpListenerResponse response, int statusCode, string json)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;

        using (var output = response.OutputStream)
        {
            output.Write(buffer, 0, buffer.Length);
        }
    }
}