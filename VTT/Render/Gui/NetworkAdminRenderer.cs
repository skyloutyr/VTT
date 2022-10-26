namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using VTT.Network;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private void RenderNetworkAdminPanel(SimpleLanguage lang, GuiState state)
        {
            if (Server.Instance != null)
            {
                if (ImGui.Begin(lang.Translate("ui.network") + "###Network"))
                {
                    ulong cbpsI = Client.Instance.NetworkIn.LastValue;
                    ulong cbpsO = Client.Instance.NetworkOut.LastValue;
                    ulong sbpsI = Server.Instance?.NetworkIn.LastValue ?? 0ul;
                    ulong sbpsO = Server.Instance?.NetworkOut.LastValue ?? 0ul;

                    if (ImGui.BeginTable("TableNetwork", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PreciseWidths | ImGuiTableFlags.NoHostExtendX, new System.Numerics.Vector2(0, 0), 100))
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Image(this.NetworkIn, Vec24x24);
                        ImGui.SameLine();
                        ImGui.Text(lang.Translate("ui.network.received"));
                        ImGui.TableSetColumnIndex(2);
                        ImGui.Image(this.NetworkOut, Vec24x24);
                        ImGui.SameLine();
                        ImGui.Text(lang.Translate("ui.network.sent"));

                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(lang.Translate("ui.network.client"));
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text(this.FormatDataUsage(cbpsI));
                        ImGui.TableSetColumnIndex(2);
                        ImGui.Text(this.FormatDataUsage(cbpsO));

                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(lang.Translate("ui.network.server"));
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text(this.FormatDataUsage(sbpsI));
                        ImGui.TableSetColumnIndex(2);
                        ImGui.Text(this.FormatDataUsage(sbpsO));

                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(lang.Translate("ui.network.total"));
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text(this.FormatDataUsage(cbpsI + sbpsI));
                        ImGui.TableSetColumnIndex(2);
                        ImGui.Text(this.FormatDataUsage(cbpsO + sbpsO));

                        ImGui.EndTable();
                    }

                    ImGui.Text(lang.Translate("ui.network.id_client_mappings"));
                    if (ImGui.BeginTable("TableClientMapping", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PreciseWidths | ImGuiTableFlags.NoHostExtendX, new System.Numerics.Vector2(0, 0), 100))
                    {
                        foreach (ClientInfo ci in Client.Instance.ClientInfos.Values)
                        {
                            if (ci.ID.Equals(Guid.Empty))
                            {
                                continue;
                            }

                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.Text(ci.ID.ToString());
                            ImGui.TableSetColumnIndex(1);
                            ImGui.TextUnformatted(ci.Name.ToString());
                            ImGui.TableSetColumnIndex(2);
                            if (ci.IsLoggedOn)
                            {
                                ImGui.TextColored(((System.Numerics.Vector4)Color.Green), lang.Translate("ui.network.online"));
                            }
                            else
                            {
                                ImGui.TextColored(((System.Numerics.Vector4)Color.Gray), lang.Translate("ui.network.offline"));
                            }
                        }

                        ImGui.EndTable();
                    }
                }

                ImGui.End();
            }
        }

        private string FormatDataUsage(ulong bytesThisSecond)
        {
            string[] ranks = {
                "bps",
                "kbps",
                "mbps",
                "gbps",
                "impossible value"
            };

            int rank = 0;
            double dBts = bytesThisSecond * 8;
            while (dBts >= 1000)
            {
                rank++;
                dBts /= 1000;
            }

            return $"{dBts:0.000}{ranks[rank % 5]}";
        }
    }
}
