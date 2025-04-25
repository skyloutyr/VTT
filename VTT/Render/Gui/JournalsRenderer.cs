namespace VTT.Render.Gui
{
    using ImGuiNET;
    using System;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private unsafe void RenderJournals(SimpleLanguage lang, GuiState state)
        {
            if (ImGui.Begin(lang.Translate("ui.journals") + "###Journals"))
            {
                System.Numerics.Vector2 wC = ImGui.GetWindowSize();
                foreach (TextJournal tj in Client.Instance.Journals.Values)
                {
                    if (ImGui.BeginChild("journal_" + tj.SelfID.ToString(), new System.Numerics.Vector2(wC.X - 32, 32), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings))
                    {
                        ImGui.TextUnformatted(tj.Title);
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(wC.X - 32 - 28);
                        if (!Client.Instance.IsAdmin && tj.OwnerID != Client.Instance.ID)
                        {
                            ImGui.BeginDisabled();
                        }

                        ImGui.PushID("BtnDeleteJournal_" + tj.SelfID.ToString());
                        if (this.DeleteIcon.ImImageButton("BtnDeleteJournal_" + tj.SelfID.ToString(), Vec12x12))
                        {
                            new PacketDeleteJournal() { JournalID = tj.SelfID }.Send();
                        }

                        ImGui.PopID();
                        if (!Client.Instance.IsAdmin && tj.OwnerID != Client.Instance.ID)
                        {
                            ImGui.EndDisabled();
                        }

                        ImGui.SameLine();
                        ImGui.SetCursorPosX(wC.X - 32 - 56);
                        ImGui.PushID("BtnEditJournal_" + tj.SelfID.ToString());
                        if (this.JournalEdit.ImImageButton("BtnEditJournal_" + tj.SelfID.ToString(), Vec12x12))
                        {
                            state.journalPopup = true;
                            this._editedJournal = tj;
                            this._journalTextEdited = false;
                        }

                        ImGui.PopID();
                    }

                    ImGui.EndChild();
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetNextWindowSize(new System.Numerics.Vector2(400, 300));
                        ImGui.BeginTooltip();
                        ImGui.TextWrapped(tj.Text);
                        ImGui.EndTooltip();
                    }
                }

                ImGui.PushID("btnNewJournal");
                if (this.AddIcon.ImImageButton("btnNewJournal", Vec12x12))
                {
                    if (Client.Instance.IsAdmin)
                    {
                        new PacketCreateJournal() { CreatorID = Client.Instance.ID, JournalID = Guid.NewGuid(), Title = "New Journal" }.Send();
                    }
                }

                ImGui.PopID();
            }

            ImGui.End();
        }

    }
}
