namespace VTT.Render.Gui
{
    using ImGuiNET;
    using NLayer;
    using NVorbis;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using VTT.Asset;
    using VTT.Asset.Glb;
    using VTT.Asset.Shader.NodeGraph;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Network.Packet;
    using VTT.Sound;
    using VTT.Util;

    public partial class GuiRenderer
    {
        public void HandleFileDrop(string[] e)
        {
            foreach (string s in e)
            {
                this.FrameState.dropEventsReceiver.Enqueue(s);
            }
        }

        public bool ProcessFileDrop(string s, int index)
        {
            if (this._mouseOverAssets && Client.Instance.IsAdmin)
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
                        glbm = new GlbScene(new ModelData.Metadata() { CompressNormal = false, CompressAlbedo = false, CompressAOMR = false, CompressEmissive = false }, str);
                        img = glbm.CreatePreview(256, 256, new Vector4(0.39f, 0.39f, 0.39f, 1.0f));
                        using MemoryStream imgMs = new MemoryStream();
                        img.SaveAsPng(imgMs);
                        Asset a = new Asset()
                        {
                            ID = Guid.NewGuid(),
                            Model = new ModelData { GLMdl = glbm },
                            Type = AssetType.Model
                        };

                        AssetMetadata metadata = new AssetMetadata() { Name = Path.GetFileNameWithoutExtension(s), Type = AssetType.Model, ModelInfo = new ModelData.Metadata() { CompressAlbedo = false, CompressAOMR = false, CompressEmissive = false, CompressNormal = false } };
                        AssetRef aRef = new AssetRef() { AssetID = a.ID, AssetPreviewID = a.ID, IsServer = false, Meta = metadata };
                        PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = a.ToBinary(binary), AssetPreview = imgMs.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                        pau.Send(Client.Instance.NetClient);
                        glbm.Dispose();
                        img.Dispose();
                        return true;
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

                        return false;
                    }
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
                        return true;
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

