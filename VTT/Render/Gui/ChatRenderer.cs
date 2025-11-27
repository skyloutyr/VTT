namespace VTT.Render.Gui
{
    using ImGuiNET;
    using SixLabors.ImageSharp;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Text;
    using VTT.Control;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;

    public partial class GuiRenderer
    {
        public bool showDisconnect;

        public static GuiRenderer Instance => Client.Instance.Frontend.Renderer.GuiRenderer;

        public bool MoveChatToEnd { get; set; }
        public ChatBuffer ChatInputBuffer { get; set; }

        public void SendChat(string line)
        {
            Client.Instance.Logger.Log(LogLevel.Debug, "Sending chat: " + line);
            PacketChatMessage pcm = new PacketChatMessage() { Message = line };
            pcm.Send();
            this._chatMemory.Add(line);
            this._cChatIndex = this._chatMemory.Count;
        }

        private readonly Queue<Action> _chatActions = new Queue<Action>();
        /// <summary>
        /// Use this method if a chat text change is needed while the chat might be focused by the user. <br/>
        /// If not used, ImGUI overrides the buffer modification bc of it being focused!
        /// </summary>
        public void AddChatAction(Action a) => this._chatActions.Enqueue(a);

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
                if (this._needsRefocusChat)
                {
                    ImGui.SetKeyboardFocusHere();
                    this._needsRefocusChat = false;
                }

                /* Old chat handling code
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
                */
                if (this._chatActions.Count > 0)
                {
                    ImGuiHelper.ClearActiveID();
                    while (this._chatActions.Count > 0)
                    {
                        this._chatActions.Dequeue()();
                    }

                    ImGui.SetKeyboardFocusHere();
                }

                bool b = this.ChatInputBuffer.RenderInput(cSize, out sendSignal);
                if (sendSignal)
                {
                    this._needsRefocusChat = true;
                }

                if (ImGui.IsItemHovered())
                {
                    if (this.ChatInputBuffer.StartsWith("/w ") || this.ChatInputBuffer.StartsWith("/whisper ") || this.ChatInputBuffer.StartsWith("[d:")) // Whisper help
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
                                this.ChatInputBuffer.SetText(this._chatMemory[this._cChatIndex]);
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
                                    this.ChatInputBuffer.SetText(this._chatMemory[this._cChatIndex]);
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
                    if (!this.ChatInputBuffer.IsEmpty())
                    {
                        string txt = this.ChatInputBuffer.GetText();
                        if (txt.StartsWith("/as "))
                        {
                            if (Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Count > 0)
                            {
                                string n = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects[0].Name;
                                string p = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects[0].ID.ToString();
                                txt = $"[n:{n}][o:{p}]" + txt[4..];
                            }
                            else
                            {
                                txt = txt[4..];
                            }
                        }

                        if (txt.StartsWith("/me "))
                        {
                            txt = Client.Instance.Settings.Name + txt[4..];
                        }

                        PacketChatMessage pcm = new PacketChatMessage() { Message = txt };
                        pcm.Send();
                        this._chatMemory.Add(txt);
                        this._cChatIndex = this._chatMemory.Count;
                    }
                }

                this.ChatInputBuffer.Clear();
            }

            if (chatChanged)
            {
                this._chatClientRect = cSize;
            }
        }

        public unsafe class ChatBuffer
        {
            private const int MaxUTF8CharactersInBuffer = 22435188; // Arbitrary ~22MB
            private const byte NullTerminator = 0x0;

            private readonly byte* _bufferUTF8;
            private readonly uint _bufferUTF8Size;
            private readonly byte* _label;


            private readonly UnsafeResizeableArray<byte> _comparisonBuffer;

            public ChatBuffer()
            {
                this._bufferUTF8 = (byte*)MemoryHelper.AllocateBytesZeroed(this._bufferUTF8Size = MaxUTF8CharactersInBuffer);
                byte[] d = Encoding.UTF8.GetBytes("##ChatInput");
                this._label = (byte*)MemoryHelper.AllocateBytes((nuint)(d.Length + 1));
                fixed (byte* ptr = d)
                {
                    Buffer.MemoryCopy(ptr, this._label, d.Length, d.Length);
                }

                this._label[d.Length] = NullTerminator; // NULL terminator
                this._comparisonBuffer = new UnsafeResizeableArray<byte>(256); // Arbitrary
            }

            public void Free()
            {
                MemoryHelper.Free(this._bufferUTF8);
                MemoryHelper.Free(this._label);
                this._comparisonBuffer.Free();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int GetNullTerminatorLocation()
            {
                int i = -1; // Unorthodox -1 starter so we always output the exact location of our null terminator character
                while (this._bufferUTF8[++i] != NullTerminator && i < this._bufferUTF8Size) ;
                return i;
            }

            public bool IsEmpty() => this._bufferUTF8[0] == NullTerminator;
            public void Clear() => this._bufferUTF8[0] = NullTerminator; // Simply null terminate immediately, should clear the string out
            public void SetText(string text)
            {
                int i = Encoding.UTF8.GetBytes(text, new Span<byte>(this._bufferUTF8, (int)this._bufferUTF8Size));
                this._bufferUTF8[Math.Min(i, this._bufferUTF8Size)] = NullTerminator;
            }

            public void PopCharactersAtStart(int nChars)
            {
                int offset = 0;
                char c = '\0';
                char* cptr = &c;
                Decoder decoder = Encoding.UTF8.GetDecoder();
                while (true)
                {
                    if (decoder.GetChars(this._bufferUTF8 + offset++, 1, cptr, 1, false) == 1)
                    {
                        if (c == '\0')
                        {
                            break; // Reached end of string
                        }

                        if (--nChars <= 0)
                        {
                            break;
                        }
                    }

                    if (offset >= this._bufferUTF8Size)
                    {
                        break; // Reached end of buffer
                    }
                }

                // Here offset is n+1, where n is the amout of bytes we need to actually pop.
                if (--offset <= 0)
                {
                    return; // We are at the start of the string, nothing to pop
                }

                // To actually pop the characters we will iteratively override bytes until we reach EOS
                int i = 0;
                while (true)
                {
                    byte b = this._bufferUTF8[i] = this._bufferUTF8[i + offset];
                    if (b == NullTerminator)
                    {
                        break; // Break on null terminator
                    }

                    if ((++i + offset) >= this._bufferUTF8Size)
                    {
                        this._bufferUTF8[i - 1] = NullTerminator; // Terminate the string
                        break; // Break upon buffer overflow
                    }
                }
            }

            public void AppendText(string text)
            {
                int i = this.GetNullTerminatorLocation();
                int j = Encoding.UTF8.GetBytes(text, new Span<byte>(this._bufferUTF8 + i, (int)this._bufferUTF8Size - i));
                this._bufferUTF8[Math.Min(i + j, this._bufferUTF8Size)] = NullTerminator;
            }

            public void PrependText(string text)
            {
                // Here we are fundamentally in a difficult position
                // We need to 'shift' all bytes to the right n characters, where n is our input's size
                // To do this in an optimal matter we start from the end.

                // Step 1 - find the end of the current string
                int i = this.GetNullTerminatorLocation();
                if (i == 0)
                {
                    this.SetText(text);
                    return; // Can early exit as there is no data in the string
                }


                // Step 2 - find the bytes of our input
                byte[] data = Encoding.UTF8.GetBytes(text);
                int rightShift = data.Length; // NULL terminator NOT included here bc we start overriding from it
                bool wasOOB = i + rightShift >= this._bufferUTF8Size;
                while (i >= 0)
                {
                    int dst = i + rightShift;
                    if (dst >= this._bufferUTF8Size)
                    {
                        continue; // Would write to OOB
                    }

                    this._bufferUTF8[dst] = this._bufferUTF8[i--];
                }

                if (wasOOB)
                {
                    this._bufferUTF8[this._bufferUTF8Size] = NullTerminator;
                }

                fixed (byte* src = data)
                {
                    Buffer.MemoryCopy(src, this._bufferUTF8, data.Length, data.Length); // Do not null terminate here
                }
            }

            public string GetText()
            {
                int i = this.GetNullTerminatorLocation();
                return Encoding.UTF8.GetString(new Span<byte>(this._bufferUTF8, i));
            }

            public bool StartsWith(string input)
            {
                this._comparisonBuffer.EnsureCapacity(input.Length * 4);
                int i = Encoding.UTF8.GetBytes(input, this._comparisonBuffer.AsAllocatedSpan());
                if (i >= this._bufferUTF8Size - 1)
                {
                    return false; // String too big, can't start with it
                }

                int j = 0;
                while (j < i)
                {
                    if (this._bufferUTF8[j] != this._comparisonBuffer[j])
                    {
                        return false;
                    }

                    j += 1;
                }

                return true;
            }

            public bool RenderInput(Vector2 cSize, out bool callbackSignalled)
            {
                bool sendSignal = false;
                byte ret = ImGuiNative.igInputTextMultiline(this._label, this._bufferUTF8, this._bufferUTF8Size, new Vector2(cSize.X - 64, 128), ImGuiInputTextFlags.CallbackCharFilter, (data) =>
                {
                    if (data != null) // null ptr check
                    {
                        ushort c = data->EventChar;
                        if (c == 10 && !ImGui.IsKeyDown(ImGuiKey.LeftShift)) // Enter: 0xA in UTF16
                        {
                            sendSignal = true;
                            return 1;
                        }
                    }

                    return 0;
                }, (void*)0);

                callbackSignalled = sendSignal;
                return ret != 0;
            }
        }
    }
}
