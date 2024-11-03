namespace VTT.Render.Gui
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using VTT.Asset;
    using VTT.Asset.Shader.NodeGraph;
    using VTT.Control;
    using VTT.Network;

    public class GuiState
    {
        public bool openNewFolderPopup = false;
        public bool editFolderPopup = false;
        public bool deleteFolderPopup = false;
        public bool editAssetPopup = false;
        public bool deleteAssetPopup = false;
        public bool changeColorPopup = false;
        public bool changeMapColorPopup = false;
        public bool deleteMapPopup = false;
        public bool rollPopup = false;
        public bool changeTeamColorPopup = false;
        public bool menu = false;
        public bool inspectPopup = false;
        public bool linkPopup = false;
        public bool journalPopup = false;
        public bool newStatusEffectPopup = false;
        public bool changeTintColorPopup = false;
        public bool changeNameColorPopup = false;
        public bool changeAuraColorPopup = false;
        public bool changeFastLightColorPopup = false;
        public bool editTexturePopup = false;
        public bool editModelPopup = false;
        public AssetDirectory moveTo = null;
        public bool mouseOverMoveUp = false;
        public AssetDirectory dirHovered = null;
        public MapObject objectModelHovered = null;
        public MapObject objectCustomNameplateHovered = null;
        public MapObject objectCustomShaderHovered = null;
        public ParticleSystem particleModelHovered = null;
        public ParticleSystem particleShaderHovered = null;
        public ParticleSystem particleMaskHovered = null;
        public ParticleContainer particleContainerHovered = null;
        public ShaderGraph shaderGraphExtraTexturesHovered = null;
        public int shaderGraphExtraTexturesHoveredIndex = -1;
        public bool editParticleSystemPopup = false;
        public bool changeParticleColorPopup = false;
        public bool editShaderPopup = false;
        public Map clientMap = null;
        public Map mapAmbianceHovered = null;
        public bool movingAssetOverMusicPlayerAddPoint = false;
        public bool movingParticleAssetOverFXRecepticle = false;
        public ConcurrentQueue<string> dropEventsReceiver = new ConcurrentQueue<string>();
        public List<string> dropEvents = new List<string>();
        public MapObject overrideObjectOpenRightClickContextMenu;

        public void Reset()
        {
            this.openNewFolderPopup = false;
            this.editFolderPopup = false;
            this.deleteFolderPopup = false;
            this.editAssetPopup = false;
            this.deleteAssetPopup = false;
            this.changeColorPopup = false;
            this.changeMapColorPopup = false;
            this.deleteMapPopup = false;
            this.rollPopup = false;
            this.changeTeamColorPopup = false;
            this.menu = false;
            this.inspectPopup = false;
            this.linkPopup = false;
            this.journalPopup = false;
            this.newStatusEffectPopup = false;
            this.changeTintColorPopup = false;
            this.changeNameColorPopup = false;
            this.changeAuraColorPopup = false;
            this.changeFastLightColorPopup = false;
            this.editTexturePopup = false;
            this.moveTo = null;
            this.mouseOverMoveUp = false;
            this.dirHovered = null;
            this.objectModelHovered = null;
            this.objectCustomNameplateHovered = null;
            this.objectCustomShaderHovered = null;
            this.editParticleSystemPopup = false;
            this.particleModelHovered = null;
            this.particleShaderHovered = null;
            this.particleMaskHovered = null;
            this.changeParticleColorPopup = false;
            this.particleContainerHovered = null;
            this.shaderGraphExtraTexturesHovered = null;
            this.shaderGraphExtraTexturesHoveredIndex = -1;
            this.clientMap = Client.Instance.CurrentMap;
            this.editShaderPopup = false;
            this.editModelPopup = false;
            this.mapAmbianceHovered = null;
            this.movingAssetOverMusicPlayerAddPoint = false;
            this.movingParticleAssetOverFXRecepticle = false;
            this.dropEvents.Clear();
            while (!this.dropEventsReceiver.IsEmpty)
            {
                if (!this.dropEventsReceiver.TryDequeue(out string res))
                {
                    break;
                }

                this.dropEvents.Add(res);
            }

            this.overrideObjectOpenRightClickContextMenu = null;
        }
    }
}
