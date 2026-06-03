using System.Text;
using Unity.Profiling;
using UnityEngine;
using YuumisProwl.BallChain;

namespace YuumisProwl.Utilities
{
    /// <summary>
    /// On-screen performance readout for diagnosing FPS drops in a build, including WebGL.
    /// Shows frame time / FPS (works in any build) plus draw calls, batches, SetPass calls,
    /// triangles, vertices and GC memory (these populate in a Development Build).
    ///
    /// This shows CPU-side render counts and frame timing, which is what tells you whether
    /// you're draw-call bound. It does NOT show GPU percentage; read that from the browser's
    /// task manager (Chrome: Shift+Esc).
    ///
    /// Add to one GameObject in the scene. Toggle with the key below (default F3; change it
    /// if your browser eats that key).
    /// </summary>
    public class PerfOverlay : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        [SerializeField] private bool visibleOnStart = true;

        private bool visible;
        private float smoothedDelta;

        private ProfilerRecorder drawCalls;
        private ProfilerRecorder setPass;
        private ProfilerRecorder batches;
        private ProfilerRecorder triangles;
        private ProfilerRecorder vertices;
        private ProfilerRecorder gcMemory;

        private readonly StringBuilder sb = new StringBuilder(256);
        private GUIStyle style;
        private Texture2D bg;

        private void OnEnable()
        {
            visible = visibleOnStart;
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            gcMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
        }

        private void OnDisable()
        {
            drawCalls.Dispose();
            setPass.Dispose();
            batches.Dispose();
            triangles.Dispose();
            vertices.Dispose();
            gcMemory.Dispose();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
            smoothedDelta += (Time.unscaledDeltaTime - smoothedDelta) * 0.1f;
        }

        private void OnGUI()
        {
            if (!visible) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(8, 8, 8, 8)
                };
                bg = Texture2D.whiteTexture;
            }

            float fps = smoothedDelta > 0f ? 1f / smoothedDelta : 0f;
            float ms = smoothedDelta * 1000f;

            sb.Clear();
            sb.AppendLine($"{fps:0.} FPS   ({ms:0.0} ms)");
            sb.AppendLine($"Chain Update: {BallChainManager.UpdateMilliseconds:0.0} ms");
            AppendCounter("Draw Calls", drawCalls);
            AppendCounter("SetPass", setPass);
            AppendCounter("Batches", batches);
            AppendCounter("Triangles", triangles);
            AppendCounter("Vertices", vertices);
            if (gcMemory.Valid)
                sb.AppendLine($"GC Mem: {gcMemory.LastValue / (1024 * 1024)} MB");

            var rect = new Rect(10, 10, 300, 190);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, bg);
            GUI.color = Color.white;
            GUI.Label(rect, sb.ToString(), style);
        }

        private void AppendCounter(string label, ProfilerRecorder r)
        {
            if (r.Valid) sb.AppendLine($"{label}: {r.LastValue}");
            else sb.AppendLine($"{label}: n/a (use a Dev Build)");
        }
    }
}