                        return false;
                    }
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
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Client.Instance.Logger.Log(LogLevel.Error, "Could not parse webm - " + ex.Message);
                            Client.Instance.Logger.Exception(LogLevel.Error, ex);
                            // Nothing to dispose of, high potential memory leak
                            return false;
                        }
                    }
                    else
                    {
                        Client.Instance.Logger.Log(LogLevel.Warn, "Couldn't upload webm due to missing ffmpeg");
                    }
                }

                if (ext.EndsWith("wav") || ext.EndsWith("mp3") || ext.EndsWith("ogg")) // Sound
                {
                    try
                    {
                        long len = new FileInfo(s).Length;
                        if (len >= int.MaxValue / 2) // 1 GB
                        {
                            Client.Instance.Logger.Log(LogLevel.Warn, "Audio file too large - maximum allowed is 1Gb!");
                        }
                        else
                        {
                            WaveAudio wa = null;
                            Image<Rgba32> img = null;
                            SoundData.Metadata meta = new SoundData.Metadata();
                            if (ext.EndsWith("wav")) // Wave Sound
                            {
                                wa = new WaveAudio();
                                wa.Load(File.OpenRead(s));
                            }

                            if (ext.EndsWith("mp3"))
                            {
                                MpegFile mpeg = new MpegFile(File.OpenRead(s));
                                wa = new WaveAudio(mpeg);
                                mpeg.Dispose();
                            }

                            if (ext.EndsWith("ogg"))
                            {
                                VorbisReader vorbis = new VorbisReader(File.OpenRead(s));
                                wa = new WaveAudio(vorbis);
                                vorbis.Dispose();
                            }

                            img = wa.GenWaveForm(1024, 1024);
                            byte[] dataArray = null;
                            bool doCompress = (Client.Instance.Frontend.FFmpegWrapper.IsInitialized &&
                                Client.Instance.Settings.SoundCompressionPolicy == ClientSettings.AudioCompressionPolicy.Always) ||
                                (Client.Instance.Settings.SoundCompressionPolicy == ClientSettings.AudioCompressionPolicy.LargeFilesOnly && wa.DataLength > 4194304); // 4Mb

                            if (doCompress && wa.TryGetMpegEncodedData(out dataArray, out long[] packetOffsets) && dataArray != null)
                            {
                                meta.SoundType = SoundData.Metadata.StorageType.Mpeg;
                                meta.IsFullData = false;
                                meta.TotalChunks = packetOffsets.Length;
                                meta.CompressedChunkOffsets = packetOffsets;
                            }
                            else
                            {
                                meta.SoundType = SoundData.Metadata.StorageType.Raw;
                                meta.IsFullData = wa.DataLength <= 4194304; // 4mb are allowed as raw
                                meta.TotalChunks = (int)Math.Ceiling((double)wa.DataLength / (wa.SampleRate * wa.NumChannels * 5)); // 5s audio buffers
                                meta.CompressedChunkOffsets = Array.Empty<long>();
                            }

                            meta.SampleRate = wa.SampleRate;
                            meta.NumChannels = wa.NumChannels;
                            meta.TotalDuration = wa.Duration;
                            meta.SoundAssetName = Path.GetFileNameWithoutExtension(s);
                            SoundData sound = new SoundData();
                            sound.Meta = meta;

                            Asset a = new Asset()
                            {
                                ID = Guid.NewGuid(),
                                Sound = sound,
                                Type = AssetType.Sound
                            };

                            using MemoryStream ms = new MemoryStream();
                            img.SaveAsPng(ms);

                            AssetMetadata metadata = new AssetMetadata() { Name = Path.GetFileNameWithoutExtension(s), Type = AssetType.Sound, SoundInfo = meta };
                            AssetRef aRef = new AssetRef() { AssetID = a.ID, AssetPreviewID = a.ID, IsServer = false, Meta = metadata };
                            PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = meta.SoundType == SoundData.Metadata.StorageType.Raw ? wa.GetManagedDataCopy() : dataArray, AssetPreview = ms.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                            pau.Send(Client.Instance.NetClient);

                            wa.Free();
                            img.Dispose();
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        Client.Instance.Logger.Log(LogLevel.Error, "Could not parse sound - " + ex.Message);
                        Client.Instance.Logger.Exception(LogLevel.Error, ex);
                        return false;
                    }
                }
            }

            return false;
        }

        private string _searchText = "";
        private int _sortOption = 0;
        private unsafe void RenderAssets(SimpleLanguage lang, GuiState state)
        {
            this._mouseOverAssets = false;
            if (Client.Instance.IsAdmin)
            {
                if (ImGui.Begin(lang.Translate("ui.assets") + "###Assets"))
                {
                    // Old asset rendering code
                    /*
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

                    ImGui.SameLine();
                    if (ImGui.ImageButton("BtnAssetAddShader", this.AssetShaderIcon, Vec12x12))
                    {
                        ShaderGraph sn = new ShaderGraph();
                        sn.FillDefaultLayout();
                        AssetMetadata metadata = new AssetMetadata() { Name = "New Shader", Type = AssetType.Shader, Version = 1 };
                        using MemoryStream ms = new MemoryStream();
                        using BinaryWriter bw = new BinaryWriter(ms);
                        sn.Serialize().Write(bw);
                        using Image<Rgba32> img = new Image<Rgba32>(256, 256, new Rgba32(0, 0, 0, 1.0f));
                        using MemoryStream imgMs = new MemoryStream();
                        img.SaveAsPng(imgMs);
                        PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = new Asset().ToBinary(ms.ToArray()), AssetPreview = imgMs.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                        pau.Send(Client.Instance.NetClient);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(lang.Translate("ui.assets.add_shader"));
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
                            label_size = ImGuiHelper.CalcTextSize(txt);
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
                            Texture aRefIcon = aRef.Type switch
                            {
                                AssetType.Model => this.AssetModelIcon,
                                AssetType.ParticleSystem => this.AssetParticleIcon,
                                AssetType.Shader => this.AssetShaderIcon,
                                _ => this.AssetImageIcon
                            };

                            ImGui.Image(aRefIcon, new System.Numerics.Vector2(16, 16));
                        }

                        ImGui.SetCursorPosX(32 - (label_size.X / 2));
                        ImGui.SetCursorPosY(64 - label_size.Y);
                        ImGui.TextUnformatted(txt);

                        if (hover)
                        {
                            txt = aRef.Name;
                            label_size = ImGuiHelper.CalcTextSize(txt);
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

                                    if (ImGui.MenuItem(lang.Translate("ui.assets.duplicate_particle") + "###Duplicate Particle System"))
                                    {
                                        Client.Instance.AssetManager.ClientAssetLibrary.PerformClientAssetAction(aRef.AssetID, AssetType.ParticleSystem, (status, a) =>
                                        {
                                            if (status == AssetStatus.Return && a != null && a.Type == AssetType.ParticleSystem)
                                            {
                                                ParticleSystem ps = a.ParticleSystem.Copy();
                                                AssetMetadata metadata = new AssetMetadata() { Name = aRef.Name + " (copy)", Type = AssetType.ParticleSystem, Version = aRef.Meta.Version };
                                                using MemoryStream ms = new MemoryStream();
                                                using BinaryWriter bw = new BinaryWriter(ms);
                                                ps.WriteV2(bw);
                                                using Image<Rgba32> img = new Image<Rgba32>(256, 256, new Rgba32(0.39f, 0.39f, 0.39f, 1.0f));
                                                using MemoryStream imgMs = new MemoryStream();
                                                img.SaveAsPng(imgMs);
                                                PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = new Asset().ToBinary(ms.ToArray()), AssetPreview = imgMs.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                                                pau.Send(Client.Instance.NetClient);
                                            }
                                        });
                                    }
                                }

                                if (aRef.Meta != null && aRef.Meta.Type == AssetType.Shader)
                                {
                                    if (ImGui.MenuItem(lang.Translate("ui.assets.edit_shader") + "###Edit Shader"))
                                    {
                                        state.editShaderPopup = true;
                                        this._editedShaderId = aRef.AssetID;
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

                this._mouseOverAssets = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem | ImGuiHoveredFlags.RootAndChildWindows);
                */

                    System.Numerics.Vector2 winSize = ImGui.GetWindowSize();

                    ImGui.BeginChild(ImGui.GetID("AssetsNavbar"), new System.Numerics.Vector2(winSize.X - 500, 24), ImGuiChildFlags.FrameStyle, ImGuiWindowFlags.MenuBar);
                    if (ImGui.BeginMenuBar())
                    {
                        if (ImGui.BeginMenu(lang.Translate("ui.menu.new")))
                        {
                            if (ImGui.MenuItem(lang.Translate("ui.asset.new.folder")))
                            {
                                state.openNewFolderPopup = true;
                            }

                            if (ImGui.MenuItem(lang.Translate("ui.asset.new.particle")))
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

                            if (ImGui.BeginMenu(lang.Translate("ui.asset.new.shader")))
                            {
                                if (ImGui.MenuItem(lang.Translate("ui.asset.new.shader.object")))
                                {
                                    ShaderGraph sn = new ShaderGraph();
                                    sn.FillDefaultObjectLayout();
                                    AssetMetadata metadata = new AssetMetadata() { Name = "New Object Shader", Type = AssetType.Shader, Version = 1 };
                                    using MemoryStream ms = new MemoryStream();
                                    using BinaryWriter bw = new BinaryWriter(ms);
                                    sn.Serialize().Write(bw);
                                    using Image<Rgba32> img = new Image<Rgba32>(256, 256, new Rgba32(0, 0, 0, 1.0f));
                                    using MemoryStream imgMs = new MemoryStream();
                                    img.SaveAsPng(imgMs);
                                    PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = new Asset().ToBinary(ms.ToArray()), AssetPreview = imgMs.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                                    pau.Send(Client.Instance.NetClient);
                                }

                                if (ImGui.MenuItem(lang.Translate("ui.asset.new.shader.particle")))
                                {
                                    ShaderGraph sn = new ShaderGraph();
                                    sn.FillDefaultParticleLayout();
                                    AssetMetadata metadata = new AssetMetadata() { Name = "New Particle Shader", Type = AssetType.Shader, Version = 1 };
                                    using MemoryStream ms = new MemoryStream();
                                    using BinaryWriter bw = new BinaryWriter(ms);
                                    sn.Serialize().Write(bw);
                                    using Image<Rgba32> img = new Image<Rgba32>(256, 256, new Rgba32(0, 0, 0, 1.0f));
                                    using MemoryStream imgMs = new MemoryStream();
                                    img.SaveAsPng(imgMs);
                                    PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = new Asset().ToBinary(ms.ToArray()), AssetPreview = imgMs.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                                    pau.Send(Client.Instance.NetClient);
                                }

                                ImGui.EndMenu();
                            }

                            ImGui.EndMenu();
                        }

                        ImGui.MenuItem(this.CurrentFolder.GetPath(), false);
                        ImGui.EndMenuBar();
                    }

                    ImGui.EndChild();
                    ImGui.SameLine();
                    string[] sortOptions = { lang.Translate("ui.asset.sort.name"), lang.Translate("ui.asset.sort.type"), lang.Translate("ui.asset.sort.upload_date") };
                    ImGui.SetNextItemWidth(140);
                    ImGui.Combo(lang.Translate("ui.asset.sort_by") + "###AssetSortBar", ref this._sortOption, sortOptions, sortOptions.Length);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(300 - 64);
                    ImGui.InputText("##AssetsSearchBar", ref this._searchText, ushort.MaxValue, ImGuiInputTextFlags.EscapeClearsAll);
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.SetKeyboardFocusHere();
                        this._searchText = "";

                    }
                    ImGui.SameLine();
                    ImGui.Image(this.Search, new Vector2(24, 24));

                    var winPadding = ImGui.GetStyle().WindowPadding;
                    var framePadding = ImGui.GetStyle().FramePadding;

                    ImGui.Columns(2);
                    ImGui.SetColumnWidth(0, 240);
                    float cw = ImGui.GetColumnWidth();
                    ImGui.BeginChild(ImGui.GetID("AssetsFS"), new System.Numerics.Vector2(cw, winSize.Y - 24 - (winPadding.Y * 2) - (framePadding.Y * 2) - 20), ImGuiChildFlags.FrameStyle, ImGuiWindowFlags.HorizontalScrollbar);
                    void RecursivelyDrawDirectories(AssetDirectory dir)
                    {
                        bool isCurrent = dir.Equals(this.CurrentFolder);
                        bool haveMoreDirs = dir.Directories.Count > 0;
                        ImGuiTreeNodeFlags tnf = ImGuiTreeNodeFlags.None;
                        if (!haveMoreDirs)
                        {
                            tnf |= ImGuiTreeNodeFlags.Leaf;
                        }

                        if (isCurrent)
                        {
                            System.Numerics.Vector4 clr = *ImGui.GetStyleColorVec4(ImGuiCol.HeaderHovered);
                            ImGui.PushStyleColor(ImGuiCol.Text, clr);
                            ImGui.SetNextItemOpen(true);
                        }

                        if (ImGui.TreeNodeEx(dir.Name, tnf))
                        {
                            if (isCurrent)
                            {
                                ImGui.PopStyleColor();
                            }

                            if ((ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) || (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()))
                            {
                                this.CurrentFolder = dir;
                            }

                            if (haveMoreDirs)
                            {
                                foreach (AssetDirectory ad in dir.Directories)
                                {
                                    RecursivelyDrawDirectories(ad);
                                }
                            }

                            ImGui.TreePop();
                        }
                        else
                        {
                            if ((ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) || (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()))
                            {
                                this.CurrentFolder = dir;
                            }

                            if (isCurrent)
                            {
                                ImGui.PopStyleColor();
                            }
                        }
                    }

                    RecursivelyDrawDirectories(Client.Instance.AssetManager.Root);

                    ImGui.EndChild();

                    ImGui.NextColumn();
                    cw = ImGui.GetColumnWidth();
                    ImGui.BeginChild(ImGui.GetID("AssetsView"), new System.Numerics.Vector2(cw, winSize.Y - 24 - (winPadding.Y * 2) - (framePadding.Y * 2) - 20));
                    this._mouseOverAssets = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenOverlappedByWindow | ImGuiHoveredFlags.AllowWhenOverlappedByItem | ImGuiHoveredFlags.ChildWindows);
                    IOrderedEnumerable<AssetRef> assetEnumeration;
                    switch (this._sortOption)
                    {
                        case 1:
                        {
                            assetEnumeration = this.CurrentFolder.Refs.OrderBy(x => x.Type).ThenBy(x => x.Name);
                            break;
                        }

                        case 2:
                        {
                            assetEnumeration = this.CurrentFolder.Refs.OrderBy(x => x.UploadTime).ThenBy(x => x.Name);
                            break;
                        }

                        default:
                        {
                            assetEnumeration = this.CurrentFolder.Refs.OrderBy(x => x.Name);
                            break;
                        }
                    }

                    foreach (AssetRef aRef in assetEnumeration)
                    {
                        if (!string.IsNullOrEmpty(this._searchText))
                        {
                            if (!aRef.Name.Contains(this._searchText, StringComparison.InvariantCultureIgnoreCase))
                            {
                                continue;
                            }
                        }

                        float cursorXBeforeElement = ImGui.GetCursorPosX();
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, System.Numerics.Vector2.Zero);
                        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, System.Numerics.Vector2.Zero);
                        ImGui.BeginChild(ImGui.GetID("Asset_" + aRef.AssetID), new System.Numerics.Vector2(96, 112));
                        AssetStatus a = Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestPreview(aRef.AssetID, out AssetPreview ap);

                        bool hover = ImGui.IsWindowHovered();
                        bool clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

                        var cursorCurrent = ImGui.GetCursorScreenPos();
                        ImDrawListPtr idlp = ImGui.GetWindowDrawList();

                        if (a == AssetStatus.Await)
                        {
                            int frame =
                                (int)((int)Client.Instance.Frontend.UpdatesExisted % 90 / 90.0f * this.LoadingSpinnerFrames);
                            float texelIndexStart = (float)frame / this.LoadingSpinnerFrames;
                            float texelSize = 1f / this.LoadingSpinnerFrames;
                            idlp.AddImage(a == AssetStatus.Error ? this.ErrorIcon : this.LoadingSpinner, cursorCurrent, cursorCurrent + new System.Numerics.Vector2(96, 96), new System.Numerics.Vector2(texelIndexStart, 0), new System.Numerics.Vector2(texelIndexStart + texelSize, 1));
                            if (hover)
                            {
                                idlp.AddRect(cursorCurrent, cursorCurrent + new System.Numerics.Vector2(96, 96), ImGui.GetColorU32(ImGuiCol.ButtonHovered));
                            }
                        }
                        else
                        {
                            idlp.AddImage(a is AssetStatus.Error or AssetStatus.NoAsset ? this.ErrorIcon : ap.GetGLTexture(), cursorCurrent, cursorCurrent + new System.Numerics.Vector2(96, 96));
                            if (hover)
                            {
                                idlp.AddRect(cursorCurrent, cursorCurrent + new System.Numerics.Vector2(96, 96), ImGui.GetColorU32(ImGuiCol.ButtonHovered));
                            }
                        }

                        string aName = aRef.Name;
                        System.Numerics.Vector2 ts = FitLabelByBinarySearch(aRef.Name, ref aName);
                        idlp.AddText(cursorCurrent + new System.Numerics.Vector2(48 - (ts.X * 0.5f), 96), hover ? ImGui.GetColorU32(ImGuiCol.HeaderHovered) : ImGui.GetColorU32(ImGuiCol.Text), aName);
                        idlp.AddImage(
                            aRef.Type switch
                            {
                                AssetType.Texture => this.AssetImageIcon,
                                AssetType.Model => this.AssetModelIcon,
                                AssetType.Shader => this.AssetShaderIcon,
                                AssetType.ParticleSystem => this.AssetParticleIcon,
                                AssetType.Sound => aRef.Meta?.SoundInfo?.IsFullData ?? false ? this.AssetSoundIcon : aRef.Meta?.SoundInfo?.SoundType == SoundData.Metadata.StorageType.Mpeg ? this.AssetCompressedMusicIcon : this.AssetMusicIcon,
                                _ => this.ErrorIcon
                            },

                            cursorCurrent + new System.Numerics.Vector2(80, 80),
                            cursorCurrent + new System.Numerics.Vector2(96, 96)
                        );

                        if (hover)
                        {
                            if (!this._lmbDown && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                            {
                                this._lmbDown = true;
                                this._draggedRef = aRef;
                            }
                        }

                        #region Context Menu
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

                                if (aRef.Meta != null && aRef.Meta.Type == AssetType.Sound)
                                {
                                    if (ImGui.MenuItem(lang.Translate("ui.assets.play_sound") + "###Play"))
                                    {
                                        Client.Instance.Frontend.Sound.PlayAsset(aRef.AssetID);
                                    }

                                    if (ImGui.MenuItem(lang.Translate("ui.assets.play_sound_all") + "###PlayForAll"))
                                    {
                                        new PacketPlaySoundAsset() { SoundID = aRef.AssetID }.Send();
                                    }

                                    if (ImGui.MenuItem(lang.Translate("ui.assets.stop_sound") + "###Stop"))
                                    {
                                        Client.Instance.Frontend.Sound.StopAsset(aRef.AssetID);
                                    }

                                    if (ImGui.MenuItem(lang.Translate("ui.assets.stop_sound_all") + "###StopForAll"))
                                    {
                                        new PacketPlaySoundAsset() { SoundID = aRef.AssetID, Stop = true }.Send();
                                    }
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

                                if (aRef.Meta != null && aRef.Meta.Type == AssetType.Model)
                                {
                                    if (ImGui.MenuItem(lang.Translate("ui.assets.edit.model") + "###Edit Model"))
                                    {
                                        state.editModelPopup = true;
                                        this._editedRef = aRef;
                                        this._editedModelMetadataCopy = aRef.Meta.ModelInfo.Copy();
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

                                    if (ImGui.MenuItem(lang.Translate("ui.assets.duplicate_particle") + "###Duplicate Particle System"))
                                    {
                                        Client.Instance.AssetManager.ClientAssetLibrary.PerformClientAssetAction(aRef.AssetID, AssetType.ParticleSystem, (status, a) =>
                                        {
                                            if (status == AssetStatus.Return && a != null && a.Type == AssetType.ParticleSystem)
                                            {
                                                ParticleSystem ps = a.ParticleSystem.Copy();
                                                AssetMetadata metadata = new AssetMetadata() { Name = aRef.Name + " (copy)", Type = AssetType.ParticleSystem, Version = aRef.Meta.Version };
                                                using MemoryStream ms = new MemoryStream();
                                                using BinaryWriter bw = new BinaryWriter(ms);
                                                ps.WriteV2(bw);
                                                using Image<Rgba32> img = new Image<Rgba32>(256, 256, new Rgba32(0.39f, 0.39f, 0.39f, 1.0f));
                                                using MemoryStream imgMs = new MemoryStream();
                                                img.SaveAsPng(imgMs);
                                                PacketAssetUpload pau = new PacketAssetUpload() { AssetBinary = new Asset().ToBinary(ms.ToArray()), AssetPreview = imgMs.ToArray(), IsServer = false, Meta = metadata, Path = this.CurrentFolder.GetPath(), Session = Client.Instance.SessionID };
                                                pau.Send(Client.Instance.NetClient);
                                            }
                                        });
                                    }
                                }

                                if (aRef.Meta != null && aRef.Meta.Type == AssetType.Shader)
                                {
                                    if (ImGui.MenuItem(lang.Translate("ui.assets.edit_shader") + "###Edit Shader"))
                                    {
                                        state.editShaderPopup = true;
                                        this._editedShaderId = aRef.AssetID;
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
                        #endregion

                        ImGui.EndChild();
                        if (hover)
                        {
                            ImGui.BeginTooltip();
                            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                            ImGui.TextUnformatted(aRef.AssetID.ToString());
                            ImGui.PopStyleColor();

                            ImGui.TextUnformatted(aRef.Name);
                            ImGui.TextUnformatted(lang.Translate("ui.asset_type." + aRef.Type.ToString().ToLower()));
                            if (aRef?.Meta?.SoundInfo?.SoundType == SoundData.Metadata.StorageType.Mpeg)
                            {
                                ImGui.TextUnformatted(lang.Translate("ui.sound_compressed.tt"));
                            }

                            ImGui.EndTooltip();
                        }

                        ImGui.PopStyleVar();
                        ImGui.PopStyleVar();
                        if (cursorXBeforeElement + 192 < cw)
                        {
                            ImGui.SameLine();
                        }

                    }

                    ImGui.EndChild();
                }

                ImGui.End();
            }
            this.ProcessFileDropEvents();
        }

        private unsafe void ProcessFileDropEvents()
        {
            for (int i = this.FrameState.dropEvents.Count - 1; i >= 0; i--)
            {
                string s = this.FrameState.dropEvents[i];
                if (this.ProcessFileDrop(s, i))
                {
                    this.FrameState.dropEvents.RemoveAt(i);
                }
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

                    List<MapObject> os = Client.Instance.Frontend.Renderer.SelectionManager.SelectedObjects;
                    if (!haveResult && state.objectModelHovered != null && Client.Instance.IsAdmin)
                    {
                        foreach (MapObject mo in os)
                        {
                            new PacketChangeObjectAsset() { AssetID = this._draggedRef.AssetID, MapID = mo.MapID, ObjectID = mo.ID }.Send();
                        }

                        haveResult = true;
                    }

                    if (!haveResult && state.objectCustomNameplateHovered != null && Client.Instance.IsAdmin && this._draggedRef != null && this._draggedRef.Type == AssetType.Texture)
                    {
                        state.objectCustomNameplateHovered.CustomNameplateID = this._draggedRef.AssetID;
                        new PacketMapObjectGenericData() { ChangeType = PacketMapObjectGenericData.DataType.CustomNameplateID, Data = SelectedToPacket3(os, this._draggedRef.AssetID) }.Send();
                        haveResult = true;
                    }

                    if (!haveResult && state.objectCustomShaderHovered != null && Client.Instance.IsAdmin && this._draggedRef != null && this._draggedRef.Type == AssetType.Shader)
                    {
                        new PacketMapObjectGenericData { ChangeType = PacketMapObjectGenericData.DataType.ShaderID, Data = SelectedToPacket3(os, this._draggedRef.AssetID) }.Send();
                        haveResult = true;
                    }

                    if (!haveResult && state.particleModelHovered != null && Client.Instance.IsAdmin)
                    {
                        state.particleModelHovered.AssetID = this._draggedRef.AssetID;
                        haveResult = true;
                    }

                    if (!haveResult && state.particleShaderHovered != null && Client.Instance.IsAdmin)
                    {
                        state.particleShaderHovered.CustomShaderID = this._draggedRef.AssetID;
                        haveResult = true;
                    }

                    if (!haveResult && state.particleMaskHovered != null && Client.Instance.IsAdmin)
                    {
                        state.particleMaskHovered.MaskID = this._draggedRef.AssetID;
                        haveResult = true;
                    }

                    if (!haveResult && state.mapAmbianceHovered != null && Client.Instance.IsAdmin)
                    {
                        new PacketChangeMapData() { Data = this._draggedRef.AssetID, MapID = state.clientMap.ID, Type = PacketChangeMapData.DataType.AmbientSoundID }.Send();
                        haveResult = true;
                    }

                    if (!haveResult && state.particleContainerHovered != null && Client.Instance.IsAdmin)
                    {
                        state.particleContainerHovered.SystemID = this._draggedRef.AssetID;
                        new PacketParticleContainer() { ActionType = PacketParticleContainer.Action.Edit, Container = state.particleContainerHovered.Serialize(), MapID = state.particleContainerHovered.Container.MapID, ObjectID = state.particleContainerHovered.Container.ID, ParticleID = state.particleContainerHovered.ID }.Send();
                        haveResult = true;
                    }

                    if (!haveResult && state.movingAssetOverMusicPlayerAddPoint && Client.Instance.IsAdmin && this._draggedRef?.Type == AssetType.Sound)
                    {
                        (Guid, float) kv = (this._draggedRef.AssetID, 1.0f);
                        new PacketMusicPlayerAction() { ActionType = PacketMusicPlayerAction.Type.Add, IndexMain = Client.Instance.Frontend.Sound.MusicPlayer.Tracks.Count, Data = kv }.Send();
                        haveResult = true;
                    }

                    if (!haveResult && state.shaderGraphExtraTexturesHovered != null && Client.Instance.IsAdmin)
                    {
                        if (state.shaderGraphExtraTexturesHoveredIndex == -1)
                        {
                            state.shaderGraphExtraTexturesHovered.ExtraTexturesAttachments.Add(this._draggedRef.AssetID);
                        }
                        else
                        {
                            state.shaderGraphExtraTexturesHovered.ExtraTexturesAttachments[state.shaderGraphExtraTexturesHoveredIndex] = this._draggedRef.AssetID;
                        }
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
            while (c < original.Length && label_size.X < 96)
            {
                txt += original[c++];
                label_size = ImGuiHelper.CalcTextSize(txt);
            }

            if (c < original.Length)
            {
                txt = txt[..^1];
                txt += "…";
            }

            return label_size;
        }
    }
}
