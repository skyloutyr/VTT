namespace VTT.Render.Gui
{
    using ImGuiNET;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Asset;
    using VTT.Asset.Shader.NodeGraph;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;
    using SVec2 = System.Numerics.Vector2;
    using SVec3 = System.Numerics.Vector3;
    using SVec4 = System.Numerics.Vector4;
    using OGL = OpenTK.Graphics.OpenGL.GL;

    public class ShaderGraphEditorRenderer
    {
        private enum MoveMode
        {
            None,
            Camera,
            ShaderNode,
            NodeConnectionIn,
            NodeConnectionOut
        }

        internal bool popupState = false;
        private bool _extraTexturesOpen = false;

        private SVec2 _cameraLocation = SVec2.Zero;
        private bool _lmbDown;
        private SVec2 _mouseInitialPosition;
        private SVec2 _cameraInitialLocation;
        private SVec2 _nodeInitialPosition;
        private ShaderNode _nodeMoved;
        private MoveMode _moveMode;
        private NodeInput _inMoved;
        private NodeOutput _outMoved;
        private Texture _nodeLookupTexture;
        private NodeOutput _ctxOutput;

        private List<string> _shaderErrors = new List<string>();
        private List<string> _shaderWarnings = new List<string>();

        public ShaderGraph EditedGraph { get; set; }

        public unsafe void Create()
        {
            this._nodeLookupTexture = OpenGLUtil.LoadBasicTexture(new Image<Rgba32>(32, 32, new Rgba32(0, 0, 0, 255)));
        }

        public unsafe void Render(Guid shaderId, AssetRef draggedRef, GuiState state)
        {
            SimpleLanguage lang = Client.Instance.Lang;
            NodeInput nInOver = null;
            NodeOutput nOutOver = null;
            ShaderNode nodeOver = null;
            bool nodeOverHeader = false;
            bool mOverDel = false;
            bool bOpenMenu = false;
            bool bOk = false;
            bool bCancel = false;
            if (this.popupState)
            {
                if (ImGui.Begin(lang.Translate("ui.shader.title") + "###Editor Shader", ref this.popupState, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    bool mOverWin = ImGui.IsWindowHovered();

                    ImDrawListPtr drawPtr = ImGui.GetWindowDrawList();
                    SVec2 initialScreen = ImGui.GetCursorPos();
                    if (this.EditedGraph != null)
                    {
                        SVec2 padding = this._cameraLocation;
                        lock (this.EditedGraph.Lock)
                        {
                            float wW = ImGui.GetWindowSize().X;
                            float wH = ImGui.GetWindowSize().Y;
                            
                            ImGui.SetCursorPosX(wW - 320);
                            ImGui.SetCursorPosY(20);
                            if (this._extraTexturesOpen)
                            {
                                if (ImGui.BeginChild("ui.shader.extratextures", new(320, wH - 40), ImGuiChildFlags.Border))
                                {
                                    float oY = 0;
                                    int i = 0;
                                    int dIndex = -1;
                                    foreach (Guid tid in this.EditedGraph.ExtraTexturesAttachments)
                                    {
                                        ImGui.TextUnformatted($"{i}: ");
                                        ImGui.SameLine();
                                        if (ImGui.ImageButton("btn_del_xtrtex_" + tid.ToString(), GuiRenderer.Instance.DeleteIcon, new(16, 16)))
                                        {
                                            dIndex = i;
                                        }

                                        if (this.DrawAssetRecepticle(tid, lang, () => GuiRenderer.Instance.DraggedAssetReference?.Type == AssetType.Texture, GuiRenderer.Instance.AssetImageIcon))
                                        {
                                            if (GuiRenderer.Instance.DraggedAssetReference != null && GuiRenderer.Instance.DraggedAssetReference.Type == AssetType.Texture)
                                            {
                                                state.shaderGraphExtraTexturesHovered = this.EditedGraph;
                                                state.shaderGraphExtraTexturesHoveredIndex = i;
                                            }
                                        }

                                        ++i;
                                        oY += 32;
                                    }

                                    ImGui.TextUnformatted($"{i}: ");
                                    if (this.DrawAssetRecepticle(Guid.Empty, lang, () => GuiRenderer.Instance.DraggedAssetReference?.Type == AssetType.Texture, GuiRenderer.Instance.AssetImageIcon))
                                    {
                                        if (GuiRenderer.Instance.DraggedAssetReference != null && GuiRenderer.Instance.DraggedAssetReference.Type == AssetType.Texture)
                                        {
                                            state.shaderGraphExtraTexturesHovered = this.EditedGraph;
                                            state.shaderGraphExtraTexturesHoveredIndex = -1;
                                        }
                                    }

                                    if (dIndex != -1)
                                    {
                                        this.EditedGraph.ExtraTexturesAttachments.RemoveAt(dIndex);
                                    }
                                }

                                ImGui.EndChild();
                            }

                            SVec2 cursorScreenNow = ImGui.GetCursorScreenPos();
                            SVec2 windowSizeNow = ImGui.GetWindowSize();
                            // Grid colors used from https://github.com/Fattorino/ImNodeFlow/blob/master/include/ImNodeFlow.h#L246
                            uint bgCol = ImGui.GetColorU32(ImGuiCol.WindowBg);
                            uint gridCol = new Color(new Rgba32(200, 200, 200, 40)).Abgr();
                            uint gridSubCol = new Color(new Rgba32(200, 200, 200, 10)).Abgr();
                            drawPtr.AddRectFilled(new SVec2(0, 0), windowSizeNow, bgCol);
                            float gridSize = 128;
                            float gridSubdivisions = 4;
                            for (float x = this._cameraLocation.X % gridSize; x < windowSizeNow.X; x += gridSize)
                            {
                                drawPtr.AddLine(new SVec2(x, 0), new SVec2(x, windowSizeNow.Y), gridCol, 2);
                            }

                            for (float y = this._cameraLocation.Y % gridSize; y < windowSizeNow.Y; y += gridSize)
                            {
                                drawPtr.AddLine(new SVec2(0, y), new SVec2(windowSizeNow.X, y), gridCol, 2);
                            }

                            for (float x = this._cameraLocation.X % (gridSize / gridSubdivisions); x < windowSizeNow.X; x += (gridSize / gridSubdivisions))
                            {
                                drawPtr.AddLine(new SVec2(x, 0), new SVec2(x, windowSizeNow.Y), gridSubCol, 1);
                            }

                            for (float y = this._cameraLocation.Y % (gridSize / gridSubdivisions); y < windowSizeNow.Y; y += (gridSize / gridSubdivisions))
                            {
                                drawPtr.AddLine(new SVec2(0, y), new SVec2(windowSizeNow.X, y), gridSubCol, 2);
                            }

                            Color nodeBack = Color.SlateGray.Darker(0.8f);
                            Color nodeHeader = Color.SlateGray.Darker(0.7f);
                            Color nodeHeaderHover = Color.SlateGray.Darker(0.2f);
                            Color nodeBorder = Color.SlateGray.Darker(0.4f);
                            Color nodeBorderHover = Color.RoyalBlue;
                            Dictionary<Guid, bool> nodeOutputStatuses = new Dictionary<Guid, bool>();
                            Dictionary<Guid, SVec2> nodeInOutPositions = new Dictionary<Guid, SVec2>();
                            foreach (ShaderNode n in this.EditedGraph.Nodes)
                            {
                                foreach (NodeInput ni in n.Inputs)
                                {
                                    nodeOutputStatuses[ni.ConnectedOutput] = true;
                                }
                            }

                            drawPtr.ChannelsSplit(4);
                            foreach (ShaderNode n in this.EditedGraph.Nodes)
                            {
                                bool hasTemplate = ShaderNodeTemplate.TemplatesByID.TryGetValue(n.TemplateID, out ShaderNodeTemplate sht);
                                SVec2 screenPos = padding + n.Location.SystemVector();
                                bool mOver = ImGui.IsMouseHoveringRect(screenPos, screenPos + n.Size.SystemVector());
                                bool mOverHeader = ImGui.IsMouseHoveringRect(screenPos, screenPos + new SVec2(n.Size.X, 20));
                                if (mOver)
                                {
                                    nodeOver = n;
                                    if (mOverHeader)
                                    {
                                        nodeOverHeader = true;
                                    }
                                }

                                drawPtr.ChannelsSetCurrent(2);
                                drawPtr.AddText(screenPos + new SVec2(8, 0), hasTemplate ? Extensions.ContrastBlackOrWhite(sht.Category.DisplayColor).Abgr() : ImGui.GetColorU32(ImGuiCol.Text), n.Name);
                                if (n.Deletable)
                                {
                                    Color dCol = Color.Grey;
                                    if (!mOverDel)
                                    {
                                        mOverDel = ImGui.IsMouseHoveringRect(screenPos + new SVec2(n.Size.X - 18, 2), screenPos + new SVec2(n.Size.X, 18));
                                        if (mOverDel)
                                        {
                                            dCol = Color.Red;
                                        }
                                    }

                                    drawPtr.AddText(screenPos + new SVec2(n.Size.X - 18, 2), dCol.Abgr(), "✖");
                                }

                                int yOffset = 30;
                                int iIndex = 0;
                                SVec2 nSPos = screenPos;
                                foreach (NodeInput ni in n.Inputs)
                                {
                                    bool mOverInput = ImGui.IsMouseHoveringRect(nSPos + new SVec2(-5, yOffset - 5), nSPos + new SVec2(5, yOffset + 5));
                                    if (mOverInput)
                                    {
                                        nInOver = ni;
                                    }

                                    nodeInOutPositions[ni.ID] = nSPos + new SVec2(0, yOffset);

                                    if (!n.TemplateID.Equals(ShaderNodeTemplate.ConstructColor4.ID))
                                    {
                                        uint selfColor = mOverInput ? Color.RoyalBlue.Abgr() : GetColorForType(ni.SelfType).Abgr();
                                        if (!ni.ConnectedOutput.Equals(Guid.Empty))
                                        {
                                            drawPtr.AddCircleFilled(nSPos + new SVec2(0, yOffset), 5, selfColor);
                                        }
                                        else
                                        {
                                            drawPtr.AddCircle(nSPos + new SVec2(0, yOffset), 5, selfColor);
                                        }

                                        drawPtr.AddText(nSPos + new SVec2(12, yOffset - 10), Color.White.Abgr(), ni.Name);
                                    }
                                    else
                                    {
                                        if (mOverInput)
                                        {
                                            nInOver = null;
                                        }
                                    }

                                    if (ni.ConnectedOutput.Equals(Guid.Empty))
                                    {
                                        drawPtr.ChannelsSetCurrent(3);
                                        SVec2 cPos = ImGui.GetCursorPos();
                                        ImGui.SetCursorPos(nSPos + new SVec2(8, yOffset + 8));
                                        string igbid = "##tInS" + ni.ID.ToString();

                                        if (n.TemplateID.Equals(ShaderNodeTemplate.ConstructColor4.ID)) // Color picker is special
                                        {
                                            SVec4 clr = ni.CurrentValue is Vector4 v4 ? v4.SystemVector() : SVec4.Zero;
                                            ImGui.SetNextItemWidth(200 - 16);
                                            if (ImGui.ColorPicker4(igbid, ref clr, ImGuiColorEditFlags.PickerHueWheel | ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoSmallPreview | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoBorder | ImGuiColorEditFlags.NoDragDrop | ImGuiColorEditFlags.NoTooltip))
                                            {
                                                ni.CurrentValue = clr.GLVector();
                                            }

                                            yOffset += 240;
                                        }
                                        else
                                        {
                                            ImGui.SetNextItemWidth(200 - 16);
                                            switch (ni.SelfType)
                                            {
                                                case NodeValueType.Bool:
                                                {
                                                    bool b = ni.CurrentValue is bool boolean && boolean;
                                                    if (ImGui.Checkbox(igbid, ref b))
                                                    {
                                                        ni.CurrentValue = b;
                                                    }

                                                    break;
                                                }

                                                case NodeValueType.Int:
                                                case NodeValueType.UInt:
                                                {
                                                    int b = ni.CurrentValue is int @int ? @int : ni.CurrentValue is uint int1 ? (int)int1 : 0;
                                                    if (ImGui.InputInt(igbid, ref b))
                                                    {
                                                        ni.CurrentValue = ni.SelfType == NodeValueType.UInt ? (uint)b : b;
                                                    }

                                                    break;
                                                }

                                                case NodeValueType.Float:
                                                {
                                                    float f = ni.CurrentValue is float f1 ? f1 : 0;
                                                    if (ImGui.InputFloat(igbid, ref f))
                                                    {
                                                        ni.CurrentValue = f;
                                                    }

                                                    break;
                                                }

                                                case NodeValueType.Vec2:
                                                {
                                                    SVec2 sv2 = ni.CurrentValue is Vector2 v2 ? v2.SystemVector() : SVec2.Zero;
                                                    if (ImGui.InputFloat2(igbid, ref sv2))
                                                    {
                                                        ni.CurrentValue = sv2.GLVector();
                                                    }

                                                    break;
                                                }

                                                case NodeValueType.Vec3:
                                                {
                                                    SVec3 sv3 = ni.CurrentValue is Vector3 v3 ? v3.SystemVector() : SVec3.Zero;
                                                    if (ImGui.InputFloat3(igbid, ref sv3))
                                                    {
                                                        ni.CurrentValue = sv3.GLVector();
                                                    }

                                                    break;
                                                }

                                                case NodeValueType.Vec4:
                                                {
                                                    SVec4 sv4 = ni.CurrentValue is Vector4 v4 ? v4.SystemVector() : SVec4.Zero;
                                                    if (ImGui.InputFloat4(igbid, ref sv4))
                                                    {
                                                        ni.CurrentValue = sv4.GLVector();
                                                    }

                                                    break;
                                                }
                                            }
                                            yOffset += 20;
                                        }

                                        ImGui.SetCursorPos(cPos);
                                        drawPtr.ChannelsSetCurrent(2);
                                    }

                                    yOffset += 20;
                                    ++iIndex;
                                }

                                nSPos = screenPos + new SVec2(n.Size.X, 0);
                                foreach (NodeOutput no in n.Outputs)
                                {
                                    bool mOverThisOut = ImGui.IsMouseHoveringRect(nSPos + new SVec2(-5, yOffset - 5), nSPos + new SVec2(5, yOffset + 5));
                                    uint selfColor = mOverThisOut ? Color.RoyalBlue.Abgr() : GetColorForType(no.SelfType).Abgr();
                                    if (mOverThisOut)
                                    {
                                        nOutOver = no;
                                    }

                                    nodeInOutPositions[no.ID] = nSPos + new SVec2(0, yOffset);
                                    if (nodeOutputStatuses.ContainsKey(no.ID))
                                    {
                                        drawPtr.AddCircleFilled(nSPos + new SVec2(0, yOffset), 5, selfColor);
                                    }
                                    else
                                    {
                                        drawPtr.AddCircle(nSPos + new SVec2(0, yOffset), 5, selfColor);
                                    }

                                    SVec2 ts = ImGuiHelper.CalcTextSize(no.Name);
                                    drawPtr.AddText(nSPos + new SVec2(-12 - ts.X, yOffset - 10), Color.White.Abgr(), no.Name);
                                    yOffset += 20;
                                }

                                SVec2 aSize = new SVec2(200, yOffset);
                                n.Size = new Vector2(aSize.X, aSize.Y);
                                drawPtr.ChannelsSetCurrent(1);
                                drawPtr.AddRectFilled(screenPos, screenPos + aSize, nodeBack.Abgr(), 15f);
                                drawPtr.AddRect(screenPos, screenPos + aSize, mOver ? nodeBorderHover.Abgr() : nodeBorder.Abgr(), 15f);
                                drawPtr.AddRectFilled(screenPos, screenPos + new SVec2(aSize.X, 20), mOverHeader ? nodeHeaderHover.Abgr() : hasTemplate ? sht.Category.DisplayColor.Abgr() : nodeHeader.Abgr(), 5f);
                            }

                            drawPtr.ChannelsSetCurrent(0);
                            foreach ((ShaderNode, NodeInput) inputs in this.EditedGraph.AllInputsById.Values)
                            {
                                if (!inputs.Item2.ConnectedOutput.Equals(Guid.Empty))
                                {
                                    if (this.EditedGraph.AllOutputsById.TryGetValue(inputs.Item2.ConnectedOutput, out (ShaderNode, NodeOutput) data))
                                    {
                                        int outOffset = (data.Item1.Inputs.Count * 20) + (data.Item1.Outputs.IndexOf(data.Item2) * 20);
                                        SVec2 oSPos = nodeInOutPositions[data.Item2.ID];
                                        bool mOverOutput = ImGui.IsMouseHoveringRect(padding + data.Item1.Location.SystemVector(), padding + data.Item1.Location.SystemVector() + data.Item1.Size.SystemVector()) || ImGui.IsMouseHoveringRect(oSPos + new SVec2(-3, -3), oSPos + new SVec2(3, 3));
                                        SVec2 iSPos = nodeInOutPositions[inputs.Item2.ID];
                                        int xOffset = 0;
                                        bool mOverAny = nodeOver == inputs.Item1 || nodeOver == data.Item1 || nOutOver == data.Item2 || nInOver == inputs.Item2;
                                        Color lineColor = mOverAny ? Color.RoyalBlue : Color.White;
                                        this.ShaderLine(drawPtr, oSPos, data.Item2.SelfType, iSPos, xOffset, inputs.Item2.SelfType, mOverAny || mOverOutput);
                                    }
                                }
                            }

                            drawPtr.ChannelsMerge();
                        }

                        if (this._moveMode == MoveMode.NodeConnectionIn && this._inMoved != null)
                        {
                            this.ShaderLine(drawPtr, ImGui.GetMousePos(), this._inMoved.SelfType, this._nodeInitialPosition, 0, this._inMoved.SelfType, true);
                        }
                        else
                        {
                            if (this._moveMode == MoveMode.NodeConnectionOut && this._outMoved != null)
                            {
                                this.ShaderLine(drawPtr, this._nodeInitialPosition, this._outMoved.SelfType, ImGui.GetMousePos(), 0, this._outMoved.SelfType, true);
                            }
                        }
                    }
                    else
                    {
                        if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(shaderId, AssetType.Shader, out Asset a) == AssetStatus.Return && a.Shader != null && a.Shader.NodeGraph != null && a.Shader.NodeGraph.IsLoaded)
                        {
                            this.EditedGraph = a.Shader.NodeGraph.FullCopy();
                            this.EditedGraph.ValidatePreprocess(out this._shaderErrors, out this._shaderWarnings);
                        }
                    }

                    if (mOverWin)
                    {
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                        {
                            if (this._outMoved != null)
                            {
                                this._ctxOutput = this._outMoved;
                            }

                            bOpenMenu = true;
                            this._lmbDown = false;
                            this._nodeMoved = null;
                            this._inMoved = null;
                            this._outMoved = null;
                        }

                        if (!this._lmbDown && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                        {
                            this._lmbDown = true;
                            this._mouseInitialPosition = ImGui.GetMousePos();
                            this._cameraInitialLocation = this._cameraLocation;
                            this._moveMode = MoveMode.None;
                            if (nInOver == null && nOutOver == null && nodeOver == null)
                            {
                                this._moveMode = MoveMode.Camera;
                            }

                            if (nodeOver != null && nodeOverHeader)
                            {
                                if (mOverDel)
                                {
                                    this.EditedGraph.RemoveNode(nodeOver);
                                    nodeOver.IsDeleted = true;
                                    nOutOver = null;
                                    nInOver = null;
                                    this.EditedGraph.ValidatePreprocess(out this._shaderErrors, out this._shaderWarnings);
                                }
                                else
                                {
                                    this._moveMode = MoveMode.ShaderNode;
                                    this._nodeMoved = nodeOver;
                                    this._nodeInitialPosition = nodeOver.Location.SystemVector();
                                }
                            }

                            if (nOutOver != null)
                            {
                                this._moveMode = MoveMode.NodeConnectionOut;
                                this._outMoved = nOutOver;
                                this._nodeInitialPosition = this._mouseInitialPosition;
                                this._nodeMoved = nodeOver ?? this.EditedGraph.AllOutputsById[nOutOver.ID].Item1;
                            }

                            if (nInOver != null)
                            {
                                this._moveMode = MoveMode.NodeConnectionIn;
                                this._inMoved = nInOver;
                                this._nodeInitialPosition = this._mouseInitialPosition;
                                this._nodeMoved = nodeOver ?? this.EditedGraph.AllInputsById[nInOver.ID].Item1;
                            }
                        }

                        if (this._lmbDown)
                        {
                            SVec2 cMP = ImGui.GetMousePos();
                            SVec2 mouseDelta = cMP - this._mouseInitialPosition;
                            switch (this._moveMode)
                            {
                                case MoveMode.Camera:
                                {
                                    this._cameraLocation = this._cameraInitialLocation + mouseDelta;
                                    break;
                                }

                                case MoveMode.ShaderNode:
                                {
                                    bool shouldSnap = Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftAlt) || Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.RightAlt);
                                    if (shouldSnap)
                                    {
                                        SVec2 m = this._nodeInitialPosition + mouseDelta;
                                        float nX = m.X;
                                        float nY = m.Y;
                                        nX = MathF.Floor(nX / 32) * 32;
                                        nY = MathF.Floor(nY / 32) * 32;
                                        this._nodeMoved.Location = new Vector2(nX, nY);
                                    }
                                    else
                                    {
                                        this._nodeMoved.Location = (this._nodeInitialPosition + mouseDelta).GLVector();
                                    }

                                    break;
                                }

                                case MoveMode.NodeConnectionIn:
                                {
                                    this._inMoved.ConnectedOutput = Guid.Empty;
                                    break;
                                }
                            }
                        }
                    }

                    SVec2 winSize = ImGui.GetWindowSize();
                    SVec2 bl = initialScreen + new SVec2(-4, winSize.Y - 51);
                    uint backClrV4 = ImGui.GetColorU32(ImGuiCol.WindowBg);
                    drawPtr.AddRectFilled(bl, bl + new SVec2(winSize.X, 20), backClrV4);
                    drawPtr.AddText(bl + new SVec2(32, 0), Color.DarkRed.Abgr(), "⮿");
                    drawPtr.AddText(bl + new SVec2(50, 0), Color.White.Abgr(), $"{this._shaderErrors.Count}");
                    if (ImGui.IsMouseHoveringRect(bl + new SVec2(32, 0), bl + new SVec2(50, 18)))
                    {
                        ImGui.BeginTooltip();
                        foreach (string err in this._shaderErrors)
                        {
                            int atIdx = err.IndexOf('@');
                            if (atIdx == -1)
                            {
                                ImGui.BulletText(lang.Translate(err));
                            }
                            else
                            {
                                ImGui.BulletText(lang.Translate(err.Substring(0, atIdx), err.Substring(atIdx + 1)));
                            }
                        }

                        ImGui.EndTooltip();
                    }

                    drawPtr.AddText(bl + new SVec2(100, 0), Color.Yellow.Abgr(), "⚠");
                    drawPtr.AddText(bl + new SVec2(118, 0), Color.White.Abgr(), $"{this._shaderWarnings.Count}");
                    if (ImGui.IsMouseHoveringRect(bl + new SVec2(100, 0), bl + new SVec2(118, 18)))
                    {
                        ImGui.BeginTooltip();
                        foreach (string warn in this._shaderWarnings)
                        {
                            int atIdx = warn.IndexOf('@');
                            if (atIdx == -1)
                            {
                                ImGui.BulletText(lang.Translate(warn));
                            }
                            else
                            {
                                ImGui.BulletText(lang.Translate(warn.Substring(0, atIdx), warn.Substring(atIdx + 1)));
                            }
                        }

                        ImGui.EndTooltip();
                    }

                    //drawPtr.AddImage(Client.Instance.Frontend.Renderer.GuiRenderer.ErrorIcon, bl, bl + new SVec2(20, 20));

                    ImGui.SetCursorPos(initialScreen + new SVec2(winSize.X - 70, winSize.Y - 56));
                    bOk = ImGui.Button(lang.Translate("ui.generic.ok"), new SVec2(60, 24));
                    ImGui.SetCursorPos(initialScreen + new SVec2(winSize.X - 140, winSize.Y - 56));
                    bCancel = ImGui.Button(lang.Translate("ui.generic.cancel"), new SVec2(60, 24));
                    if (this._extraTexturesOpen)
                    {
                        ImGui.SetCursorPosX(ImGui.GetWindowSize().X - 342);
                        ImGui.SetCursorPosY(22);
                        if (ImGui.ArrowButton("btn_collapse_extratex", ImGuiDir.Right))
                        {
                            this._extraTexturesOpen = false;
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.shader.extra_textures.collapse"));
                        }
                    }
                    else
                    {
                        ImGui.SetCursorPosX(ImGui.GetWindowSize().X - 26);
                        ImGui.SetCursorPosY(22);
                        if (ImGui.ArrowButton("btn_collapse_extratex", ImGuiDir.Left))
                        {
                            this._extraTexturesOpen = true;
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(lang.Translate("ui.shader.extra_textures.reveal"));
                        }
                    }
                }

                ImGui.End();

                if (bOpenMenu)
                {
                    ImGui.OpenPopup("ShaderGraphAddNewObject");
                }

                if (ImGui.BeginPopupContextWindow("ShaderGraphAddNewObject", ImGuiPopupFlags.MouseButtonRight))
                {
                    SVec2 popupPos = ImGui.GetWindowPos();
                    void RecursivelyPopulateMenu(ShaderTemplateCategory cat)
                    {
                        foreach (ShaderTemplateCategory c in cat.Children)
                        {
                            if (ImGui.BeginMenu(c.Name))
                            {
                                RecursivelyPopulateMenu(c);
                                ImGui.EndMenu();
                            }
                        }

                        foreach (ShaderNodeTemplate t in cat.Templates)
                        {
                            if (t.Deletable)
                            {
                                if (ImGui.MenuItem(t.Name))
                                {
                                    ShaderNode n = t.CreateNode();
                                    n.Location = (popupPos - this._cameraLocation).GLVector();
                                    this.EditedGraph.AddNode(n);
                                    if (this._ctxOutput != null && n.Inputs.Count > 0)
                                    {
                                        n.Inputs[0].ConnectedOutput = this._ctxOutput.ID;
                                        this._ctxOutput = null;
                                    }

                                    this.EditedGraph.ValidatePreprocess(out this._shaderErrors, out this._shaderWarnings);
                                }
                            }
                        }
                    }

                    foreach (ShaderTemplateCategory root in ShaderTemplateCategory.Roots)
                    {
                        if (ImGui.BeginMenu(root.Name))
                        {
                            RecursivelyPopulateMenu(root);
                            ImGui.EndMenu();
                        }
                    }

                    ImGui.EndPopup();
                }

                if (bOk || bCancel)
                {
                    this.popupState = false;
                    if (bOk)
                    {
                        if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(shaderId, AssetType.Shader, out Asset a) == AssetStatus.Return && (a.Shader?.NodeGraph?.IsLoaded ?? false))
                        {
                            using MemoryStream ms = new MemoryStream();
                            using BinaryWriter bw = new BinaryWriter(ms);
                            this.EditedGraph.Serialize().Write(bw);
                            byte[] bin = ms.ToArray();
                            using Image<Rgba32> img = new Image<Rgba32>(256, 256, new Rgba32(0, 0, 0, 1.0f));
                            using MemoryStream imgMs = new MemoryStream();
                            img.SaveAsPng(imgMs);
                            new PacketAssetUpdate() { AssetID = shaderId, NewBinary = a.ToBinary(bin), NewPreviewBinary = imgMs.ToArray() }.Send();
                        }
                        else
                        {
                            Client.Instance.Logger.Log(LogLevel.Error, "Edited shader data without shader asset present on the client! Shader ID was " + shaderId);
                        }
                    }
                }
            }

            if (this._lmbDown && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (this._moveMode == MoveMode.NodeConnectionIn && this._inMoved != null && nOutOver != null)
                {
                    this._inMoved.ConnectedOutput = nOutOver.ID;
                }
                else
                {
                    if (this._moveMode == MoveMode.NodeConnectionOut && this._outMoved != null && nInOver != null)
                    {
                        nInOver.ConnectedOutput = this._outMoved.ID;
                    }
                }

                this.EditedGraph.ValidatePreprocess(out this._shaderErrors, out this._shaderWarnings);
                this._lmbDown = false;
                this._nodeMoved = null;
                this._inMoved = null;
                this._outMoved = null;
            }

            if (nodeOver != null || nOutOver != null)
            {
                int nodeIndex;
                ShaderNode node = nodeOver;
                if (nodeOver != null)
                {
                    nodeIndex = nOutOver != null && nodeOver.Outputs.Contains(nOutOver) ? nodeOver.Outputs.IndexOf(nOutOver) : 0;
                }
                else
                {
                    if (this.EditedGraph.AllOutputsById.TryGetValue(nOutOver.ID, out (ShaderNode, NodeOutput) val))
                    {
                        nodeIndex = val.Item1.Outputs.IndexOf(nOutOver);
                        node = val.Item1;
                    }
                    else
                    {
                        nodeIndex = 0;
                        node = null;
                    }
                }

                if (node != null && (ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift)))
                {
                    int currentTexture = OGL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.TextureBinding2D);
                    this._nodeLookupTexture.Bind();
                    Image<Rgba32> img = this.EditedGraph.GetNodeImage(node, nodeIndex, out NodeSimulationMatrix matrix);
                    this._nodeLookupTexture.SetImage(img, OpenTK.Graphics.OpenGL.PixelInternalFormat.Rgba);
                    OGL.BindTexture(OpenTK.Graphics.OpenGL.TextureTarget.Texture2D, currentTexture);
                    img.Dispose();
                    ImGui.BeginTooltip();
                    ImGui.Text(lang.Translate("ui.shader.preview"));
                    ImGui.Image(this._nodeLookupTexture, new SVec2(128, 128));
                    ImGui.Text(lang.Translate("ui.shader.average", matrix.GetAverageValueAsString()));
                    ImGui.EndTooltip();
                }
            }
        }

        unsafe bool DrawAssetRecepticle(Guid aId, SimpleLanguage lang, Func<bool> assetEval, GL.Texture iconTex = null)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            var imScreenPos = ImGui.GetCursorScreenPos();
            var rectEnd = imScreenPos + new System.Numerics.Vector2(320, 24);
            bool mouseOver = ImGui.IsMouseHoveringRect(imScreenPos, rectEnd);
            uint bClr = mouseOver ? GuiRenderer.Instance.DraggedAssetReference != null && assetEval() ? ImGui.GetColorU32(ImGuiCol.HeaderHovered) : ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Border);
            drawList.AddRect(imScreenPos, rectEnd, bClr);
            drawList.AddImage(iconTex ?? GuiRenderer.Instance.AssetModelIcon, imScreenPos + new System.Numerics.Vector2(4, 4), imScreenPos + new System.Numerics.Vector2(20, 20));
            string mdlTxt = "";
            int mdlTxtOffset = 0;
            if (Client.Instance.AssetManager.Refs.ContainsKey(aId))
            {
                AssetRef aRef = Client.Instance.AssetManager.Refs[aId];
                mdlTxt += aRef.Name;
                if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestPreview(aId, out AssetPreview ap) == AssetStatus.Return && ap != null)
                {
                    GL.Texture tex = ap.GetGLTexture();
                    if (tex != null)
                    {
                        drawList.AddImage(tex, imScreenPos + new System.Numerics.Vector2(20, 4), imScreenPos + new System.Numerics.Vector2(36, 20));
                        mdlTxtOffset += 20;
                    }
                }
            }

            if (Guid.Equals(Guid.Empty, aId))
            {
                mdlTxt = lang.Translate("generic.none");
            }
            else
            {
                mdlTxt += " (" + aId.ToString() + ")\0";
            }

            drawList.PushClipRect(imScreenPos, rectEnd);
            drawList.AddText(imScreenPos + new System.Numerics.Vector2(20 + mdlTxtOffset, 4), ImGui.GetColorU32(ImGuiCol.Text), mdlTxt);
            drawList.PopClipRect();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 28);
            return mouseOver;
        }

        private void ShaderLine(ImDrawListPtr drawPtr, SVec2 from, NodeValueType valFrom, SVec2 to, float xOffset, NodeValueType valTo, bool mOverAny)
        {
            Color cF = GetColorForType(valFrom);
            Color cT = GetColorForType(valTo);
            uint lineColorFrom = cF.Abgr();
            uint lineColorTo = cT.Abgr();
            uint lineColorAvgTo = cT.Mix(cF, 0.75f).Abgr();
            uint lineColorAvgFrom = cF.Mix(cT, 0.75f).Abgr();
            if (mOverAny)
            {
                lineColorFrom = lineColorTo = lineColorAvgFrom = lineColorAvgTo = Color.RoyalBlue.Abgr();
            }

            CubicBezier curve = this.GetCurve(from, to, 0.1f);
            drawPtr.AddBezierCubic(curve.P0, curve.P1, curve.P2, curve.P3, lineColorFrom, 2f);
            //drawPtr.AddLine(to, to + new SVec2(-10 + xOffset, 0), lineColorTo);
            //drawPtr.AddLine(from, from + new SVec2(10, 0), lineColorFrom);
            //drawPtr.AddLine(new SVec2(to.X - 10 + xOffset, to.Y), new SVec2(to.X - 10 + xOffset, from.Y), lineColorAvgFrom);
            //drawPtr.AddLine(new SVec2(to.X - 10 + xOffset, from.Y), new SVec2(from.X + 10, from.Y), lineColorAvgTo);
        }

        // https://github.com/Nelarius/imnodes/blob/master/imnodes.cpp#L116
        private CubicBezier GetCurve(SVec2 from, SVec2 to, float segmentLength)
        {
            float length = SVec2.Distance(from, to);
            SVec2 offset = new SVec2(0.25f * length, 0.0f);
            return new CubicBezier(
                from,
                from + offset,
                to - offset,
                to,
                Math.Max(1, (int)(length * segmentLength))
            );
        }

        public static Color GetColorForType(NodeValueType nT)
        {
            return nT switch
            {
                NodeValueType.Bool => Color.MediumAquamarine,
                NodeValueType.Int => Color.MediumSeaGreen,
                NodeValueType.UInt => Color.MediumSpringGreen,
                NodeValueType.Float => Color.LimeGreen,
                NodeValueType.Vec2 => Color.Khaki,
                NodeValueType.Vec3 => Color.Gold,
                NodeValueType.Vec4 => Color.Goldenrod,
                _ => Color.White
            };
        }

        private struct CubicBezier
        {
            public SVec2 P0;
            public SVec2 P1;
            public SVec2 P2;
            public SVec2 P3;
            public int NSegments;

            public CubicBezier(SVec2 p0, SVec2 p1, SVec2 p2, SVec2 p3, int nSegments)
            {
                this.P0 = p0;
                this.P1 = p1;
                this.P2 = p2;
                this.P3 = p3;
                this.NSegments = nSegments;
            }
        }
    }
}
