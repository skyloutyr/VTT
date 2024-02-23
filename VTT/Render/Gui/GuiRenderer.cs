namespace VTT.Render.Gui
{
    using ImGuiNET;
    using Newtonsoft.Json.Linq;
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VTT.Asset;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Render.MainMenu;
    using VTT.Util;
    using Extensions = Util.Extensions;

    public partial class GuiRenderer
    {
        public AssetDirectory CurrentFolder { get; set; }
        public Random Random { get; set; } = new Random();

        #region Textures
        public Texture FolderIcon { get; set; }
        public Texture AddIcon { get; set; }
        public Texture BackIcon { get; set; }
        public Texture ErrorIcon { get; set; }
        public Texture NoImageIcon { get; set; }
        public Texture LoadingSpinner { get; set; }
        public Texture GotoIcon { get; set; }
        public Texture MoveToIcon { get; set; }
        public Texture MoveAllToIcon { get; set; }
        public Texture DeleteIcon { get; set; }
        public Texture RollIcon { get; set; }
        public Texture CrossedSwordsIcon { get; set; }
        public Texture MagicIcon { get; set; }
        public Texture VerbalComponentIcon { get; set; }
        public Texture SomaticComponentIcon { get; set; }
        public Texture MaterialComponentIcon { get; set; }
        public Texture RitualComponentIcon { get; set; }
        public Texture ConcentrationComponentIcon { get; set; }

        public Texture AssetModelIcon { get; set; }
        public Texture AssetImageIcon { get; set; }
        public Texture AssetShaderIcon { get; set; }
        public Texture AssetParticleIcon { get; set; }
        public Texture AssetSoundIcon { get; set; }
        public Texture AssetMusicIcon { get; set; }
        public Texture AssetCompressedMusicIcon { get; set; }

        public Texture Select { get; set; }
        public Texture Translate { get; set; }
        public Texture Rotate { get; set; }
        public Texture Scale { get; set; }
        public Texture ChangeFOW { get; set; }
        public Texture ToggleTurnOrder { get; set; }
        public Texture Measure { get; set; }
        public Texture PlayerStop { get; set; }
        public Texture PlayerNext { get; set; }

        public Texture MoveGizmo { get; set; }
        public Texture MoveArrows { get; set; }

        public Texture FOWRevealIcon { get; set; }
        public Texture FOWHideIcon { get; set; }
        public Texture FOWModeBox { get; set; }
        public Texture FOWModePolygon { get; set; }
        public Texture FOWModeBrush { get; set; }

        public Texture MeasureModeRuler { get; set; }
        public Texture MeasureModeCircle { get; set; }
        public Texture MeasureModeSphere { get; set; }
        public Texture MeasureModeSquare { get; set; }
        public Texture MeasureModeCube { get; set; }
        public Texture MeasureModeCone { get; set; }
        public Texture MeasureModeLine { get; set; }
        public Texture MeasureModeWall { get; set; }
        public Texture MeasureModePolyline { get; set; }
        public Texture MeasureModeErase { get; set; }

        public Texture ChatSimpleRollImage { get; set; }
        public Texture ChatSendImage { get; set; }
        public Texture ChatLinkImage { get; set; }
        public Texture JournalEdit { get; set; }

        public Texture PlayIcon { get; set; }
        public Texture PauseIcon { get; set; }

        public Texture TurnTrackerBackground { get; set; }
        public Texture TurnTrackerBackgroundNoObject { get; set; }
        public Texture TurnTrackerForeground { get; set; }
        public Texture TurnTrackerHighlighter { get; set; }
        public Texture TurnTrackerSeparator { get; set; }
        public Texture TurnTrackerParticle { get; set; }

        public Texture StatusAtlas { get; set; }

        public Texture NetworkIn { get; set; }
        public Texture NetworkOut { get; set; }

        public Texture CameraMove { get; set; }
        public Texture CameraRotate { get; set; }

        public int LoadingSpinnerFrames { get; set; }
        #endregion

        #region ImGui Reference Parameters

        private int _lastLogNum;
        private string _newFolderNameString = string.Empty;
        private AssetDirectory _contextDir;
        private bool _mouseOverAssets;
        private bool _lmbDown;

        private int _editedBarIndex;
        private MapObject _editedMapObject;
        private System.Numerics.Vector4 _editedBarColor;

        private System.Numerics.Vector4 _editedMapColor;
        private int _editedMapColorIndex;

        private System.Numerics.Vector4 _editedTeamColor;
        private string _editedTeamName;

        private AssetRef _draggedRef;
        public AssetRef DraggedAssetReference => this._draggedRef;

        private AssetRef _editedRef;
        private Guid _editedParticleSystemId;
        private Guid _editedShaderId;

        private Guid _deletedMapId;
        private string _chatString = string.Empty;

        private System.Numerics.Vector2 _chatClientRect = default;

        private bool _needsRefocusChat = false;

        private int _lastChatLinesRendered;
        private float _scrollYLast;

        private long _lastChatRequest;

        private int _numDiceSingular = 1;
        private int _numDiceSeparate = 1;

        private int _numDiceCustom = 1;
        private int _dieSideCustom = 20;
        private int _dieExtraCustom = 0;

        private bool _showingTurnOrder = false;
        private bool _turnTrackerCollapsed = false;

        private MapObject _inspectedObject;
        private MapObject _mouseOverWhenClicked;

        private string[] _teams = Array.Empty<string>();
        private string[] _objects = Array.Empty<string>();

        private string _imgUrl = string.Empty;
        private int _imgWidth = 340;
        private int _imgHeight = 280;
        private string _imgTooltip = string.Empty;

        private TextJournal _editedJournal;
        private bool _journalTextEdited;

        private bool _statusOpen;
        private float _statusStepX;
        private float _statusStepY;
        private string _statusSortString = string.Empty;
        private readonly List<(string, float, float)> _sortedStatuses = new List<(string, float, float)>();
        private readonly List<(string, float, float)> _allStatuses = new List<(string, float, float)>();

        #endregion

        #region Draw Consts

        private static readonly System.Numerics.Vector2 Vec12x12 = new System.Numerics.Vector2(12, 12);
        private static readonly System.Numerics.Vector2 Vec24x24 = new System.Numerics.Vector2(24, 24);
        private static readonly System.Numerics.Vector2 Vec32x32 = new System.Numerics.Vector2(32, 32);
        private static readonly System.Numerics.Vector2 Vec56x0 = new(56, 0);
        private static readonly System.Numerics.Vector2 Vec56x70 = new(56, 70);
        private static readonly System.Numerics.Vector2 Vec320x70 = new(320, 70);
        private static readonly System.Numerics.Vector2 Vec48x60 = new System.Numerics.Vector2(48, 60);
        private static readonly System.Numerics.Vector2 Vec48x36 = new System.Numerics.Vector2(48, 36);

        private static readonly System.Numerics.Vector4 ImColBlack = new System.Numerics.Vector4(0, 0, 0, 1);

        #endregion

        public MainMenuRenderer MainMenuRenderer { get; set; }
        public ParticleEditorRenderer ParticleEditorRenderer { get; set; }
        public ShaderGraphEditorRenderer ShaderEditorRenderer { get; set; }
        public bool DebugEnabled { get; set; }

        public void Create()
        {
            this.CurrentFolder = Client.Instance.AssetManager.Root;
            this.FolderIcon = OpenGLUtil.LoadUIImage("icons8-folder-40");
            this.BackIcon = OpenGLUtil.LoadUIImage("icons8-return-40");
            this.AddIcon = OpenGLUtil.LoadUIImage("icons8-plus-math-40");
            this.ErrorIcon = OpenGLUtil.LoadUIImage("icons8-error-40");
            this.NoImageIcon = OpenGLUtil.LoadUIImage("icons8-no-image-40");
            this.GotoIcon = OpenGLUtil.LoadUIImage("icons8-day-camera-40");
            this.MoveToIcon = OpenGLUtil.LoadUIImage("icons8-curved-arrow-40");
            this.MoveAllToIcon = OpenGLUtil.LoadUIImage("icons8-move-all-arrow-40");
            this.DeleteIcon = OpenGLUtil.LoadUIImage("icons8-trash-can-40");
            this.RollIcon = OpenGLUtil.LoadUIImage("icons8-dice-40");
            this.LoadingSpinner = OpenGLUtil.LoadUIImage("icons8-loading-circle");
            this.ToggleTurnOrder = OpenGLUtil.LoadUIImage("icons8-stopwatch-40");
            this.CrossedSwordsIcon = OpenGLUtil.LoadUIImage("icons8-swords-40");
            this.MagicIcon = OpenGLUtil.LoadUIImage("icons8-magic-64");
            this.VerbalComponentIcon = OpenGLUtil.LoadUIImage("icons8-lips-40");
            this.SomaticComponentIcon = OpenGLUtil.LoadUIImage("icons8-so-so-40");
            this.MaterialComponentIcon = OpenGLUtil.LoadUIImage("icons8-money-bag-40");
            this.RitualComponentIcon = OpenGLUtil.LoadUIImage("icons8-pentagram-64");
            this.ConcentrationComponentIcon = OpenGLUtil.LoadUIImage("icons8-thinking-male-40");
            this.Measure = this.MeasureModeRuler = OpenGLUtil.LoadUIImage("icons8-length-40");

            this.AssetModelIcon = OpenGLUtil.LoadUIImage("icons8-3d-64");
            this.AssetImageIcon = OpenGLUtil.LoadUIImage("icons8-picture-40");
            this.AssetShaderIcon = OpenGLUtil.LoadUIImage("icons8-color-swatch-40");
            this.AssetParticleIcon = OpenGLUtil.LoadUIImage("icons8-particle-40");
            this.AssetSoundIcon = OpenGLUtil.LoadUIImage("icons8-sound-40");
            this.AssetMusicIcon = OpenGLUtil.LoadUIImage("icons8-musical-notes-40");
            this.AssetCompressedMusicIcon = OpenGLUtil.LoadUIImage("icons8-music-library-40");

            this.Select = OpenGLUtil.LoadUIImage("icons8-cursor-40");
            this.Translate = OpenGLUtil.LoadUIImage("icons8-drag-40");
            this.Rotate = OpenGLUtil.LoadUIImage("icons8-process-40");
            this.Scale = OpenGLUtil.LoadUIImage("icons8-resize-40");
            this.ChangeFOW = OpenGLUtil.LoadUIImage("icons8-visualy-impaired-40");
            this.PlayerStop = OpenGLUtil.LoadUIImage("icons8-stop-40");
            this.PlayerNext = OpenGLUtil.LoadUIImage("icons8-double-right-40");

            this.MoveGizmo = OpenGLUtil.LoadUIImage("icons8-abscissa-40");
            this.MoveArrows = OpenGLUtil.LoadUIImage("icons8-move-separate-40");

            this.FOWRevealIcon = OpenGLUtil.LoadUIImage("icons8-eye-40");
            this.FOWHideIcon = OpenGLUtil.LoadUIImage("icons8-closed-eye-40");
            this.FOWModeBox = OpenGLUtil.LoadUIImage("icons8-rectangle-40");
            this.FOWModePolygon = OpenGLUtil.LoadUIImage("icons8-radar-plot-40");
            this.FOWModeBrush = OpenGLUtil.LoadUIImage("icons8-paint-40");

            this.TurnTrackerBackground = OpenGLUtil.LoadUIImage("turn-tracker-back");
            this.TurnTrackerBackgroundNoObject = OpenGLUtil.LoadUIImage("turn-tracker-back-no-object");
            this.TurnTrackerForeground = OpenGLUtil.LoadUIImage("turn-tracker-front");
            this.TurnTrackerHighlighter = OpenGLUtil.LoadUIImage("turn-tracker-highlighter-back");
            this.TurnTrackerSeparator = OpenGLUtil.LoadUIImage("turn-tracker-separator");
            this.TurnTrackerParticle = OpenGLUtil.LoadUIImage("turn-tracker-particle");

            this.MeasureModeCircle = OpenGLUtil.LoadUIImage("icons8-radius-40");
            this.MeasureModeSphere = OpenGLUtil.LoadUIImage("icons8-sphere-58");
            this.MeasureModeSquare = OpenGLUtil.LoadUIImage("icons8-surface-40");
            this.MeasureModeCube = OpenGLUtil.LoadUIImage("icons8-cube-40");
            this.MeasureModeCone = OpenGLUtil.LoadUIImage("icons8-pipeline-40");
            this.MeasureModeLine = OpenGLUtil.LoadUIImage("icons8-vertical-line-40");
            this.MeasureModeWall = OpenGLUtil.LoadUIImage("icons8-block-40");
            this.MeasureModePolyline = OpenGLUtil.LoadUIImage("icons8-polyline-40");
            this.MeasureModeErase = OpenGLUtil.LoadUIImage("icons8-erase-40");

            this.ChatSimpleRollImage = OpenGLUtil.LoadUIImage("icons8-dice-60");
            this.ChatSendImage = OpenGLUtil.LoadUIImage("icons8-paper-plane-40");
            this.ChatLinkImage = OpenGLUtil.LoadUIImage("icons8-link-picture-40");
            this.JournalEdit = OpenGLUtil.LoadUIImage("icons8-edit-40");

            this.PlayIcon = OpenGLUtil.LoadUIImage("icons8-play-40");
            this.PauseIcon = OpenGLUtil.LoadUIImage("icons8-pause-40");

            this.NetworkIn = OpenGLUtil.LoadUIImage("icons8-incoming-data-40");
            this.NetworkOut = OpenGLUtil.LoadUIImage("icons8-outgoing-data-40");

            this.CameraMove = OpenGLUtil.LoadUIImage("icons8-video-camera-move-40");
            this.CameraRotate = OpenGLUtil.LoadUIImage("icons8-video-camera-rotate-40");

            this._modeTextures = new Texture[] { this.Select, this.Translate, this.Rotate, this.Scale, this.ChangeFOW, this.Measure, this.FOWModeBrush };
            this._rulerModeTextures = new Texture[] { this.MeasureModeRuler, this.MeasureModeCircle, this.MeasureModeSphere, this.MeasureModeSquare, this.MeasureModeCube, this.MeasureModeLine, this.MeasureModeCone, this.MeasureModePolyline, this.MeasureModeErase };
            this.LoadingSpinnerFrames = (int)MathF.Ceiling((float)this.LoadingSpinner.Size.Width / this.LoadingSpinner.Size.Height);

            this.MainMenuRenderer = new MainMenuRenderer();
            this.MainMenuRenderer.Create();
            this.ParticleEditorRenderer = new ParticleEditorRenderer();
            this.ShaderEditorRenderer = new ShaderGraphEditorRenderer();
            this.ShaderEditorRenderer.Create();
            this.DebugEnabled = ArgsManager.TryGetValue<bool>("debug", out _);
            this.LoadStatuses();
        }

        private Texture[] _modeTextures;
        private Texture[] _rulerModeTextures;

        private readonly List<string> _chat = new List<string>();
        private int _cChatIndex;
        private bool _showImGuiDemoWindow = false;
        public GuiState FrameState { get; } = new GuiState();
        public DoubleBufferedStopwatch Timer { get; } = new DoubleBufferedStopwatch();

        public unsafe void RenderEarly(double time)
        {
        }

        public unsafe void Render(double time)
        {
            this.Timer.Restart();
            Map cMap = Client.Instance.CurrentMap;
            MapObjectRenderer mor = Client.Instance.Frontend.Renderer.ObjectRenderer;
            SimpleLanguage lang = Client.Instance.Lang;
            ImGuiWindowFlags window_flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;

            #region Frame Setup
            this._mouseOverAssets = false;

            if (Client.Instance.NetClient == null || !Client.Instance.NetClient.IsConnected)
            {
                this.MainMenuRenderer.Render(ref this.showDisconnect, time);
                return;
            }

            if (this.CurrentFolder.Parent != null)
            {
                if (!this.CurrentFolder.Parent.Directories.Contains(this.CurrentFolder)) // Race condition prevention
                {
                    this.CurrentFolder = this.CurrentFolder.Parent;
                }
            }

            this.FrameState.Reset();
            #endregion

            if (this._showImGuiDemoWindow)
            {
                ImGui.ShowDemoWindow(ref this._showImGuiDemoWindow);
            }

            this.RenderSidebar(lang, window_flags, mor);
            this.RenderDebugInfo(time, window_flags);
            this.RenderFOWControls(mor, lang, window_flags);
            this.RenderTranslationControls(mor, lang, window_flags);
            this.RenderCameraControls(mor, lang, window_flags);
            this.RenderMeasureControls(mor, lang, window_flags);
            this.RenderDrawControls(mor, lang, window_flags);
            this.RenderChat(lang, this.FrameState);
            this.RenderMaps(lang, this.FrameState);
            this.RenderObjectProperties(lang, this.FrameState, time);
            this.RenderObjectsList(this.FrameState, lang);
            this.RenderJournals(lang, this.FrameState);
            this.RenderAssets(lang, this.FrameState);
            this.RenderNetworkAdminPanel(lang, this.FrameState);
            this.RenderMusicPlayer(lang, this.FrameState, time);
            this.ParticleEditorRenderer.Render(this._editedParticleSystemId, this._draggedRef, this.FrameState);
            this.ShaderEditorRenderer.Render(this._editedShaderId, this._draggedRef, this.FrameState);
            this.RenderLogs(lang);
            this.RenderTurnTrackerControls(cMap, lang, this.FrameState);
            this.RenderTurnTrackerOverlay(cMap, window_flags);
            this.RenderPerformanceMonitor(lang);

            string ok = lang.Translate("ui.generic.ok");
            string cancel = lang.Translate("ui.generic.cancel");
            string close = lang.Translate("ui.generic.close");
            this.RenderObjectOverlays();
            this.RenderDraggedAssetRef();

            if (this.FrameState.moveTo != null)
            {
                this.CurrentFolder = Client.Instance.AssetManager.GetDirAt(this.FrameState.moveTo.GetPath());
            }

            RulerRenderer rr = Client.Instance.Frontend.Renderer.RulerRenderer;
            if (rr != null && cMap != null)
            {
                foreach (RulerInfo ri in rr.ActiveInfos)
                {
                    if (!ri.IsDead && ri.DisplayInfo)
                    {
                        if (ri.KeepAlive)
                        {
                            Vector3 screen = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace((ri.Type == RulerType.Polyline ? ri.CumulativeCenter : ri.Start) + Vector3.UnitZ);
                            if (screen.Z >= 0)
                            {
                                float len = ri.Type == RulerType.Polyline ? ri.CumulativeLength * cMap.GridUnit : (ri.End - ri.Start).Length * cMap.GridUnit;
                                string text = len.ToString("0.00");
                                System.Numerics.Vector2 tLen = ImGuiHelper.CalcTextSize(ri.OwnerName);
                                System.Numerics.Vector2 tLen2 = ImGuiHelper.CalcTextSize(ri.Tooltip);
                                System.Numerics.Vector2 tLen3 = ImGuiHelper.CalcTextSize(text);
                                float maxW = MathF.Max(tLen.X, MathF.Max(tLen2.X, tLen3.X));
                                ImGui.SetNextWindowPos(screen.Xy.SystemVector() - (new System.Numerics.Vector2(maxW, tLen.Y) / 2));
                                ImGuiWindowFlags flags = ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings;
                                if (ImGui.Begin("TextOverlayData_" + ri.SelfID.ToString(), flags))
                                {
                                    float cX;
                                    float delta;
                                    if (tLen.X < maxW)
                                    {
                                        cX = ImGui.GetCursorPosX();
                                        delta = maxW - tLen.X;
                                        ImGui.SetCursorPosX(cX + (delta / 2));
                                    }

                                    ImGui.PushStyleColor(ImGuiCol.Text, ri.Color.Abgr());
                                    ImGui.TextUnformatted(ri.OwnerName);
                                    ImGui.PopStyleColor();
                                    if (!string.IsNullOrEmpty(ri.Tooltip))
                                    {
                                        if (tLen2.X < maxW)
                                        {
                                            cX = ImGui.GetCursorPosX();
                                            delta = maxW - tLen2.X;
                                            ImGui.SetCursorPosX(cX + (delta / 2));
                                        }

                                        ImGui.TextUnformatted(ri.Tooltip);
                                    }


                                    if (tLen3.X < maxW)
                                    {
                                        cX = ImGui.GetCursorPosX();
                                        delta = maxW - tLen3.X;
                                        ImGui.SetCursorPosX(cX + (delta / 2));
                                    }

                                    if (len > 0.01f)
                                    {
                                        ImGui.PushStyleColor(ImGuiCol.Text, ri.Color.Abgr());
                                        ImGui.TextUnformatted(text);
                                        ImGui.PopStyleColor();
                                    }
                                }

                                ImGui.End();
                            }
                        }
                        else
                        {
                            Vector3 half = ri.Type == RulerType.Polyline ? ri.CumulativeCenter : ri.Start + ((ri.End - ri.Start) / 2f);
                            Vector3 halfScreen = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.ToScreenspace(half);
                            if (halfScreen.Z >= 0)
                            {
                                float len = ri.Type == RulerType.Polyline ? ri.CumulativeLength * cMap.GridUnit : (ri.End - ri.Start).Length * cMap.GridUnit;
                                string text = len.ToString("0.00");
                                var tLen = ImGuiHelper.CalcTextSize(text);
                                ImGui.SetNextWindowPos(halfScreen.Xy.SystemVector() - (tLen / 2));
                                ImGuiWindowFlags flags = ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings;
                                if (ImGui.Begin("TextOverlayData_" + ri.SelfID.ToString(), flags))
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Text, ri.Color.Abgr());
                                    ImGui.TextUnformatted(text);
                                    ImGui.PopStyleColor();
                                }

                                ImGui.End();
                            }
                        }
                    }
                }
            }

            // Right click context menu
            if (!ImGui.GetIO().WantCaptureMouse && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                if (Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver != null)
                {
                    this._mouseOverWhenClicked = Client.Instance.Frontend.Renderer.ObjectRenderer.ObjectMouseOver;
                    ImGui.OpenPopup("Object Actions");
                }
            }

            // Copy-paste
            if (!ImGui.GetIO().WantCaptureMouse && !ImGui.GetIO().WantCaptureKeyboard && Client.Instance.Frontend.GameHandle.IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftControl))
            {
                if (Client.Instance.IsAdmin)
                {
                    bool copy = ImGui.IsKeyPressed(ImGuiKey.C);
                    bool paste = ImGui.IsKeyPressed(ImGuiKey.V);
                    if (copy)
                    {
                        CopyObjects(this.FrameState);
                    }

                    if (paste)
                    {
                        PasteObjects(this.FrameState);
                    }
                }
            }

            // Open ImGui context menu
            if (ImGui.BeginPopupContextWindow("Object Actions"))
            {
                MapObject mouseOver = this._mouseOverWhenClicked;
                bool hasSelected = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Contains(mouseOver);

                if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.focus") + "###Focus"))
                {
                    Vector3 camPos = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position;
                    Vector3 camDirection = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction;
                    Vector3 oPos = mouseOver.Position;
                    Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.MoveCamera(oPos - (camDirection * 5.0f), false);
                    Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.RecalculateData(assumedUpAxis: Vector3.UnitZ);
                    ImGui.CloseCurrentPopup();
                }

                if (hasSelected)
                {
                    if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.copy") + "###Copy"))
                    {
                        CopyObjects(this.FrameState);
                    }

                    if (!Client.Instance.IsAdmin)
                    {
                        ImGui.BeginDisabled();
                    }

                    if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.paste") + "###Paste"))
                    {
                        PasteObjects(this.FrameState);
                    }

                    if (ImGui.BeginMenu(lang.Translate("ui.popup.object_actions.move_to") + "###Move To"))
                    {
                        int l = -100;
                        bool click = false;
                        if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.move_to.-2") + "###(-2) Map Background"))
                        {
                            l = -2;
                            click = true;
                        }

                        if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.move_to.-1") + "###(-1) Map"))
                        {
                            l = -1;
                            click = true;
                        }

                        if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.move_to.0") + "###(0) Objects"))
                        {
                            l = 0;
                            click = true;
                        }

                        if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.move_to.1") + "(1) GM - Normal"))
                        {
                            l = 1;
                            click = true;
                        }

                        if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.move_to.2") + "##(2) GM - Obscure"))
                        {
                            l = 2;
                            click = true;
                        }

                        if (click)
                        {
                            PacketMapObjectGenericData pmogd = new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.MapLayer, Data = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Select(mo => (mo.MapID, mo.ID, (object)l)).ToList(), IsServer = false, Session = Client.Instance.SessionID };
                            pmogd.Send();
                        }

                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.add_turn") + "###Add Turn"))
                    {
                        for (int i = 0; i < Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Count; i++)
                        {
                            MapObject mo = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects[i];
                            new PacketAddTurnEntry() { AdditionIndex = -1, ObjectID = mo.ID, Value = 0, TeamName = string.Empty }.Send();
                        }
                    }

                    if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.delete") + "###Delete"))
                    {
                        PacketDeleteMapObject pdmo = new PacketDeleteMapObject() { DeletedObjects = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects.Select(o => (o.MapID, o.ID)).ToList(), SenderID = Client.Instance.ID, IsServer = false, Session = Client.Instance.SessionID };
                        pdmo.Send();
                    }

                    if (!Client.Instance.IsAdmin)
                    {
                        ImGui.EndDisabled();
                    }
                }

                if (mouseOver != null && (Client.Instance.IsAdmin || mouseOver.IsNameVisible || mouseOver.CanEdit(Client.Instance.ID)))
                {
                    if (ImGui.MenuItem(lang.Translate("ui.popup.object_actions.inspect") + "###Inspect"))
                    {
                        this._inspectedObject = mouseOver;
                        this.FrameState.inspectPopup = true;
                    }
                }

                ImGui.EndPopup();
            }

            bool kOpen = true;
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 400));
            if (ImGui.BeginPopupModal("Inspect Window", ref kOpen, ImGuiWindowFlags.NoDecoration))
            {
                if (this._inspectedObject != null)
                {
                    AssetStatus status = Client.Instance.AssetManager.ClientAssetLibrary.GetOrCreatePortrait(this._inspectedObject.AssetID, out AssetPreview ap);
                    if (status == AssetStatus.Return && ap != null)
                    {
                        System.Numerics.Vector2 winSize = ImGui.GetWindowSize();
                        System.Numerics.Vector2 screenPos = ImGui.GetCursorScreenPos();

                        System.Numerics.Vector4 winBack = *ImGui.GetStyleColorVec4(ImGuiCol.WindowBg);
                        System.Numerics.Vector4 winBorder = *ImGui.GetStyleColorVec4(ImGuiCol.Border);

                        ImDrawListPtr drawList = ImGui.GetForegroundDrawList();
                        screenPos -= new System.Numerics.Vector2((-winSize.X / 2) + 52, 32);
                        drawList.AddQuadFilled(
                            screenPos,
                            screenPos + new System.Numerics.Vector2(96, 0),
                            screenPos + new System.Numerics.Vector2(96, 96),
                            screenPos + new System.Numerics.Vector2(0, 96),
                            Extensions.FromVec4(winBack.GLVector()).Abgr()
                        );

                        drawList.AddQuad(
                           screenPos,
                           screenPos + new System.Numerics.Vector2(96, 0),
                           screenPos + new System.Numerics.Vector2(96, 96),
                           screenPos + new System.Numerics.Vector2(0, 96),
                           Extensions.FromVec4(winBorder.GLVector()).Abgr()
                        );

                        Texture glTex = ap.GetGLTexture();
                        if (glTex != null)
                        {
                            if (ap.IsAnimated)
                            {
                                float tW = glTex.Size.Width;
                                float tH = glTex.Size.Height;
                                AssetPreview.FrameData frame = ap.GetCurrentFrame((int)(Client.Instance.Frontend.UpdatesExisted % (ulong)ap.FramesTotalDelay));
                                float sS = frame.X / tW;
                                float sE = sS + (frame.Width / tW);
                                float tS = frame.Y / tH;
                                float tE = tS + (frame.Height / tH);
                                drawList.AddImage(glTex, screenPos, screenPos + new System.Numerics.Vector2(96, 96), new System.Numerics.Vector2(sS, tS), new System.Numerics.Vector2(sE, tE), this._inspectedObject.TintColor.Abgr());
                            }
                            else
                            {
                                drawList.AddImage(glTex, screenPos, screenPos + new System.Numerics.Vector2(96, 96), System.Numerics.Vector2.Zero, System.Numerics.Vector2.One, this._inspectedObject.TintColor.Abgr());
                            }
                        }

                        System.Numerics.Vector2 tSize = ImGuiHelper.CalcTextSize(this._inspectedObject.Name);
                        ImGui.SetCursorPosX((winSize.X / 2) - (tSize.X / 2));
                        ImGui.SetCursorPosY(72);

                        uint nClr = this._inspectedObject.NameColor.Abgr();
                        if (this._inspectedObject.NameColor.Alpha() < 0.5f)
                        {
                            nClr = ImGui.GetColorU32(ImGuiCol.Text);
                        }

                        ImGui.PushStyleColor(ImGuiCol.Text, nClr);
                        ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(this._inspectedObject.Name));
                        ImGui.PopStyleColor();

                        ImGui.BeginChild("ObjectMouseOverDesc", new System.Numerics.Vector2(284, 260), ImGuiChildFlags.Border, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoSavedSettings);
                        if (this._inspectedObject.UseMarkdownForDescription)
                        {
                            try
                            {
                                ImGuiMarkdown.Markdown(this._inspectedObject.Description, ImGuiMarkdown.MarkdownConfig.Default);
                            }
                            catch (Exception e)
                            {
                                this._inspectedObject.UseMarkdownForDescription = false;
                                Client.Instance.Logger.Log(LogLevel.Error, $"Object markdown corrupted or invalid! Unable to draw markdown for object {this._inspectedObject.ID}({this._inspectedObject.Name})!");
                                Client.Instance.Logger.Log(LogLevel.Error, $"Object was marked as non-markdown but this is a severe error that probably corrupted internal UI state!");
                                Client.Instance.Logger.Log(LogLevel.Error, $"Please report this issue to your server administrator!");
                                Client.Instance.Logger.Exception(LogLevel.Error, e);
                            }
                        }
                        else
                        {
                            ImGui.PushTextWrapPos();
                            ImGui.TextUnformatted(ImGuiHelper.TextOrEmpty(this._inspectedObject.Description));
                            ImGui.PopTextWrapPos();
                        }

                        if (ImGui.BeginPopupContextItem())
                        {
                            if (ImGui.MenuItem(lang.Translate("ui.chat.copy")))
                            {
                                ImGui.SetClipboardText(this._inspectedObject.Description);
                            }

                            ImGui.EndPopup();
                        }

                        ImGui.EndChild();

                        if (ImGui.Button(close + "###Close", new System.Numerics.Vector2(284, 36)))
                        {
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }

                ImGui.EndPopup();
            }

            this.HandlePopupRequests(this.FrameState);
            this.RenderPopups(this.FrameState, lang, ok, cancel, close);
            // New object creation through asset drag'n'drop
            this.HandleAssetPtrDrag(this.FrameState);

            Client.Instance.Frontend.Renderer.PingRenderer.RenderUI();

            this.Timer.Stop();
        }

        private void LoadStatuses()
        {
            int[] textureSize = new int[1];
            OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.MaxTextureSize, textureSize);
            OpenTK.Graphics.OpenGL.PixelInternalFormat f = OpenTK.Graphics.OpenGL.PixelInternalFormat.R8;
            this.StatusAtlas = textureSize[0] >= 4096 ? OpenGLUtil.LoadUIImage("atlas", f) : OpenGLUtil.LoadUIImage("atlas_low", f);
            OpenTK.Graphics.OpenGL.GL.TexParameter(OpenTK.Graphics.OpenGL.TextureTarget.Texture2D, OpenTK.Graphics.OpenGL.TextureParameterName.TextureSwizzleRgba, new int[] {
                (int)OpenTK.Graphics.OpenGL.Version10.Red,
                (int)OpenTK.Graphics.OpenGL.Version10.Red,
                (int)OpenTK.Graphics.OpenGL.Version10.Red,
                (int)OpenTK.Graphics.OpenGL.Version10.One
            });

            JObject o = JObject.Parse(IOVTT.ResourceToString("VTT.Embed.atlas_info.json"));
            JArray ja = (JArray)o["imgs"];
            foreach (JToken jt in ja)
            {
                JObject jo = (JObject)jt;
                string name = jo["name"].ToObject<string>();
                float s = jo["s"].ToObject<float>();
                float t = jo["t"].ToObject<float>();
                this._allStatuses.Add((name, s, t));
            }

            this._allStatuses.Sort((l, r) => l.Item1.CompareTo(r.Item1));
            this._statusStepX = 64f / o["width"].ToObject<float>();
            this._statusStepY = 64f / o["height"].ToObject<float>();
        }

        public void Update() => this.UpdateTurnTrackerParticles();
    }
}
