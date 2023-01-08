namespace VTT.Render.Gui
{
    using OpenTK.Mathematics;
    using OpenTK.Windowing.Common;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.IO;
    using VTT.Asset.Glb;
    using VTT.Asset;
    using VTT.Network;
    using VTT.Network.Packet;
    using ImGuiNET;
    using VTT.Control;
    using VTT.Util;
    using SixLabors.ImageSharp.Processing;
    using System.Collections.Generic;
    using VTT.GL;

    public partial class GuiRenderer
    {
        public void HandleFileDrop(FileDropEventArgs e)
        {
            if (this._mouseOverAssets && Client.Instance.IsAdmin)
            {
                foreach (string s in e.FileNames)
                {
                    string ext = Path.GetExtension(s).ToLower();
                    if (ext.EndsWith("glb")) // Model
                    {
                        GlbScene glbm = null;
                        Image<Rgba32> img = null;
                        try
                        {
                            byte[] binary = File.ReadAllBytes(s);
                            using MemoryStream str = new MemoryStream(binary);
                            glbm = new GlbScene(str);
                            img = glbm.CreatePreview(Client.Instance.Frontend.Renderer.ObjectRenderer.RenderShader, 256, 256, new Vector4(0.39f, 0.39f, 0.39f, 1.0f));
                            using MemoryStream imgMs = new MemoryStream();
                            img.SaveAsPng(imgMs);
                            Asset a = new Asset()
                            {
                                ID = Guid.NewGuid(),
                                Model = new ModelData { GLMdl = glbm },
                                Type = AssetType.Model
                            };

                            AssetMetadata metadata = new AssetMetadata() { Name = Path.GetFileNameWithoutExtension(s), Type = AssetType.Model };
                            AssetRef aRef = new AssetRef() { AssetID = a.ID, AssetPreviewID = a.ID, IsServer = false, Meta = metadata };
                            PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = a.ToBinary(binary), AssetPreview = imgMs.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                            pau.Send(Client.Instance.NetClient);
                            glbm.Dispose();
                            img.Dispose();
                        }
                        catch (Exception ex)
                        {
                            // Issue parsing model/rendering preview
                            Client.Instance.Logger.Log(LogLevel.Error, "Could not parse glb - " + ex.Message);
                            Client.Instance.Logger.Exception(LogLevel.Error, ex);
                            try
                            {
                                glbm?.Dispose();
                                img?.Dispose();
                            }
                            catch
                            {
                                // NOOP
                            }
                        }

                        continue;
                    }

                    if (ext.EndsWith("png") || ext.EndsWith("jpg") || ext.EndsWith("jpeg")) // Image
                    {
                        Image<Rgba32> img = null;
                        Image<Rgba32> preview = null;
                        try
                        {
                            img = Image.Load<Rgba32>(s);
                            preview = img.Clone();
                            preview.Mutate(x => x.Resize(256, 256));
                            using MemoryStream ms = new MemoryStream();
                            preview.SaveAsPng(ms);
                            preview.Dispose();
                            Asset a = new Asset()
                            {
                                ID = Guid.NewGuid(),
                                Texture = TextureData.CreateDefaultFromImage(img, out byte[] tdataBinary, out TextureData.Metadata meta),
                                Type = AssetType.Texture
                            };

                            AssetMetadata metadata = new AssetMetadata() { Name = Path.GetFileNameWithoutExtension(s), Type = AssetType.Texture, TextureInfo = meta };
                            AssetRef aRef = new AssetRef() { AssetID = a.ID, AssetPreviewID = a.ID, IsServer = false, Meta = metadata };
                            PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = a.ToBinary(tdataBinary), AssetPreview = ms.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                            pau.Send(Client.Instance.NetClient);
                            img.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Client.Instance.Logger.Log(LogLevel.Error, "Could not parse image - " + ex.Message);
                            Client.Instance.Logger.Exception(LogLevel.Error, ex);
                            try
                            {
                                img?.Dispose();
                                preview?.Dispose();
                            }
                            catch
                            {
                                // NOOP
                            }
                        }

                        continue;
                    }

                    if (ext.EndsWith("webm"))
                    {
                        if (Client.Instance.Frontend.FFmpegWrapper.IsInitialized)
                        {
                            try
                            {
                                int i = 0;
                                byte[] previewBinary = Array.Empty<byte>();
                                List<TextureData.Frame> frames = new List<TextureData.Frame>();
                                foreach (Image<Rgba32> img in Client.Instance.Frontend.FFmpegWrapper.DecodeAllFrames(s))
                                {
                                    if (i == 0)
                                    {
                                        Image<Rgba32> preview = img.Clone();
                                        preview.Mutate(x => x.Resize(256, 256));
                                        using MemoryStream ms1 = new MemoryStream();
                                        preview.SaveAsPng(ms1);
                                        previewBinary = ms1.ToArray();
                                        preview.Dispose();
                                    }

                                    MemoryStream ms = new MemoryStream();
                                    img.SaveAsPng(ms);
                                    byte[] imgBin = ms.ToArray();
                                    ms.Dispose();
                                    TextureData.Frame f = new TextureData.Frame(i, 1, false, imgBin);
                                    frames.Add(f);
                                    img.Dispose();
                                    ++i;
                                }

                                TextureData ret = new TextureData()
                                {
                                    Meta = new TextureData.Metadata()
                                    {
                                        WrapS = WrapParam.Repeat,
                                        WrapT = WrapParam.Repeat,
                                        FilterMag = FilterParam.Linear,
                                        FilterMin = FilterParam.LinearMipmapLinear,
                                        EnableBlending = true,
                                        Compress = true,
                                        GammaCorrect = true,
                                    },

                                    Frames = frames.ToArray()
                                };

                                Asset a = new Asset()
                                {
                                    ID = Guid.NewGuid(),
                                    Texture = ret,
                                    Type = AssetType.Texture
                                };

                                AssetMetadata metadata = new AssetMetadata() { Name = Path.GetFileNameWithoutExtension(s), Type = AssetType.Texture, TextureInfo = ret.Meta };
                                AssetRef aRef = new AssetRef() { AssetID = a.ID, AssetPreviewID = a.ID, IsServer = false, Meta = metadata };
                                PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = a.ToBinary(ret.Write()), AssetPreview = previewBinary, IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                                pau.Send(Client.Instance.NetClient);
                            }
                            catch (Exception ex)
                            {
                                Client.Instance.Logger.Log(LogLevel.Error, "Could not parse webm - " + ex.Message);
                                Client.Instance.Logger.Exception(LogLevel.Error, ex);
                                // Nothing to dispose of, high potential memory leak
                            }

                            continue;
                        }
                        else
                        {
                            Client.Instance.Logger.Log(LogLevel.Warn, "Couldn't upload webm due to missing ffmpeg");
                        }
                    }
                }
            }
        }

        private unsafe void RenderAssets(SimpleLanguage lang, GuiState state)
        {
            if (Client.Instance.IsAdmin)
            {
                if (ImGui.Begin(lang.Translate("ui.assets") + "###Assets"))
                {
                    this._mouseOverAssets = ImGui.IsWindowHovered();

                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                    if (ImGui.ImageButton("btn_asset_back", this.BackIcon, Vec12x12) && this.CurrentFolder.Parent != null)
                    {
                        state.moveTo = this.CurrentFolder.Parent;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        state.mouseOverMoveUp = true;
                        ImGui.SetTooltip(lang.Translate("ui.assets.back"));
                    }

                    ImGui.PopStyleColor();
                    ImGui.SameLine();

                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                    if (ImGui.ImageButton("btn_asset_add_folder", this.AddIcon, Vec12x12))
                    {
                        state.openNewFolderPopup = true;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.assets.add"));
                    }

                    ImGui.SameLine();
                    if (ImGui.ImageButton("btn_asset_add_particle", this.AssetParticleIcon, Vec12x12))
                    {
                        ParticleSystem ps = new ParticleSystem();
                        AssetMetadata metadata = new AssetMetadata() { Name = "New Particle System", Type = AssetType.ParticleSystem, Version = 2 };
                        using MemoryStream ms = new MemoryStream();
                        using BinaryWriter bw = new BinaryWriter(ms);
                        ps.WriteV2(bw);
                        using Image<Rgba32> img = new Image<Rgba32>(256, 256, new Rgba32(0.39f, 0.39f, 0.39f, 1.0f));
                        using MemoryStream imgMs = new MemoryStream();
                        img.SaveAsPng(imgMs);
                        PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = new Asset().ToBinary(ms.ToArray()), AssetPreview = imgMs.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                        pau.Send(Client.Instance.NetClient);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.assets.add_particle"));
                    }

                    ImGui.PopStyleColor();
                    ImGui.SameLine();

                    ImGui.TextDisabled(this.CurrentFolder.GetPath());
                    ImGui.Separator();

                    float ww = ImGui.GetWindowWidth();
                    float wpx = ImGui.GetWindowPos().X;
                    float xmax = wpx + ww;

                    float spx = ImGui.GetCursorPosX();

                    float px = ImGui.GetCursorPosX();
                    float py = ImGui.GetCursorPosY();
                    for (int i = 0; i < this.CurrentFolder.Directories.Count; i++)
                    {
                        AssetDirectory d = this.CurrentFolder.Directories[i];
                        ImGui.SetCursorPosX(px);
                        ImGui.SetCursorPosY(py);
                        ImGui.BeginChild(d.Name, new System.Numerics.Vector2(64 + (ImGui.GetStyle().CellPadding.X * 2), 64 + (ImGui.GetStyle().CellPadding.Y * 2)), false, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
                        bool hover = ImGui.IsWindowHovered();
                        bool clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && hover)
                        {
                            state.moveTo = d;
                        }

                        ImGui.SetCursorPosX(8);
                        ImGui.SetCursorPosY(8);
                        ImGui.Image(this.FolderIcon, new System.Numerics.Vector2(48, 48), new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(1, 1), new System.Numerics.Vector4(1, 1, 1, 1), hover ? (System.Numerics.Vector4)Color.RoyalBlue : clicked ? (System.Numerics.Vector4)Color.Blue : new System.Numerics.Vector4(0, 0, 0, 0));

                        string txt = d.Name;
                        System.Numerics.Vector2 label_size;
                        label_size = FitLabelByBinarySearch(d.Name, ref txt);

                        ImGui.SetCursorPosX(32 - (label_size.X / 2));
                        ImGui.SetCursorPosY(64 - label_size.Y);
                        ImGui.TextUnformatted(txt);

                        if (hover)
                        {
                            txt = d.Name;
                            label_size = ImGui.CalcTextSize(txt);
                            ImGui.SetNextWindowSize(label_size + new System.Numerics.Vector2(16, 16));
                            ImGui.Begin("Name Highlight", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.Tooltip);
                            ImGui.TextUnformatted(txt);
                            ImGui.End();
                            state.dirHovered = d;
                        }

                        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsItemHovered())
                        {
                            if (ImGui.BeginPopupContextWindow(d.Name))
                            {
                                if (ImGui.MenuItem(lang.Translate("ui.assets.rename") + "###Rename"))
                                {
                                    state.editFolderPopup = true;
                                    this._contextDir = d;
                                    this._newFolderNameString = d.Name;
                                }

                                if (ImGui.MenuItem(lang.Translate("ui.assets.delete") + "###Delete"))
                                {
                                    state.deleteFolderPopup = true;
                                    this._contextDir = d;
                                }

                                ImGui.EndPopup();
                            }
                        }

                        ImGui.EndChild();

                        px += 64 + ImGui.GetStyle().CellPadding.X;
                        if (px >= xmax - 64 + ImGui.GetStyle().CellPadding.X)
                        {
                            px = spx;
                            py += 64 + ImGui.GetStyle().CellPadding.Y;
                        }
                    }

                    for (int i = 0; i < this.CurrentFolder.Refs.Count; ++i)
                    {
                        AssetRef aRef = this.CurrentFolder.Refs[i];
                        ImGui.SetCursorPosX(px);
                        ImGui.SetCursorPosY(py);
                        ImGui.BeginChild(aRef.AssetID.ToString(), new System.Numerics.Vector2(64 + (ImGui.GetStyle().CellPadding.X * 2), 64 + (ImGui.GetStyle().CellPadding.Y * 2)), false, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar); ;
                        AssetStatus a = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestPreview(aRef.AssetID, out AssetPreview ap);

                        bool hover = ImGui.IsWindowHovered();
                        bool clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

                        ImGui.SetCursorPosX(8);
                        ImGui.SetCursorPosY(8);
                        if (a == AssetStatus.Await)
                        {
                            int frame =
                                (int)((int)Client.Instance.Frontend.UpdatesExisted % 90 / 90.0f * this.LoadingSpinnerFrames);
                            float texelIndexStart = (float)frame / this.LoadingSpinnerFrames;
                            float texelSize = 1f / this.LoadingSpinnerFrames;
                            ImGui.Image(a == AssetStatus.Await ? this.LoadingSpinner : a == AssetStatus.Error ? this.ErrorIcon : ap.GetGLTexture(), new System.Numerics.Vector2(48, 48), new System.Numerics.Vector2(texelIndexStart, 0), new System.Numerics.Vector2(texelIndexStart + texelSize, 1), new System.Numerics.Vector4(1, 1, 1, 1), hover ? (System.Numerics.Vector4)Color.RoyalBlue : clicked ? (System.Numerics.Vector4)Color.Blue : new System.Numerics.Vector4(0, 0, 0, 0));
                        }
                        else
                        {
                            ImGui.Image(a == AssetStatus.Error ? this.ErrorIcon : ap.GetGLTexture(), new System.Numerics.Vector2(48, 48), new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(1, 1), new System.Numerics.Vector4(1, 1, 1, 1), hover ? (System.Numerics.Vector4)Color.RoyalBlue : clicked ? (System.Numerics.Vector4)Color.Blue : new System.Numerics.Vector4(0, 0, 0, 0));
                        }

                        string txt = aRef.Name;
                        System.Numerics.Vector2 label_size;
                        label_size = FitLabelByBinarySearch(aRef.Name, ref txt);

                        if (a == AssetStatus.Return && ap != null)
                        {
                            ImGui.SetCursorPosX(44);
                            ImGui.SetCursorPosY(44);
                            ImGui.Image(aRef.Type == AssetType.Model ? this.AssetModelIcon : aRef.Type == AssetType.ParticleSystem ? this.AssetParticleIcon : this.AssetImageIcon, new System.Numerics.Vector2(16, 16));
                        }

                        ImGui.SetCursorPosX(32 - (label_size.X / 2));
                        ImGui.SetCursorPosY(64 - label_size.Y);
                        ImGui.TextUnformatted(txt);

                        if (hover)
                        {
                            txt = aRef.Name;
                            label_size = ImGui.CalcTextSize(txt);
                            ImGui.SetNextWindowSize(label_size + new System.Numerics.Vector2(16, 16));
                            ImGui.Begin("Name Highlight", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.Tooltip);
                            ImGui.TextUnformatted(txt);
                            ImGui.End();
                            if (!this._lmbDown && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                            {
                                this._lmbDown = true;
                                this._draggedRef = aRef;
                            }
                        }

                        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsItemHovered())
                        {
                            if (ImGui.BeginPopupContextWindow(aRef.AssetID.ToString()))
                            {
                                if (ImGui.MenuItem(lang.Translate("ui.assets.rename") + "###Rename"))
                                {
                                    state.editAssetPopup = true;
                                    this._editedRef = aRef;
                                    this._newFolderNameString = aRef.Name;
                                }

                                if (aRef.Meta != null && aRef.Meta.Type == AssetType.Texture)
                                {
                                    if (ImGui.MenuItem(lang.Translate("ui.assets.edit.tex") + "###Edit Texture"))
                                    {
                                        state.editTexturePopup = true;
                                        this._editedRef = aRef;
                                        this._editedTextureMetadataCopy = aRef.Meta.TextureInfo.Copy();
                                    }
                                }

                                if (aRef.Meta != null && aRef.Meta.Type == AssetType.ParticleSystem)
                                {
                                    if (ImGui.MenuItem(lang.Translate("ui.assets.edit_particle") + "###Edit Particle System"))
                                    {
                                        state.editParticleSystemPopup = true;
                                        this._editedParticleSystemId = aRef.AssetID;
                                        Client.Instance.Frontend.Renderer.ParticleRenderer.CurrentlyEditedSystem = null;
                                        Client.Instance.Frontend.Renderer.ParticleRenderer.CurrentlyEditedSystemInstance = null;
                                    }
                                }

                                if (ImGui.MenuItem(lang.Translate("ui.assets.delete") + "###Delete"))
                                {
                                    state.deleteAssetPopup = true;
                                    this._editedRef = aRef;
                                }

                                ImGui.EndPopup();
                            }
                        }

                        ImGui.EndChild();

                        px += 64 + ImGui.GetStyle().CellPadding.X;
                        if (px >= xmax - 64 + ImGui.GetStyle().CellPadding.X)
                        {
                            px = spx;
                            py += 64 + ImGui.GetStyle().CellPadding.Y;
                        }
                    }
                }

                ImGui.End();
            }
        }

        private unsafe void HandleAssetPtrDrag(GuiState state)
        {
            if (state.clientMap == null)
            {
                return;
            }

            if (this._lmbDown && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (!ImGui.GetIO().WantCaptureMouse)
                {
                    if (this._draggedRef.Type is AssetType.Model or AssetType.Texture)
                    {
                        Vector3? worldVec = Client.Instance.Frontend.Renderer.RulerRenderer.TerrainHit ?? Client.Instance.Frontend.Renderer.MapRenderer.CursorWorld;
                        if (!worldVec.HasValue)
                        {
                            Ray ray = Client.Instance.Frontend.Renderer.MapRenderer.RayFromCursor();
                            worldVec = ray.Origin + (ray.Direction * 6.0f);
                        }

                        if (ImGui.IsKeyDown(ImGuiKey.LeftAlt))
                        {
                            worldVec = MapRenderer.SnapToGrid(worldVec.Value, state.clientMap.GridSize);
                        }

                        MapObject mo = new MapObject()
                        {
                            ID = Guid.NewGuid(),
                            AssetID = this._draggedRef.AssetID,
                            Container = state.clientMap,
                            MapID = state.clientMap.ID,
                            MapLayer = Client.Instance.Frontend.Renderer.MapRenderer.CurrentLayer,
                            Name = "New " + this._draggedRef.Name.CapitalizeWords(),
                            OwnerID = Client.Instance.ID,
                            Position = worldVec.Value
                        };

                        if (this._draggedRef.Type == AssetType.Texture)
                        {
                            if (state.clientMap.Is2D)
                            {
                                mo.Position += new Vector3(0, 0, 0.01f);
                            }
                            else
                            {
                                Camera cam = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera;
                                Vector3 a = Vector3.Cross(Vector3.UnitZ, -cam.Direction);
                                Quaternion q = new Quaternion(a, 1 + Vector3.Dot(Vector3.UnitZ, -cam.Direction)).Normalized();
                                mo.Rotation = q;
                            }
                        }

                        PacketMapObject pmo = new PacketMapObject() { IsServer = false, Obj = mo, Session = Client.Instance.SessionID };
                        pmo.Send();
                    }
                }
                else
                {
                    bool haveResult = false;
                    if (this._mouseOverAssets || state.dirHovered != null)
                    {
                        AssetDirectory assetDestination = state.mouseOverMoveUp ? this.CurrentFolder.Parent : state.dirHovered;
                        if (assetDestination != null)
                        {
                            PacketAssetMove pam = new PacketAssetMove() { MovedFrom = this.CurrentFolder.GetPath(), MovedTo = assetDestination.GetPath(), MovedRefID = this._draggedRef.AssetID };
                            pam.Send();
                            haveResult = true;
                        }
                    }

                    if (!haveResult && state.objectModelHovered != null && Client.Instance.IsAdmin)
                    {
                        new PacketChangeObjectAsset() { AssetID = this._draggedRef.AssetID, MapID = state.objectModelHovered.MapID, ObjectID = state.objectModelHovered.ID }.Send();
                        haveResult = true;
                    }

                    if (!haveResult && state.objectCustomNameplateHovered != null && Client.Instance.IsAdmin && this._draggedRef != null && this._draggedRef.Type == AssetType.Texture)
                    {
                        state.objectCustomNameplateHovered.CustomNameplateID = this._draggedRef.AssetID;
                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.CustomNameplateID, Data = new List<(Guid, Guid, object)>() { (state.objectCustomNameplateHovered.MapID, state.objectCustomNameplateHovered.ID, this._draggedRef.AssetID) } }.Send(); 
                        haveResult = true;
                    }

                    if (!haveResult && state.particleModelHovered != null && Client.Instance.IsAdmin)
                    {
                        state.particleModelHovered.AssetID = this._draggedRef.AssetID;
                        haveResult = true;
                    }

                    if (!haveResult && state.particleContainerHovered != null && Client.Instance.IsAdmin)
                    {
                        state.particleContainerHovered.SystemID = this._draggedRef.AssetID;
                        new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Edit, Container = state.particleContainerHovered.Serialize(), MapID = state.particleContainerHovered.Container.MapID, ObjectID = state.particleContainerHovered.Container.ID, ParticleID = state.particleContainerHovered.ID }.Send();
                    }
                }

                this._lmbDown = false;
                this._draggedRef = null;
            }
        }

        private unsafe void RenderDraggedAssetRef()
        {
            if (this._draggedRef != null)
            {
                ImGuiWindowFlags flags = ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize;
                ImGui.SetNextWindowBgAlpha(0.5f);
                ImGui.SetNextWindowPos(new(Client.Instance.Frontend.MouseX, Client.Instance.Frontend.MouseY));
                ImGui.Begin("Dragged Preview", flags);

                AssetStatus a = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestPreview(this._draggedRef.AssetPreviewID, out AssetPreview ap);
                if (a == AssetStatus.Await)
                {
                    int frame =
                        (int)((int)Client.Instance.Frontend.UpdatesExisted % 90 / 90.0f * this.LoadingSpinnerFrames);
                    float texelIndexStart = (float)frame / this.LoadingSpinnerFrames;
                    float texelSize = 1f / this.LoadingSpinnerFrames;
                    ImGui.Image(a == AssetStatus.Await ? this.LoadingSpinner : a == AssetStatus.Error ? this.ErrorIcon : ap.GetGLTexture(), new System.Numerics.Vector2(48, 48), new System.Numerics.Vector2(texelIndexStart, 0), new System.Numerics.Vector2(texelIndexStart + texelSize, 1), new System.Numerics.Vector4(1, 1, 1, 1));
                }
                else
                {
                    ImGui.Image(a == AssetStatus.Error ? this.ErrorIcon : ap.GetGLTexture(), new System.Numerics.Vector2(48, 48), new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(1, 1), new System.Numerics.Vector4(1, 1, 1, 1));
                }

                ImGui.End();
            }
        }

        private static System.Numerics.Vector2 FitLabelByBinarySearch(string original, ref string txt)
        {
            System.Numerics.Vector2 label_size = default;
            txt = original[..1];
            int c = 1;
            while (c < original.Length && label_size.X < 64)
            {
                txt += original[c++];
                label_size = ImGui.CalcTextSize(txt);
            }

            if (c < original.Length)
            {
                txt = txt[..^3];
                txt += "...";
            }

            return label_size;
        }
    }
}
