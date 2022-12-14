namespace VTT.Render.Gui
{
    using ImGuiNET;
    using OpenTK.Mathematics;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using VTT.Asset;
    using VTT.Asset.Shader.NodeGraph;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Util;
    using SVec2 = System.Numerics.Vector2;
    using SVec4 = System.Numerics.Vector4;

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

        private SVec2 _cameraLocation = SVec2.Zero;
        private bool _lmbDown;
        private SVec2 _mouseInitialPosition;
        private SVec2 _cameraInitialLocation;
        private SVec2 _nodeInitialPosition;
        private ShaderNode _nodeMoved;
        private MoveMode _moveMode;
        private NodeInput _inMoved;
        private NodeOutput _outMoved;

        private List<string> _shaderErrors = new List<string>();
        private List<string> _shaderWarnings = new List<string>();

        public ShaderGraph EditedGraph { get; set; }

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
                            Color nodeBack = Color.SlateGray.Darker(0.8f);
                            Color nodeHeader = Color.SlateGray.Darker(0.7f);
                            Color nodeHeaderHover = Color.SlateGray.Darker(0.2f);
                            Color nodeBorder = Color.SlateGray.Darker(0.4f);
                            Color nodeBorderHover = Color.RoyalBlue;
                            foreach (ShaderNode n in this.EditedGraph.Nodes)
                            {
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

                                drawPtr.AddRectFilled(screenPos, screenPos + n.Size.SystemVector(), nodeBack.Abgr());
                                drawPtr.AddRectFilled(screenPos, screenPos + new SVec2(n.Size.X, 20), mOverHeader ? nodeHeaderHover.Abgr() : nodeHeader.Abgr());
                                drawPtr.AddRect(screenPos, screenPos + n.Size.SystemVector(), mOver ? nodeBorderHover.Abgr() : nodeBorder.Abgr());
                                drawPtr.AddText(screenPos + new SVec2(8, 0), Color.White.Abgr(), n.Name);
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
                                    bool mOverInput = ImGui.IsMouseHoveringRect(nSPos + new SVec2(-3, yOffset - 3), nSPos + new SVec2(3, yOffset + 3));
                                    if (mOverInput)
                                    {
                                        nInOver = ni;
                                    }

                                    drawPtr.AddCircle(nSPos + new SVec2(0, yOffset), 6, mOverInput ? Color.RoyalBlue.Abgr() : Color.Orange.Abgr());
                                    drawPtr.AddCircleFilled(nSPos + new SVec2(0, yOffset), 4, GetColorForType(ni.SelfType).Abgr());
                                    drawPtr.AddText(nSPos + new SVec2(12, yOffset - 10), Color.White.Abgr(), ni.Name);
                                    if (!ni.ConnectedOutput.Equals(Guid.Empty))
                                    {
                                        if (this.EditedGraph.AllOutputsById.TryGetValue(ni.ConnectedOutput, out (ShaderNode, NodeOutput) data))
                                        {
                                            int outOffset = data.Item1.Inputs.Count * 20 + data.Item1.Outputs.IndexOf(data.Item2) * 20;
                                            SVec2 oSPos = padding + data.Item1.Location.SystemVector() + new SVec2(data.Item1.Size.X, 30 + outOffset);
                                            bool mOverOutput = ImGui.IsMouseHoveringRect(padding + data.Item1.Location.SystemVector(), padding + data.Item1.Location.SystemVector() + data.Item1.Size.SystemVector()) || ImGui.IsMouseHoveringRect(oSPos + new SVec2(-3, -3), oSPos + new SVec2(3, 3));
                                            SVec2 iSPos = nSPos + new SVec2(0, yOffset);
                                            int xOffset = iIndex * -5;
                                            Color lineColor = mOver || mOverInput || mOverOutput ? Color.RoyalBlue : Color.White;
                                            this.ShaderLine(drawPtr, oSPos, data.Item2.SelfType, iSPos, xOffset, ni.SelfType, mOver || mOverInput || mOverOutput);
                                        }
                                    }

                                    yOffset += 20;
                                    ++iIndex;
                                }

                                nSPos = screenPos + new SVec2(n.Size.X, 0);
                                foreach (NodeOutput no in n.Outputs)
                                {
                                    bool mOverThisOut = ImGui.IsMouseHoveringRect(nSPos + new SVec2(-3, yOffset - 3), nSPos + new SVec2(3, yOffset + 3));
                                    if (mOverThisOut)
                                    {
                                        nOutOver = no;
                                    }

                                    drawPtr.AddCircle(nSPos + new SVec2(0, yOffset), 6, mOverThisOut ? Color.RoyalBlue.Abgr() : Color.Blue.Abgr());
                                    drawPtr.AddCircleFilled(nSPos + new SVec2(0, yOffset), 4, GetColorForType(no.SelfType).Abgr());
                                    SVec2 ts = ImGui.CalcTextSize(no.Name);
                                    drawPtr.AddText(nSPos + new SVec2(-12 - ts.X, yOffset - 10), Color.White.Abgr(), no.Name);
                                    yOffset += 20;
                                }
                            }
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
                            bOpenMenu = true;
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
                                    this._nodeMoved.Location = (this._nodeInitialPosition + mouseDelta).GLVector();
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
                    drawPtr.AddText(bl + new SVec2(50, 0), Color.White.Abgr(), $"{ this._shaderErrors.Count }");
                    drawPtr.AddText(bl + new SVec2(100, 0), Color.Yellow.Abgr(), "⚠");
                    drawPtr.AddText(bl + new SVec2(118, 0), Color.White.Abgr(), $"{ this._shaderWarnings.Count }");
                    //drawPtr.AddImage(Client.Instance.Frontend.Renderer.GuiRenderer.ErrorIcon, bl, bl + new SVec2(20, 20));

                    ImGui.SetCursorPos(initialScreen + new SVec2(winSize.X - 70, winSize.Y - 56));
                    bOk = ImGui.Button(lang.Translate("ui.generic.ok"), new SVec2(60, 24));
                    ImGui.SetCursorPos(initialScreen + new SVec2(winSize.X - 140, winSize.Y - 56));
                    bCancel = ImGui.Button(lang.Translate("ui.generic.cancel"), new SVec2(60, 24));
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

            drawPtr.AddLine(to, to + new SVec2(-10 + xOffset, 0), lineColorTo);
            drawPtr.AddLine(from, from + new SVec2(10, 0), lineColorFrom);
            drawPtr.AddLine(new SVec2(to.X - 10 + xOffset, to.Y), new SVec2(to.X - 10 + xOffset, from.Y), lineColorAvgFrom);
            drawPtr.AddLine(new SVec2(to.X - 10 + xOffset, from.Y), new SVec2(from.X + 10, from.Y), lineColorAvgTo);
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
    }
}
