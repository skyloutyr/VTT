namespace VTT.Control
{
    using System.Numerics;
    using System;
    using VTT.Asset;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;
    using VTT.Render;

    public class ParticleContainer : ISerializable
    {
        public Guid ID { get; set; }
        public Guid SystemID { get; set; }
        public Vector3 ContainerPositionOffset { get; set; }
        public bool UseContainerOrientation { get; set; }
        public bool RotateVelocityByOrientation { get; set; }
        public bool IsActive { get; set; }
        public string AttachmentPoint { get; set; } = string.Empty;

        public MapObject Container { get; }
        public ParticleContainer(MapObject container) => this.Container = container;

        private ParticleSystemInstance _psIns;
        private bool _psInsDispose;

        public Guid CustomShaderID => this._psIns?.Template?.CustomShaderID ?? Guid.Empty;

        public int ParticlesToEmit { get; set; }
        public bool IsFXEmitter { get; set; }

        public void Update()
        {
            if (this.IsActive && !Guid.Empty.Equals(this.SystemID))
            {
                this._psIns?.Update(Client.Instance.Frontend.Renderer.MapRenderer.ClientCamera.Position);
                if (this.IsFXEmitter && this.ParticlesToEmit <= 0 && this._psIns.NumActiveParticles <= 0)
                {
                    this.ParticlesToEmit = -1;
                }
            }
        }

        public void UpdateBufferState()
        {
            if (this.IsActive && !Guid.Empty.Equals(this.SystemID))
            {
                if (this._psIns != null && this._psInsDispose)
                {
                    this._psInsDispose = false;
                    this._psIns.Dispose();
                    this._psIns = null;
                }

                if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(this.SystemID, AssetType.ParticleSystem, out Asset a) == AssetStatus.Return && a?.ParticleSystem != null)
                {
                    if (this._psIns == null)
                    {
                        this._psIns = new ParticleSystemInstance(a.ParticleSystem, this);
                    }

                    this._psIns.UpdateBufferState();
                }
                else
                {
                    this._psIns = null;
                }
            }
        }

        public void Render(FastAccessShader shader, Camera cam)
        {
            if (this.IsActive && !Guid.Empty.Equals(this.SystemID))
            {
                this._psIns?.Render(shader, cam.Position, cam);
            }
        }

        public void DisposeInternal()
        {
            this._psIns?.Dispose();
            this._psIns = null;
        }

        public DataElement Serialize()
        {
            DataElement ret = new DataElement();
            ret.SetGuid("ID", this.ID);
            ret.SetGuid("SystemID", this.SystemID);
            ret.SetVec3("Pos", this.ContainerPositionOffset);
            ret.SetBool("UseOrientation", this.UseContainerOrientation);
            ret.SetBool("Active", this.IsActive);
            ret.SetString("Attach", this.AttachmentPoint);
            ret.SetBool("DoVRot", this.RotateVelocityByOrientation);
            ret.SetInt("PLeft", this.ParticlesToEmit);
            ret.SetBool("IsFX", this.IsFXEmitter);
            return ret;
        }

        public void Deserialize(DataElement e)
        {
            this.ID = e.GetGuid("ID");
            this.SystemID = e.GetGuid("SystemID");
            if (this._psIns != null)
            {
                this._psInsDispose = true;
            }

            this.ContainerPositionOffset = e.GetVec3("Pos");
            this.UseContainerOrientation = e.GetBool("UseOrientation");
            this.IsActive = e.GetBool("Active");
            this.AttachmentPoint = e.GetString("Attach");
            this.RotateVelocityByOrientation = e.GetBool("DoVRot", false);
            this.ParticlesToEmit = e.GetInt("PLeft", 0);
            this.IsFXEmitter = e.GetBool("IsFX", false);
        }
    }
}
