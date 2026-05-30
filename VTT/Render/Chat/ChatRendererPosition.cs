namespace VTT.Render.Chat
{
    using ImGuiNET;
    using System;
    using System.Numerics;
    using VTT.Control;
    using VTT.Network;
    using VTT.Util;

    public class ChatRendererPosition : ChatRendererBase
    {
        private readonly Guid _localOwnID;
        public ChatRendererPosition(ChatLine container) : base(container) => this._localOwnID = Guid.NewGuid();

        public override void Cache(Vector2 windowSize, out float width, out float height)
        {
            width = 320f;
            height = 48f;
        }

        public override void ClearCache()
        {
        }

        public override string ProvideTextForClipboard(DateTime dateTime, string senderName, SimpleLanguage lang) => $"{this.Container.GetBlockTextOrEmpty(0)} - {this.Container.GetBlockTextOrEmpty(2)},{this.Container.GetBlockTextOrEmpty(3)}";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "Here we don't actually care if the conversion succeeded or not - a 0,0,0 is a fine position too.")]
        public override void Render(Guid senderId, uint senderColorAbgr)
        {
            this.Container.TryGetBlockAt(0, out ChatBlock msgBlock);
            string msg = msgBlock?.Text ?? string.Empty;
            this.Container.TryGetBlockAt(1, out ChatBlock mapIDBlock);
            this.Container.TryGetBlockAt(2, out ChatBlock posXBlock);
            this.Container.TryGetBlockAt(3, out ChatBlock posYBlock);
            this.Container.TryGetBlockAt(4, out ChatBlock posZBlock);
            Vector2 start = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new(320f, 48f));
            ImGui.SetCursorScreenPos(start + new Vector2(160f - (ImGui.CalcTextSize(msg).X / 2f), 0f));
            ColorAbgr clr = msgBlock?.Color ?? ColorAbgr.Transparent;
            if (clr != ColorAbgr.Transparent)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, clr);
            }

            ImGui.TextUnformatted(msg);
            if (clr != ColorAbgr.Transparent)
            {
                ImGui.PopStyleColor();
            }

            if (!Guid.TryParse(mapIDBlock?.Text ?? string.Empty, out Guid mid))
            {
                mid = Guid.Empty;
            }

            SimpleLanguage lang = Client.Instance.Lang;
            if (!Guid.Equals(mid, Client.Instance.CurrentMap?.ID ?? Guid.Empty))
            {
                ImGui.Button(lang.Translate("ui.chat.embed_location.wrong_map") + "###GotoMapBTN_" + this._localOwnID, new Vector2(320f, 24f));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.chat.embed_location.wrong_map.tt"));
                }
            }
            else
            {
                float.TryParse(posXBlock?.Text ?? string.Empty, out float x);
                float.TryParse(posYBlock?.Text ?? string.Empty, out float y);
                float.TryParse(posZBlock?.Text ?? string.Empty, out float z);
                if (ImGui.Button(lang.Translate("ui.chat.embed_location.goto_location", x, y, z) + "###GotoMapBTN_" + this._localOwnID, new Vector2(320f, 24f)))
                {
                    Vector3 camPos = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position;
                    Vector3 oPos = new Vector3(x, y, z);
                    if (Client.Instance.Frontend.Renderer.MapRenderer.IsOrtho)
                    {
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.MoveCamera(new Vector3(oPos.X, oPos.Y, camPos.Z), true);
                    }
                    else
                    {
                        Vector3 camDirection = Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Direction;
                        Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.MoveCamera(oPos - (camDirection * 5.0f), true);
                    }

                    Client.Instance.Frontend.Renderer.PingRenderer.AddPing(new Ping() 
                    {
                        OwnerID = this.Container.SenderID,
                        DeathTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 6000,
                        OwnerColor = this.Container.SenderColor,
                        OwnerName = this.Container.SenderDisplayName,
                        Position = oPos,
                        Type = Ping.PingType.Generic,
                        IsSilentWhenCreated = true
                    });
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(lang.Translate("ui.chat.embed_location.goto_location.tt"));
                }
            }
        }

        public static string CreateChatMessage(Vector3 position, string header)
        {
            ChatBuilder cb = new ChatBuilder()
                .SpecifyRenderType(ChatLine.RenderType.Position)
                .AddPassthrough(header)
                .AddPassthrough(Client.Instance.CurrentMap.ID.ToString())
                .AddPassthrough(position.X.ToString())
                .AddPassthrough(position.Y.ToString())
                .AddPassthrough(position.Z.ToString());

            return cb.ToString();
        }
    }
}
