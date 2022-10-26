namespace VTT.Render.Gui
{
    using ImGuiNET;
    using System;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private unsafe void RenderLogs(SimpleLanguage lang)
        {
            if (DebugEnabled)
            {
                if (ImGui.Begin(lang.Translate("ui.logs") + "###Logs"))
                {
                    int logCount = 0;
                    lock (VTTLogListener.Instance.lockV)
                    {
                        foreach (Tuple<System.Numerics.Vector4, string> s in VTTLogListener.Instance.Logs)
                        {
                            ImGui.PushTextWrapPos();
                            ImGui.PushStyleColor(ImGuiCol.Text, s.Item1);
                            ImGui.TextUnformatted(s.Item2);
                            ImGui.PopStyleColor();
                            ImGui.PopTextWrapPos();
                            ++logCount;
                        }
                    }

                    if (this._lastLogNum != logCount)
                    {
                        ImGui.SetScrollHereY(1.0f);
                    }

                    this._lastLogNum = logCount;
                }

                ImGui.End();
            }
        }
    }
}
