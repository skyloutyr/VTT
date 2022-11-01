namespace VTT.Render.Chat
{
    using ImGuiNET;
    using System.Numerics;
    using VTT.Control;
    using VTT.Util;

    public class ChatRendererDefault : ChatRendererBase
    {
        public ChatRendererDefault(ChatLine container) : base(container)
        {
        }

        public override void Cache(out float width, out float height)
        {
            width = 400;
            height = 32;
            for (int i = 0; i < this.Container.Blocks.Count; i += 2)
            {
                height += ImGui.GetTextLineHeightWithSpacing();
            }
        }

        public override void ClearCache()
        {

        }

        public override void Render()
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 cursorScreen = ImGui.GetCursorScreenPos();
            Vector2 cursorLocal = ImGui.GetCursorPos();
            drawList.AddRectFilledMultiColor(
                cursorScreen, cursorScreen + new Vector2(400, 24),
                this.Container.SenderColor.Abgr(),
                0,
                0,
                this.Container.SenderColor.Abgr()
            );

            ImGui.SetCursorPos(cursorLocal + new Vector2(8, 32));
            for (int i = 0; i < this.Container.Blocks.Count; i += 2)
            {
                if (i >= this.Container.Blocks.Count - 1)
                {
                    break;
                }

                ChatBlock key = this.Container.Blocks[i];
                ChatBlock value = this.Container.Blocks[i + 1];
                if (key.Text.ToLower().Equals("name"))
                {
                    Vector2 cursorOld = ImGui.GetCursorPos();
                    ImGui.SetCursorPos(cursorLocal + new Vector2(24, 4));
                    ImGui.TextUnformatted(value.Text);
                    ImGui.SetCursorPos(cursorOld);
                }
            }

            if (ImGui.BeginTable("##ChatTable", 2, ImGuiTableFlags.Borders))
            {
                for (int i = 0; i < this.Container.Blocks.Count; i += 2)
                {
                    if (i >= this.Container.Blocks.Count - 1)
                    {
                        break;
                    }

                    ChatBlock key = this.Container.Blocks[i];
                    ChatBlock value = this.Container.Blocks[i + 1];
                    if (key.Text.ToLower().Equals("name"))
                    {
                        continue;
                    }

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.PushStyleColor(ImGuiCol.Text, key.Color.Abgr());
                    ImGui.TextUnformatted(key.Text);
                    ImGui.PopStyleColor();
                    ImGui.TableSetColumnIndex(1);
                    Vector2 imTtStart = ImGui.GetCursorScreenPos();
                    ImGui.PushStyleColor(ImGuiCol.Text, value.Color.Abgr());
                    ImGui.TextUnformatted(value.Text);
                    ImGui.PopStyleColor();
                    Vector2 imTtEnd = imTtStart + new Vector2(ImGui.GetColumnWidth(), ImGui.GetTextLineHeight());
                    if (!string.IsNullOrEmpty(value.Tooltip))
                    {
                        if (ImGui.IsMouseHoveringRect(imTtStart, imTtEnd))
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted(value.Tooltip);
                            ImGui.EndTooltip();
                        }
                    }
                }

                ImGui.EndTable();
            }
        }
    }
}
