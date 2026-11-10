// UnityMCPBridge.cs
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Unity MCP Bridge - Full Remote Controller
/// </summary>
[InitializeOnLoad]
public static class UnityMCPBridge
{
    private static HttpListener listener;
    private static Thread listenerThread;
    private static bool isRunning = false;
    private const int PORT = 8090;

    static UnityMCPBridge()
    {
        StartServer();
        AssemblyReloadEvents.beforeAssemblyReload += StopServer;
        EditorApplication.quitting += StopServer;
    }

    public static void StartServer()
    {
        if (isRunning) return;
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{PORT}/");
            listener.Start();
            isRunning = true;
            listenerThread = new Thread(ListenLoop) { IsBackground = true };
            listenerThread.Start();
            Debug.Log($"<color=#4cd964>🟢 Unity MCP Bridge AKTIF di port {PORT}!</color>");
        }
        catch
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{PORT}/");
                listener.Start();
                isRunning = true;
                listenerThread = new Thread(ListenLoop) { IsBackground = true };
                listenerThread.Start();
                Debug.Log($"<color=#4cd964>🟢 Unity MCP Bridge AKTIF di http://localhost:{PORT}/</color>");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityMCPBridge] Gagal: {e.Message}");
            }
        }
    }

    public static void StopServer()
    {
        isRunning = false;
        if (listener != null && listener.IsListening)
        {
            try { listener.Stop(); } catch { }
            try { listener.Close(); } catch { }
        }
    }

    private static void ListenLoop()
    {
        while (isRunning && listener != null && listener.IsListening)
        {
            try
            {
                var context = listener.GetContext();
                ThreadPool.QueueUserWorkItem((_) => ProcessRequest(context));
            }
            catch { break; }
        }
    }

    [System.Serializable]
    public class NodeInfo
    {
        public string name;
        public bool active;
        public string[] components;
        public List<NodeInfo> children = new List<NodeInfo>();
    }

    private static NodeInfo DumpGameObject(GameObject go)
    {
        var node = new NodeInfo { name = go.name, active = go.activeSelf };
        var comps = go.GetComponents<Component>();
        var list = new List<string>();
        foreach (var c in comps)
        {
            if (c != null) list.Add(c.GetType().Name);
        }
        node.components = list.ToArray();
        foreach (Transform child in go.transform)
        {
            node.children.Add(DumpGameObject(child.gameObject));
        }
        return node;
    }

    private static void ProcessRequest(HttpListenerContext context)
    {
        string path = context.Request.Url.AbsolutePath;
        string responseText = "";
        int statusCode = 200;

        try
        {
            if (path == "/status")
            {
                responseText = "{\"status\":\"ok\",\"engine\":\"Unity 6\",\"project\":\"NusantaraCrown-AR\"}";
            }
            else if (path == "/hierarchy")
            {
                string jsonResult = "";
                var handle = new ManualResetEvent(false);
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                        var roots = scene.GetRootGameObjects();
                        var list = new List<NodeInfo>();
                        foreach (var r in roots) list.Add(DumpGameObject(r));
                        jsonResult = JsonUtility.ToJson(new HierarchyWrapper { roots = list });
                    }
                    catch (Exception ex) { jsonResult = $"{{\"error\":\"{ex.Message}\"}}"; }
                    finally { handle.Set(); }
                };
                handle.WaitOne(3000);
                responseText = string.IsNullOrEmpty(jsonResult) ? "{\"error\":\"timeout\"}" : jsonResult;
            }
            else if (path == "/play")
            {
                EditorApplication.delayCall += () => { EditorApplication.isPlaying = true; };
                responseText = "{\"action\":\"play\",\"playing\":true}";
            }
            else if (path == "/stop")
            {
                EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
                responseText = "{\"action\":\"stop\",\"playing\":false}";
            }
            else if (path == "/fix_camera")
            {
                var handle = new ManualResetEvent(false);
                string msg = "";
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var cam = Camera.main;
                        if (cam != null)
                        {
                            cam.clearFlags = CameraClearFlags.SolidColor;
                            cam.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
                            msg = "Camera background set to SolidColor #1F1F1F";
                        }
                        else { msg = "Main camera not found"; }
                    }
                    catch (Exception ex) { msg = ex.Message; }
                    finally { handle.Set(); }
                };
                handle.WaitOne(2000);
                responseText = $"{{\"result\":\"{msg}\"}}";
            }
            else
            {
                responseText = "{\"error\":\"Unknown endpoint\"}";
                statusCode = 404;
            }
        }
        catch (Exception ex)
        {
            responseText = $"{{\"error\":\"{ex.Message}\"}}";
            statusCode = 500;
        }

        byte[] buf = Encoding.UTF8.GetBytes(responseText);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = buf.Length;
        try
        {
            context.Response.OutputStream.Write(buf, 0, buf.Length);
            context.Response.OutputStream.Close();
        }
        catch { }
    }

    [System.Serializable]
    public class HierarchyWrapper
    {
        public List<NodeInfo> roots;
    }
}
