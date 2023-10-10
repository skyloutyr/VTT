namespace VTT.Render.Gui
{
    using ImGuiNET;
    using OpenTK.Windowing.GraphicsLibraryFramework;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.Network;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private Lazy<int> CachedRefreshRate { get; } = new Lazy<int>(() => {
            unsafe
            {
                VideoMode* vm = GLFW.GetVideoMode((Monitor*)Client.Instance.Frontend.GameHandle.CurrentMonitor.Pointer);
                return vm->RefreshRate;
            }
        });

        private Gradient<Vector4> _gradColors = new Gradient<Vector4>()
        {
            [0] = ((Vector4)Color.LawnGreen),
            [0.125f] = ((Vector4)Color.LimeGreen),
            [0.25f] = ((Vector4)Color.MediumSeaGreen),
            [0.5f] = ((Vector4)Color.MediumSlateBlue),
            [0.65f] = ((Vector4)Color.Gold),
            [0.8f] = ((Vector4)Color.Orange),
            [0.9f] = ((Vector4)Color.Crimson),
            [1] = ((Vector4)Color.DarkRed),
        };

        private static Vector4 LerpVec4(Gradient<Vector4> grad, IList<GradientPoint<Vector4>> collection, float a)
        {
            int closestStart = grad.GetClosest(a);
            GradientPoint<Vector4> point = collection[closestStart];
            GradientPoint<Vector4> next = collection[(closestStart + 1) % collection.Count];
            float aRel = Math.Abs((a - point.Key) / (next.Key - point.Key));
            return (point.Color * (1 - aRel)) + (next.Color * aRel);
        }

        private static readonly Dictionary<string, SmoothedDouble> cachedLastFrameFractions = new Dictionary<string, SmoothedDouble>();

        public void RenderPerformanceMonitor(SimpleLanguage lang)
        {
            if (!this.DebugEnabled)
            {
                return;
            }

            Color GetColorForFraction(double fract) => new Color(this._gradColors.Interpolate((float)fract, LerpVec4));

            void RenderSection(string sectionName, double time, double fract)
            {
                if (!cachedLastFrameFractions.TryGetValue(sectionName, out SmoothedDouble cf))
                {
                    cachedLastFrameFractions[sectionName] = cf = new SmoothedDouble(15);
                }

                fract = cf.GetAndInsert(fract);

                Color sectionColor = GetColorForFraction(fract);
                ImGui.TextDisabled(sectionName);
                ImDrawListPtr ptr = ImGui.GetWindowDrawList();
                Vector2 c = ImGui.GetCursorScreenPos();
                ptr.AddRectFilled(c, c + new Vector2(600, 16), Color.Black.Abgr());
                ptr.AddRectFilled(c + new Vector2(1, 1), c + new Vector2((float)(1 + 598 * fract), 15), sectionColor.Abgr());
                string t = $"{time:0.000}ms";
                Vector2 ts = ImGui.CalcTextSize(t);
                ptr.AddText(c + new Vector2(592 - ts.X, -1), Color.White.Abgr(), t);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 20);
            }

            void RenderCPUAndGPUSection(string sectionName, double tGpu, double tCpu, double tTotal)
            {
                double fractGpu = tGpu / tTotal;
                double fractCpu = tCpu / tTotal;

                if (double.IsNaN(fractGpu) || double.IsNaN(fractCpu))
                {
                    return;
                }

                if (!cachedLastFrameFractions.TryGetValue(sectionName + "C", out SmoothedDouble cf))
                {
                    cachedLastFrameFractions[sectionName + "C"] = cf = new SmoothedDouble(10);
                }

                fractCpu = cf.GetAndInsert(fractCpu);

                if (!cachedLastFrameFractions.TryGetValue(sectionName + "G", out cf))
                {
                    cachedLastFrameFractions[sectionName + "G"] = cf = new SmoothedDouble(10);
                }

                fractGpu = cf.GetAndInsert(fractGpu);

                ImGui.TextDisabled(sectionName);
                ImDrawListPtr ptr = ImGui.GetWindowDrawList();
                Vector2 c = ImGui.GetCursorScreenPos();
                Color sectionColor = GetColorForFraction(fractGpu);
                ptr.AddRectFilled(c, c + new Vector2(600, 16), Color.Black.Abgr());
                ptr.AddRectFilled(c + new Vector2(1, 1), c + new Vector2((float)(1 + 598 * fractGpu), 15), sectionColor.Abgr());
                string t = $"{tGpu:0.000}ms";
                Vector2 ts = ImGui.CalcTextSize(t);
                ptr.AddText(c + new Vector2(592 - ts.X, -1), Color.White.Abgr(), t);

                ptr.AddRectFilled(c + new Vector2(0, 17), c + new Vector2(600, 33), Color.Black.Abgr());
                sectionColor = GetColorForFraction(fractCpu);
                ptr.AddRectFilled(c + new Vector2(1, 18), c + new Vector2((float)(1 + 598 * fractCpu), 32), sectionColor.Abgr());
                t = $"{tCpu:0.000}ms";
                ts = ImGui.CalcTextSize(t);
                ptr.AddText(c + new Vector2(592 - ts.X, 16), Color.White.Abgr(), t);

                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 41);
            }

            if (ImGui.Begin(lang.Translate("ui.performance") + "###Performance Monitor"))
            {
                double frameTarget;
                if (Client.Instance.Frontend.GameHandle.RenderFrequency != 0)
                {
                    frameTarget = 1000d / Client.Instance.Frontend.GameHandle.RenderFrequency;
                }
                else
                {
                    if (Client.Instance.Frontend.GameHandle.VSync != OpenTK.Windowing.Common.VSyncMode.Off)
                    {
                        int rr = this.CachedRefreshRate.Value;
                        frameTarget = rr != 0 ? 1000d / rr : 0;
                    }
                    else
                    {
                        frameTarget = 0;
                    }
                }

                double cpuDeferred = Client.Instance.Frontend.Renderer.ObjectRenderer.CPUTimerDeferred?.ElapsedMillis() ?? 0;
                double cpuCompoundRender = Client.Instance.Frontend.Renderer.ObjectRenderer.CPUTimerCompound?.ElapsedMillis() ?? 0;
                double cpuGrid = Client.Instance.Frontend.Renderer.MapRenderer.GridRenderer.CPUTimer?.ElapsedMillis() ?? 0;
                double cpuGuiQueue = Client.Instance.Frontend.Renderer.GuiRenderer.Timer?.Buffer.ElapsedMillis() ?? 0;
                double cpuGui = Client.Instance.Frontend.GuiWrapper.CPUTimer?.ElapsedMillis() ?? 0;
                double cpuMOMain = Client.Instance.Frontend.Renderer.ObjectRenderer.CPUTimerMain?.ElapsedMillis() ?? 0;
                double cpuMOAuras = Client.Instance.Frontend.Renderer.ObjectRenderer.CPUTimerAuras?.ElapsedMillis() ?? 0;
                double cpuMOGizmos = Client.Instance.Frontend.Renderer.ObjectRenderer.CPUTimerGizmos?.ElapsedMillis() ?? 0;
                double cpuMOUBO = Client.Instance.Frontend.Renderer.ObjectRenderer.CPUTimerUBOUpdate?.ElapsedMillis() ?? 0;
                double cpuMOLights = Client.Instance.Frontend.Renderer.ObjectRenderer.CPUTimerLights?.ElapsedMillis() ?? 0;
                double cpuMOHighlights = Client.Instance.Frontend.Renderer.ObjectRenderer.CPUTimerHighlights?.ElapsedMillis() ?? 0;
                double cpuSun = Client.Instance.Frontend.Renderer.ObjectRenderer.DirectionalLightRenderer.CPUTimer?.ElapsedMillis() ?? 0;
                double cpuParticles = Client.Instance.Frontend.Renderer.ParticleRenderer.CPUTimer?.ElapsedMillis() ?? 0;
                double cpuPings = Client.Instance.Frontend.Renderer.PingRenderer.CPUTimer?.ElapsedMillis() ?? 0;
                double cpuRulers = Client.Instance.Frontend.Renderer.RulerRenderer.CPUTimer?.ElapsedMillis() ?? 0;
                double cpuFastLights = Client.Instance.Frontend.Renderer.ObjectRenderer.FastLightRenderer.CPUTimer?.ElapsedMillis() ?? 0;

                double total = cpuRulers + cpuPings + cpuDeferred + cpuGrid + cpuGui + cpuMOAuras + cpuMOMain + cpuMOGizmos + cpuMOUBO + cpuMOLights + cpuSun + cpuGuiQueue + cpuParticles + cpuDeferred + cpuMOHighlights + cpuCompoundRender + cpuFastLights;
                double totalTarget = Math.Max(total, frameTarget);

                double objectsTotal = cpuDeferred + cpuMOMain + cpuMOAuras + cpuMOUBO + cpuMOLights + cpuMOGizmos + cpuMOHighlights + cpuCompoundRender;

                RenderSection(lang.Translate("ui.performance.frame_total"), total, total / totalTarget);
                if (ImGui.TreeNode(lang.Translate("ui.performance.frame_info")))
                {
                    RenderSection(lang.Translate("ui.performance.frame_objects_total"), objectsTotal, objectsTotal / total);
                    if (ImGui.TreeNode(lang.Translate("ui.performance.frame_objects_info")))
                    {
                        RenderSection(lang.Translate("ui.performance.frame_objects_deferred"), cpuDeferred, cpuDeferred / objectsTotal);
                        RenderSection(lang.Translate("ui.performance.frame_objects_forward"), cpuMOMain, cpuMOMain / objectsTotal);
                        RenderSection(lang.Translate("ui.performance.frame_objects_compound"), cpuCompoundRender, cpuCompoundRender / objectsTotal);
                        RenderSection(lang.Translate("ui.performance.frame_objects_auras"), cpuMOAuras, cpuMOAuras / objectsTotal);
                        RenderSection(lang.Translate("ui.performance.frame_objects_ubo"), cpuMOUBO, cpuMOUBO / objectsTotal);
                        RenderSection(lang.Translate("ui.performance.frame_objects_gizmos"), cpuMOGizmos, cpuMOGizmos / objectsTotal);
                        RenderSection(lang.Translate("ui.performance.frame_objects_lights"), cpuMOLights, cpuMOLights / objectsTotal);
                        RenderSection(lang.Translate("ui.performance.frame_objects_highlights"), cpuMOHighlights, cpuMOHighlights / objectsTotal);
                        ImGui.TreePop();
                    }

                    ImGui.NewLine();
                    RenderSection(lang.Translate("ui.performance.frame_grid"), cpuGrid, cpuGrid / total);
                    RenderSection(lang.Translate("ui.performance.frame_gui"), cpuGui, cpuGui / total);
                    RenderSection(lang.Translate("ui.performance.frame_gui_queue"), cpuGuiQueue, cpuGuiQueue / total);
                    RenderSection(lang.Translate("ui.performance.frame_sun"), cpuSun, cpuSun / total);
                    RenderSection(lang.Translate("ui.performance.frame_particles"), cpuParticles, cpuParticles / total);
                    RenderSection(lang.Translate("ui.performance.frame_pings"), cpuPings, cpuPings / total);
                    RenderSection(lang.Translate("ui.performance.frame_rulers"), cpuRulers, cpuRulers / total);
                    RenderSection(lang.Translate("ui.performance.frame_fast_lights"), cpuFastLights, cpuFastLights / total);
                    ImGui.NewLine();
                    ImGui.TreePop();
                }
            }

            ImGui.End();
        }
    }
}
