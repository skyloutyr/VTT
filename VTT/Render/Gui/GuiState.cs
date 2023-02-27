namespace VTT.Render.Gui
{
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
        public bool changeAuraColorPopup = false;
        public bool editTexturePopup = false;
        public AssetDirectory moveTo = null;
        public bool mouseOverMoveUp = false;
        public AssetDirectory dirHovered = null;
        public MapObject objectModelHovered = null;
        public MapObject objectCustomNameplateHovered = null;
        public MapObject objectCustomShaderHovered = null;
        public ParticleSystem particleModelHovered = null;
        public ParticleContainer particleContainerHovered = null;
        public bool editParticleSystemPopup = false;
        public bool changeParticleColorPopup = false;
        public bool editShaderPopup = false;
        public Map clientMap = null;
    
        public void Reset()
        {
            openNewFolderPopup = false;
            editFolderPopup = false;
            deleteFolderPopup = false;
            editAssetPopup = false;
            deleteAssetPopup = false;
            changeColorPopup = false;
            changeMapColorPopup = false;
            deleteMapPopup = false;
            rollPopup = false;
            changeTeamColorPopup = false;
            menu = false;
            inspectPopup = false;
            linkPopup = false;
            journalPopup = false;
            newStatusEffectPopup = false;
            changeTintColorPopup = false;
            changeAuraColorPopup = false;
            editTexturePopup = false;
            moveTo = null;
            mouseOverMoveUp = false;
            dirHovered = null;
            objectModelHovered = null;
            objectCustomNameplateHovered = null;
            objectCustomShaderHovered = null;
            editParticleSystemPopup = false;
            particleModelHovered = null;
            changeParticleColorPopup = false;
            particleContainerHovered = null;
            clientMap = Client.Instance.CurrentMap;
            editShaderPopup = false;
        }
    }
}
