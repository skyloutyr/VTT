namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Numerics;
    using VTT.Asset;
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
                    if (ImGui.BeginTable("TableClientMapping", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PreciseWidths | ImGuiTableFlags.NoHostExtendX, new System.Numerics.Vector2(0, 0), 100))
                    {
                        foreach (ClientInfo ci in Client.Instance.ClientInfos.Values)
                        {
                            if (ci.ID.Equals(Guid.Empty))
                            {
                                continue;
                            }

                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.ColorButton("##ClrClient_" + ci.ID, ((System.Numerics.Vector4)ci.Color));
                            ImGui.TableSetColumnIndex(1);
                            ImGui.Text(ci.ID.ToString());
                            ImGui.TableSetColumnIndex(2);
                            ImGui.TextUnformatted(ci.Name.ToString());
                            ImGui.TableSetColumnIndex(3);
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

                    ImGui.Text(lang.Translate("ui.network.server_cache_info"));
                    ImGui.Text(lang.Translate("ui.network.server_cache_status"));
                    ImGui.SameLine();
                    AssetManager am = Server.Instance?.AssetManager;
                    ImGui.Text(
                        am == null ? lang.Translate("ui.network.server_cache_status_noserver") : 
                        am.ServerAssetCache.Enabled ? lang.Translate("ui.network.server_cache_status_ok") : lang.Translate("ui.network.server_cache_status_disabled")
                    );

                    if (am != null && am.ServerAssetCache.Enabled)
                    {
                        ImGui.Text(lang.Translate("ui.network.server_cache_usage"));
                        ImGui.TextUnformatted(FormatMemUsage(am.ServerAssetCache.Occupancy) + "/" + FormatMemUsage(am.ServerAssetCache.MaxCacheLength) + $" ({((double)am.ServerAssetCache.Occupancy / (double)am.ServerAssetCache.MaxCacheLength * 100):0.0}%)");
                        Random rand = new Random(1337);
                        Vector2 vecScreen = ImGui.GetCursorScreenPos();
                        ImGui.Dummy(new Vector2(512, 24));
                        ImDrawListPtr dlptr = ImGui.GetWindowDrawList();
                        dlptr.AddRectFilled(vecScreen, vecScreen + new Vector2(512, 24), ImGui.GetColorU32(ImGuiCol.FrameBg));
                        long o = 0;
                        float os = 0;
                        foreach ((Guid, long) d in am.ServerAssetCache.GetDebugOccupancyInfo())
                        {
                            long l = d.Item2;
                            float f1 = rand.NextSingle() * 360;
                            if (MathF.Abs(os - f1) < 60)
                            {
                                f1 = (os + 60) % 360;
                            }

                            os = f1;
                            HSVColor clr = new HSVColor(f1, 1, 1);
                            float aS = (float)((double)o / am.ServerAssetCache.MaxCacheLength);
                            float aE = aS + (float)((double)l / am.ServerAssetCache.MaxCacheLength);
                            o += l;
                            dlptr.AddRectFilled(vecScreen + new Vector2(512 * aS, 0), vecScreen + new Vector2(512 * aE, 24), ((Color)clr).Abgr());
                            if (ImGui.IsMouseHoveringRect(vecScreen + new Vector2(512 * aS, 0), vecScreen + new Vector2(512 * aE, 24)))
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text(d.Item1.ToString());
                                if (am.Refs.TryGetValue(d.Item1, out AssetRef v))
                                {
                                    ImGui.TextUnformatted(v.Type.ToString() + " " + v.Name);
                                }
                                else
                                {
                                    ImGui.Text(lang.Translate("ui.network.server_cache_unknown_asset_usage"));
                                }

                                ImGui.Text(FormatMemUsage(l));
                                ImGui.EndTooltip();
                            }
                        }
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

            return $"{dBts:0.000}{ranks[Math.Min(rank, 4)]}";
        }

        private string FormatMemUsage(long mem)
        {
            string[] ranks = {
                "B",
                "kB",
                "mB",
                "gB",
                "i"
            };

            int rank = 0;
            double dMem = mem;
            while (dMem >= 1024)
            {
                rank++;
                dMem /= 1024;
            }

            return $"{dMem:0.000}{ranks[Math.Min(rank, 4)]}";
        }
    }
}
