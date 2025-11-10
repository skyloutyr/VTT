namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        public bool showDisconnect;

        public static GuiRenderer Instance => Client.Instance.Frontend.Renderer.GuiRenderer;

        public bool MoveChatToEnd { get; set; }

        public void SendChat(string line)
        {
            Client.Instance.Logger.Log(LogLevel.Debug, "Sending chat: " + line);
            PacketChatMessage pcm = new PacketChatMessage() { Message = line };
            pcm.Send();
            this._chatMemory.Add(line);
            this._cChatIndex = this._chatMemory.Count;
        }

        private unsafe void RenderChat(SimpleLanguage lang, GuiState state)
        {
            ImGui.SetNextWindowBgAlpha(0.0f);
            ImGui.DockSpaceOverViewport(ImGui.GetID("ChatDockID"), ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

            Vector2 cSize = Vector2.Zero;
            bool chatChanged = false;
            bool loseFocus = false;
            bool sendSignal = false;

            if (ImGui.Begin(lang.Translate("ui.chat") + "###Chat"))
            {
                cSize = ImGui.GetContentRegionAvail();
                chatChanged = !Equals(this._chatClientRect, cSize);
                loseFocus = false;
                ImGui.SetCursorPosY(cSize.Y - 100);
                sendSignal = false;
                string inputCopy = this._chatString;
                if (this._needsRefocusChat)
                {
                    ImGui.SetKeyboardFocusHere();
                    this._needsRefocusChat = false;
                }

                bool b = ImGuiHelper.InputTextMultilinePreallocated("ChatInputBufferID", "##ChatInput", ref inputCopy, 22369622 + ushort.MaxValue, new Vector2(cSize.X - 64, 128), ImGuiInputTextFlags.CallbackCharFilter, (data) =>
                {
                    if (data != null) // null ptr check
                    {
                        ushort c = data->EventChar;
                        if (c == 10 && !ImGui.IsKeyDown(ImGuiKey.LeftShift)) // Enter: 0xA in UTF16
                        {
                            sendSignal = true;
                            this._needsRefocusChat = true;
                            return 1;
                        }
                    }

                    return 0;
                });
                if (b && !sendSignal)
                {
                    this._chatString = inputCopy;
                }

                if (ImGui.IsItemHovered())
                {
                    if (inputCopy.StartsWith("/w ") || inputCopy.StartsWith("/whisper ") || inputCopy.StartsWith("[d:")) // Whisper help
                    {
                        ImGui.BeginTooltip();
                        foreach (ClientInfo client in Client.Instance.ClientInfos.Values)
                        {
                            if (client.ID.Equals(Guid.Empty) || !client.IsLoggedOn)
                            {
                                continue;
                            }

                            ImGui.PushStyleColor(ImGuiCol.Text, client.Color.Abgr());
                            ImGui.Text(client.Name);
                            ImGui.PopStyleColor();
                        }

                        ImGui.EndTooltip();
                    }

                    state.chatHovered = true;
                }

                if (!b && ImGui.IsItemFocused() && Client.Instance.Frontend.GameHandle.IsAnyControlDown())
                {
                    if (this._chatMemory.Count > 0)
                    {
                        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
                        {
                            if (--this._cChatIndex >= 0 && this._chatMemory.Count > 0)
                            {
                                loseFocus = true;
                                this._chatString = this._chatMemory[this._cChatIndex];
                                this._needsRefocusChat = true;
                            }

                            this._cChatIndex = Math.Clamp(this._cChatIndex, 0, this._chatMemory.Count - 1);
                        }
                        else
                        {
                            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
                            {
                                if (++this._cChatIndex < this._chatMemory.Count && this._chatMemory.Count > 0)
                                {
                                    loseFocus = true;
                                    this._chatString = this._chatMemory[this._cChatIndex];
                                    this._needsRefocusChat = true;
                                }

                                this._cChatIndex = Math.Clamp(this._cChatIndex, 0, this._chatMemory.Count - 1);
                            }
                        }
                    }
                }


                ImGui.SameLine();
                float bscX = ImGui.GetCursorPosX();
                ImGui.SetCursorPosX(bscX);
                ImGui.SetCursorPosY(cSize.Y - 100);
                if (this.Search.ImImageButtonCustomImageSize("btnChatSearch", Vec48x24, new Vector2(14, 2), new Vector2(20, 20)))
                {
                    state.chatSearchPopup = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.chat.btn.search.tt"));
                }

                ImGui.SetCursorPosX(bscX);
                ImGui.SetCursorPosY(cSize.Y - 66);
                if (this.ChatLinkImage.ImImageButtonCustomImageSize("btnChatLinkImage", Vec48x24, new Vector2(14, 2), new Vector2(20, 20)))
                {
                    state.linkPopup = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.chat.btn.link_image.tt"));
                }

                ImGui.SetCursorPosX(bscX);
                ImGui.SetCursorPosY(cSize.Y - 34);
                if (this.RollIcon.ImImageButtonCustomImageSize("btnChatRoll", Vec48x24, new Vector2(14, 2), new Vector2(20, 20)))
                {
                    state.rollPopup = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.chat.btn.roll.tt"));
                }

                ImGui.SetCursorPosX(bscX);
                ImGui.SetCursorPosY(cSize.Y - 2);
                sendSignal |= this.ChatSendImage.ImImageButtonCustomImageSize("btnChatSendMessage", Vec48x24, new Vector2(14, 2), new Vector2(20, 20));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.chat.btn.send.tt"));
                }

                ImGui.SetCursorPosY(32);
                Vector4 darkGray = ((Vector4)Color.DimGray);
                Vector4 imDefault = (*ImGui.GetStyleColorVec4(ImGuiCol.ChildBg));
                ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Lerp(imDefault, darkGray, Client.Instance.Settings.ChatBackgroundBrightness));
                if (ImGui.BeginChild("ChatWindow", new Vector2(cSize.X, cSize.Y - 140), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking))
                {
                    int clRendered = 0;
                    int lowestIndex = -1;
                    if (Client.Instance.Chat.Count > 0)
                    {
                        lock (Client.Instance.chatLock)
                        {
                            lowestIndex = Client.Instance.Chat[0].Index;
                            foreach (ChatLine cl in Client.Instance.Chat)
                            {
                                if (chatChanged)
                                {
                                    cl.InvalidateCache();
                                }

                                if (cl.ImRender(new Vector2(cSize.X - 8, cSize.Y - 128), cSize.Y - 128, cl.Index, lang))
                                {
                                    ImGui.Separator();
                                    ++clRendered;
                                }
                            }
                        }
                    }

                    if (ImGui.GetScrollY() / ImGui.GetScrollMaxY() <= float.Epsilon)
                    {
                        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        if (now - this._lastChatRequest > 2000) // can request chat
                        {
                            this._lastChatRequest = now;
                            if (lowestIndex > 0) // Have chat to request
                            {
                                PacketChatRequest pcr = new PacketChatRequest() { Index = lowestIndex };
                                pcr.Send();
                            }
                        }
                    }

                    if (clRendered != this._lastChatLinesRendered || this.MoveChatToEnd)
                    {
                        this._lastChatLinesRendered = clRendered;
                        if (this._scrollYLast >= 0.99f || this.MoveChatToEnd)
                        {
                            ImGui.SetScrollHereY(1.0f);
                            this.MoveChatToEnd = false;
                        }
                    }

                    this._scrollYLast = ImGui.GetScrollY() / ImGui.GetScrollMaxY();
                }

                ImGui.EndChild();
                ImGui.PopStyleColor();
            }

            ImGui.End();

            if (loseFocus)
            {
                ImGui.SetKeyboardFocusHere(-1);
            }

            if (sendSignal)
            {
                ImGui.SetKeyboardFocusHere(-1);
                if (Client.Instance.NetClient?.IsConnected ?? false)
                {
                    if (!string.IsNullOrEmpty(this._chatString))
                    {
                        if (this._chatString.StartsWith("/as "))
                        {
                            if (Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Count > 0)
                            {
                                string n = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects[0].Name;
                                string p = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects[0].ID.ToString();
                                this._chatString = $"[n:{n}][o:{p}]" + this._chatString[4..];
                            }
                            else
                            {
                                this._chatString = this._chatString[4..];
                            }
                        }

                        PacketChatMessage pcm = new PacketChatMessage() { Message = this._chatString };
                        pcm.Send();
                        this._chatMemory.Add(this._chatString);
                        this._cChatIndex = this._chatMemory.Count;
                    }
                }

                this._chatString = "";
            }

            if (chatChanged)
            {
                this._chatClientRect = cSize;
            }
        }
    }
}
