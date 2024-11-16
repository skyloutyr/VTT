namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        private void RenderNetworkAdminPanel(SimpleLanguage lang, GuiState state)
        {
            if (Server.Instance != null)
            {
                if (ImGui.Begin(lang.Translate("ui.network") + "###Network"))
                {
                    unsafe void Plot(nint ptrIn, int ptrInAmt, nint ptrOut, nint ptrOutAmount, Vector2 size, ref int idx)
                    {
                        ImGui.BeginChild("localplot_" + idx++, new Vector2(size.X + 192, size.Y + 24));
                        ulong* dataIn = (ulong*)ptrIn;
                        ulong* dataOut = (ulong*)ptrOut;
                        ulong maxIn = 0;
                        ulong maxOut = 0;
                        ulong avgMax = 512 * 128;
                        ulong sumIn = 0, sumOut = 0;
                        for (int i = 0; i < ptrInAmt; ++i)
                        {
                            maxIn = Math.Max(dataIn[i], maxIn);
                            maxOut = Math.Max(dataOut[i], maxOut);
                            sumIn += dataIn[i];
                            sumOut += dataOut[i];
                        }

                        sumIn = (ulong)((double)sumIn / ptrInAmt);
                        sumOut = (ulong)((double)sumOut / ptrInAmt);
                        ulong tMax = Math.Max(maxIn, maxOut);
                        tMax = Math.Max(tMax, avgMax);
                        ImDrawListPtr idlp = ImGui.GetWindowDrawList();
                        Vector2 cursorScreen = ImGui.GetCursorScreenPos();

                        idlp.AddRectFilled(cursorScreen, cursorScreen + size, ImGui.GetColorU32(ImGuiCol.TitleBg));
                        float gfxStep = size.X / ptrInAmt;
                        for (int i = 0; i < ptrInAmt - 1; ++i)
                        {
                            idlp.AddLine(cursorScreen + new Vector2(gfxStep * i, 0), cursorScreen + new Vector2(gfxStep * i, size.Y), ImGui.GetColorU32(ImGuiCol.Border));
                        }

                        for (int i = 0; i < (ptrInAmt / 2) + 1; ++i)
                        {
                            float dy = i * size.Y / (ptrInAmt / 2);
                            idlp.AddLine(cursorScreen + new Vector2(0, dy), cursorScreen + new Vector2(size.X - gfxStep, dy), ImGui.GetColorU32(ImGuiCol.Border));
                        }

                        int idxMOver = -1;
                        idlp.PathClear();
                        for (int i = 0; i < ptrInAmt; ++i)
                        {
                            float posX = (gfxStep * i);
                            float posY = size.Y - ((float)(dataIn[i] / (double)tMax) * size.Y);
                            idlp.PathLineTo(cursorScreen + new Vector2(posX, posY));
                            if (ImGui.IsMouseHoveringRect(cursorScreen + new Vector2(posX, 0), cursorScreen + new Vector2(gfxStep * (i + 1), size.Y)))
                            {
                                idxMOver = i;
                            }
                        }

                        uint clrIn = ImGui.GetColorU32(ImGuiCol.PlotLines);
                        uint clrOut = Color.Orange.Abgr();
                        idlp.PathStroke(clrIn, ImDrawFlags.None, 1f);

                        idlp.PathClear();
                        for (int i = 0; i < ptrInAmt; ++i)
                        {
                            float posX = (gfxStep * i);
                            float posY = size.Y - ((float)(dataOut[i] / (double)tMax) * size.Y);
                            idlp.PathLineTo(cursorScreen + new Vector2(posX, posY));
                        }

                        idlp.PathStroke(clrOut, ImDrawFlags.None, 1f);

                        string textMax = this.FormatDataUsage(tMax);
                        Vector2 t0 = ImGui.CalcTextSize("0bps");

                        idlp.AddText(cursorScreen + new Vector2(size.X, size.Y - t0.Y), ImGui.GetColorU32(ImGuiCol.Text), "0bps");
                        idlp.AddText(cursorScreen + new Vector2(size.X, 0), ImGui.GetColorU32(ImGuiCol.Text), textMax);

                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + size.Y);
                        ImGui.TextUnformatted(lang.Translate("ui.network.inout_statistics", this.FormatDataUsage(sumIn), this.FormatDataUsage(sumOut)));

                        ImGui.EndChild();

                        if (idxMOver != -1)
                        {
                            ulong nInAtI = dataIn[idxMOver];
                            ulong nOutAtI = dataOut[idxMOver];
                            ImGui.BeginTooltip();

                            ImGui.TextUnformatted(lang.Translate("ui.network.n_ago", ptrInAmt - idxMOver));
                            ImGui.TextUnformatted($"{lang.Translate("ui.network.received")} {this.FormatDataUsage(nInAtI)}");
                            ImGui.TextUnformatted($"{lang.Translate("ui.network.sent")} {this.FormatDataUsage(nOutAtI)}");

                            ImGui.EndTooltip();
                        }
                    }

                    int plotIdx = 0;
                    ImGui.Text(lang.Translate("ui.network.client"));
                    Client.Instance.NetworkIn.GetUnderlyingDataArray(out nint dptri, out int dleni);
                    Client.Instance.NetworkOut.GetUnderlyingDataArray(out nint dptro, out int dleno);
                    Plot(dptri, dleni, dptro, dleno, new Vector2(300, 100), ref plotIdx);
                    if (Server.Instance != null)
                    {
                        ImGui.Text(lang.Translate("ui.network.server"));
                        Server.Instance.NetworkIn.GetUnderlyingDataArray(out dptri, out dleni);
                        Server.Instance.NetworkOut.GetUnderlyingDataArray(out dptro, out dleno);
                        Plot(dptri, dleni, dptro, dleno, new Vector2(300, 100), ref plotIdx);
                    }

                    ImGui.Text(lang.Translate("ui.network.id_client_mappings"));
                    if (ImGui.BeginTable("TableClientMapping", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PreciseWidths | ImGuiTableFlags.NoHostExtendX, new Vector2(0, 0), 100))
                    {
                        ImGui.TableHeadersRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(lang.Translate("ui.network.client.color"));
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text(lang.Translate("ui.network.client.id"));
                        ImGui.TableSetColumnIndex(2);
                        ImGui.Text(lang.Translate("ui.network.client.name"));
                        ImGui.TableSetColumnIndex(3);
                        ImGui.Text(lang.Translate("ui.network.client.status"));
                        ImGui.TableSetColumnIndex(4);
                        ImGui.Text(lang.Translate("ui.network.client.can_draw"));
                        ImGui.TableSetColumnIndex(5);
                        ImGui.Text(lang.Translate("ui.network.client.admin"));
                        ImGui.TableSetColumnIndex(6);
                        ImGui.Text(lang.Translate("ui.network.client.observer"));
                        ImGui.TableSetColumnIndex(7);
                        ImGui.Text(lang.Translate("ui.network.client.banned"));
                        foreach (ClientInfo ci in Client.Instance.ClientInfos.Values)
                        {
                            if (ci.ID.Equals(Guid.Empty))
                            {
                                continue;
                            }

                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.ColorButton("##ClrClient_" + ci.ID, ((Vector4)ci.Color));
                            ImGui.TableSetColumnIndex(1);
                            ImGui.Text(ci.ID.ToString());
                            ImGui.TableSetColumnIndex(2);
                            ImGui.TextUnformatted(ci.Name.ToString());
                            ImGui.TableSetColumnIndex(3);
                            if (ci.IsLoggedOn)
                            {
                                ImGui.TextColored(((Vector4)Color.Green), lang.Translate("ui.network.online"));
                            }
                            else
                            {
                                ImGui.TextColored(((Vector4)Color.Gray), lang.Translate("ui.network.offline"));
                            }

                            ImGui.TableSetColumnIndex(4);
                            bool bDrawings = ci.CanDraw;
                            if (ImGui.Checkbox("##CanDrawClient_" + ci.ID, ref bDrawings))
                            {
                                new PacketChangeClientPermissions() { ChangeeID = ci.ID, ChangeType = PacketChangeClientPermissions.PermissionType.CanDraw, ChangeValue = bDrawings }.Send();
                            }

                            ImGui.TableSetColumnIndex(5);
                            bool bAdm = ci.IsAdmin;
                            ImGui.Checkbox("##IsAdmClient_" + ci.ID, ref bAdm);

                            ImGui.TableSetColumnIndex(6);
                            bool bObs = ci.IsObserver;
                            if (ImGui.Checkbox("##IsObsClient_" + ci.ID, ref bObs))
                            {
                                new PacketChangeClientPermissions() { ChangeeID = ci.ID, ChangeType = PacketChangeClientPermissions.PermissionType.IsObserver, ChangeValue = bObs }.Send();
                            }

                            ImGui.TableSetColumnIndex(7);
                            bool bBan = ci.IsBanned;
                            if (ImGui.Checkbox("##IsBannedClient_" + ci.ID, ref bBan))
                            {
                                new PacketChangeClientPermissions() { ChangeeID = ci.ID, ChangeType = PacketChangeClientPermissions.PermissionType.IsBanned, ChangeValue = bBan }.Send();
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
