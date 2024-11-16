namespace VTT.Asset
{
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using VTT.Asset.Glb;
    using VTT.Control;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Render;
    using VTT.Util;
    using GL = GL.Bindings.GL;

    public class ParticleSystem
    {
        public static Dictionary<Guid, List<WeightedList<Vector2>>> ImageEmissionLocations { get; } = new Dictionary<Guid, List<WeightedList<Vector2>>>();
        public static readonly object imageEmissionLock = new object();

        public EmissionMode EmissionType { get; set; } = EmissionMode.Point;
        public float EmissionRadius { get; set; } = 1.0f;
        public Vector3 EmissionVolume { get; set; } = Vector3.One;
        public float EmissionChance { get; set; } = 1.0f;
        public RangeInt EmissionAmount { get; set; } = new RangeInt(1, 1);
        public RangeInt EmissionCooldown { get; set; } = new RangeInt(0, 0);
        public RangeInt Lifetime { get; set; } = new RangeInt(60, 60);
        public RangeSingle ScaleVariation { get; set; } = new RangeSingle(1, 1);

        public int MaxParticles { get; set; }
        public bool DoBillboard { get; set; } = true;
        public bool DoFow { get; set; } = false;
        public bool ClusterEmission { get; set; } = false;

        public RangeVector3 InitialVelocity { get; set; } = new RangeVector3(Vector3.Zero, Vector3.Zero);
        public float InitialVelocityRandomAngle { get; set; } = 0f;
        public Vector3 Gravity { get; set; } = new Vector3(0, 0, -1 / 60.0f);
        public float VelocityDampenFactor { get; set; } = 0.98f;

        public Gradient<Vector4> ColorOverLifetime { get; set; } = new Gradient<Vector4>() { [0] = Vector4.One, [1] = new Vector4(1, 1, 1, 0) };
        public Gradient<float> ScaleOverLifetime { get; set; } = new Gradient<float>() { [0] = 1f, [1] = 1f };
        public bool IsSpriteSheet { get; set; } = false;
        public SpriteSheetData SpriteData { get; set; } = new SpriteSheetData();
        public bool SpriteSheetIsAnimation { get; set; } = false;

        /// <summary>
        /// Note - NOT OWN ID! This is the ID of the asset the particles use for rendering.
        /// </summary>
        public Guid AssetID { get; set; }
        public Guid CustomShaderID { get; set; }
        public Guid MaskID { get; set; }

        public void WriteV1(BinaryWriter bw)
        {
            bw.WriteEnumSmall(this.EmissionType);
            bw.Write(this.EmissionRadius);
            bw.Write(this.EmissionVolume);
            bw.Write(this.EmissionChance);
            bw.Write(this.EmissionAmount.Min);
            bw.Write(this.EmissionAmount.Max);
            bw.Write(this.EmissionCooldown.Min);
            bw.Write(this.EmissionCooldown.Max);
            bw.Write(this.Lifetime.Min);
            bw.Write(this.Lifetime.Max);
            bw.Write(this.ScaleVariation.Min);
            bw.Write(this.ScaleVariation.Max);
            bw.Write(this.MaxParticles);
            bw.Write(this.InitialVelocity.Min);
            bw.Write(this.InitialVelocity.Max);
            bw.Write(this.InitialVelocityRandomAngle);
            bw.Write(this.Gravity);
            bw.Write(this.VelocityDampenFactor);
            bw.Write(this.ColorOverLifetime.Count);
            foreach (KeyValuePair<float, Vector4> kv in this.ColorOverLifetime)
            {
                bw.Write(kv.Key);
                bw.Write(kv.Value);
            }

            bw.Write(this.ScaleOverLifetime.Count);
            foreach (KeyValuePair<float, float> kv in this.ScaleOverLifetime)
            {
                bw.Write(kv.Key);
                bw.Write(kv.Value);
            }

            bw.Write(this.AssetID);
            bw.Write(this.CustomShaderID);
        }

        public void WriteV2(BinaryWriter bw)
        {
            DataElement ret = new DataElement();
            ret.SetEnum("EmissionType", this.EmissionType);
            ret.SetSingle("EmissionRadius", this.EmissionRadius);
            ret.SetVec3("EmissionVolume", this.EmissionVolume);
            ret.SetSingle("EmissionChance", this.EmissionChance);
            ret.SetInt("EmissionAmountLo", this.EmissionAmount.Min);
            ret.SetInt("EmissionAmountHi", this.EmissionAmount.Max);
            ret.SetInt("EmissionCooldownLo", this.EmissionCooldown.Min);
            ret.SetInt("EmissionCooldownHi", this.EmissionCooldown.Max);
            ret.SetInt("LifetimeLo", this.Lifetime.Min);
            ret.SetInt("LifetimeHi", this.Lifetime.Max);
            ret.SetSingle("ScaleVariationLo", this.ScaleVariation.Min);
            ret.SetSingle("ScaleVariationHi", this.ScaleVariation.Max);
            ret.SetInt("MaxParticles", this.MaxParticles);
            ret.SetVec3("InitialVelocityLo", this.InitialVelocity.Min);
            ret.SetVec3("InitialVelocityHi", this.InitialVelocity.Max);
            ret.SetSingle("InitialVelocityRandomAngle", this.InitialVelocityRandomAngle);
            ret.SetVec3("Gravity", this.Gravity);
            ret.SetSingle("VelocityDampenFactor", this.VelocityDampenFactor);
            ret.SetArray("ColorOverLifetime", this.ColorOverLifetime.Select(x =>
            {
                DataElement ret = new DataElement();
                ret.SetSingle("k", x.Key);
                ret.SetVec4("v", x.Value);
                return ret;
            }).ToArray(), (n, c, e) => c.SetMap(n, e));

            ret.SetArray("ScaleOverLifetime", this.ScaleOverLifetime.Select(x =>
            {
                DataElement ret = new DataElement();
                ret.SetSingle("k", x.Key);
                ret.SetSingle("v", x.Value);
                return ret;
            }).ToArray(), (n, c, e) => c.SetMap(n, e));

            ret.SetGuid("AssetID", this.AssetID);
            ret.SetBool("DoBillboard", this.DoBillboard);
            ret.SetBool("ClusterEmission", this.ClusterEmission);
            ret.SetBool("DoFow", this.DoFow);
            ret.SetBool("IsSpriteSheet", this.IsSpriteSheet);
            ret.SetMap("SpriteSheetData", this.SpriteData.Serialize());
            ret.SetGuid("CustomShaderID", this.CustomShaderID);
            ret.SetGuid("MaskID", this.MaskID);
            ret.SetBool("SpriteSheetIsAnimation", this.SpriteSheetIsAnimation);
            ret.Write(bw);
        }

        public void ReadV2(BinaryReader br)
        {
            DataElement de = new DataElement(br);
            this.EmissionType = de.GetEnum<EmissionMode>("EmissionType");
            this.EmissionRadius = de.GetSingle("EmissionRadius");
            this.EmissionVolume = de.GetVec3("EmissionVolume");
            this.EmissionChance = de.GetSingle("EmissionChance");
            this.EmissionAmount = new RangeInt(de.GetInt("EmissionAmountLo"), de.GetInt("EmissionAmountHi"));
            this.EmissionCooldown = new RangeInt(de.GetInt("EmissionCooldownLo"), de.GetInt("EmissionCooldownHi"));
            this.Lifetime = new RangeInt(de.GetInt("LifetimeLo"), de.GetInt("LifetimeHi"));
            this.ScaleVariation = new RangeSingle(de.GetSingle("ScaleVariationLo"), de.GetSingle("ScaleVariationHi"));
            this.MaxParticles = de.GetInt("MaxParticles");
            this.InitialVelocity = new RangeVector3(de.GetVec3("InitialVelocityLo"), de.GetVec3("InitialVelocityHi"));
            this.InitialVelocityRandomAngle = de.GetSingle("InitialVelocityRandomAngle");
            this.Gravity = de.GetVec3("Gravity");
            this.VelocityDampenFactor = de.GetSingle("VelocityDampenFactor");
            DataElement[] col = de.GetArray("ColorOverLifetime", (n, c) => c.GetMap(n), Array.Empty<DataElement>());
            this.ColorOverLifetime.Clear();
            foreach (DataElement e in col)
            {
                this.ColorOverLifetime[e.GetSingle("k")] = e.GetVec4("v");
            }

            col = de.GetArray("ScaleOverLifetime", (n, c) => c.GetMap(n), Array.Empty<DataElement>());
            this.ScaleOverLifetime.Clear();
            foreach (DataElement e in col)
            {
                this.ScaleOverLifetime[e.GetSingle("k")] = e.GetSingle("v");
            }

            this.AssetID = de.GetGuid("AssetID");
            this.DoBillboard = de.GetBool("DoBillboard", true);
            this.ClusterEmission = de.GetBool("ClusterEmission", false);
            this.DoFow = de.GetBool("DoFow", false);
            this.IsSpriteSheet = de.GetBool("IsSpriteSheet", false);
            if (de.Has("SpriteSheetData", DataType.Map))
            {
                this.SpriteData.Deserialize(de.GetMap("SpriteSheetData"));
            }

            this.CustomShaderID = de.GetGuid("CustomShaderID", Guid.Empty);
            this.MaskID = de.GetGuid("MaskID", Guid.Empty);
            this.SpriteSheetIsAnimation = de.GetBool("SpriteSheetIsAnimation", false);
        }

        public void ReadV1(BinaryReader br)
        {
            this.EmissionType = br.ReadEnumSmall<EmissionMode>();
            this.EmissionRadius = br.ReadSingle();
            this.EmissionVolume = br.ReadGlVec3();
            this.EmissionChance = br.ReadSingle();
            this.EmissionAmount = new RangeInt(br.ReadInt32(), br.ReadInt32());
            this.EmissionCooldown = new RangeInt(br.ReadInt32(), br.ReadInt32());
            this.Lifetime = new RangeInt(br.ReadInt32(), br.ReadInt32());
            this.ScaleVariation = new RangeSingle(br.ReadSingle(), br.ReadSingle());
            this.MaxParticles = br.ReadInt32();
            this.InitialVelocity = new RangeVector3(br.ReadGlVec3(), br.ReadGlVec3());
            this.InitialVelocityRandomAngle = br.ReadSingle();
            this.Gravity = br.ReadGlVec3();
            this.VelocityDampenFactor = br.ReadSingle();
            int c = br.ReadInt32();
            this.ColorOverLifetime = new Gradient<Vector4>();
            while (c-- > 0)
            {
                this.ColorOverLifetime[br.ReadSingle()] = br.ReadGlVec4();
            }

            c = br.ReadInt32();
            this.ScaleOverLifetime = new Gradient<float>();
            while (c-- > 0)
            {
                this.ScaleOverLifetime[br.ReadSingle()] = br.ReadSingle();
            }

            this.AssetID = br.ReadGuid();
            this.CustomShaderID = br.ReadGuid();
            this.DoBillboard = true;
            this.ClusterEmission = false;
        }

        public ParticleSystem Copy() => new ParticleSystem()
        {
            EmissionType = this.EmissionType,
            EmissionRadius = this.EmissionRadius,
            EmissionVolume = new Vector3(this.EmissionVolume.X, this.EmissionVolume.Y, this.EmissionVolume.Z),
            EmissionChance = this.EmissionChance,
            EmissionAmount = new RangeInt(this.EmissionAmount.Min, this.EmissionAmount.Max),
            EmissionCooldown = new RangeInt(this.EmissionCooldown.Min, this.EmissionCooldown.Max),
            Lifetime = new RangeInt(this.Lifetime.Min, this.Lifetime.Max),
            ScaleVariation = new RangeSingle(this.ScaleVariation.Min, this.ScaleVariation.Max),
            MaxParticles = this.MaxParticles,
            InitialVelocity = new RangeVector3(new Vector3(this.InitialVelocity.Min.X, this.InitialVelocity.Min.Y, this.InitialVelocity.Min.Z), new Vector3(this.InitialVelocity.Max.X, this.InitialVelocity.Max.Y, this.InitialVelocity.Max.Z)),
            InitialVelocityRandomAngle = this.InitialVelocityRandomAngle,
            Gravity = new Vector3(this.Gravity.X, this.Gravity.Y, this.Gravity.Z),
            VelocityDampenFactor = this.VelocityDampenFactor,
            ColorOverLifetime = new Gradient<Vector4>(this.ColorOverLifetime),
            ScaleOverLifetime = new Gradient<float>(this.ScaleOverLifetime),
            AssetID = this.AssetID,
            CustomShaderID = this.CustomShaderID,
            MaskID = this.MaskID,
            DoBillboard = this.DoBillboard,
            DoFow = this.DoFow,
            ClusterEmission = this.ClusterEmission,
            IsSpriteSheet = this.IsSpriteSheet,
            SpriteData = this.SpriteData.Clone(),
            SpriteSheetIsAnimation = this.SpriteSheetIsAnimation
        };

        public class RangeInt
        {
            public int Min { get; set; }
            public int Max { get; set; }

            public RangeInt(int min, int max)
            {
                this.Min = min;
                this.Max = max;
            }

            public int Value(float index) => this.Min + (int)((this.Max - this.Min) * index);
        }

        public class RangeSingle
        {
            public float Min { get; set; }
            public float Max { get; set; }

            public RangeSingle(float min, float max)
            {
                this.Min = min;
                this.Max = max;
            }

            public float Value(float index) => this.Min + ((this.Max - this.Min) * index);
        }

        public class RangeVector3
        {
            public Vector3 Min { get; set; }
            public Vector3 Max { get; set; }

            public RangeVector3(Vector3 min, Vector3 max)
            {
                this.Min = min;
                this.Max = max;
            }

            public Vector3 Value(float indexX, float indexY, float indexZ) => this.Min + ((this.Max - this.Min) * new Vector3(indexX, indexY, indexZ));
        }

        public class SpriteSheetData : ISerializable
        {
            private int _numRows = 1;
            private int _numColumns = 1;
            private int _numSprites = 1;

            public int NumRows { get => this._numRows; set => this._numRows = Math.Max(1, value); }
            public int NumColumns { get => this._numColumns; set => this._numColumns = Math.Max(1, value); }
            public int NumSprites { get => this._numSprites; set => this._numSprites = Math.Max(1, value); }
            public SelectionMode Selection { get; set; }

            public int[] SelectionWeights { get; set; } = new int[1] { 1 };

            public List<WeightedItem<int>> SelectionWeightsList { get; } = new List<WeightedItem<int>>() { new WeightedItem<int>(0, 1) };

            public void ReallocateSelectionWeights()
            {
                int[] newChances = new int[this.NumSprites];
                Array.Fill(newChances, 1);
                if (this.SelectionWeights.Length > 0 && this.NumSprites > 0)
                {
                    Array.Copy(this.SelectionWeights, newChances, Math.Min(newChances.Length, this.SelectionWeights.Length));
                }

                this.SelectionWeights = newChances;
                this.SelectionWeightsList.Clear();
                for (int i = 0; i < newChances.Length; ++i)
                {
                    this.SelectionWeightsList.Add(new WeightedItem<int>(i, this.SelectionWeights[i]));
                }
            }

            public void Init()
            {
                this.NumColumns = 1;
                this.NumRows = 1;
                this.NumSprites = 1;
                this.Selection = SelectionMode.Progressive;
                this.ReallocateSelectionWeights();
            }

            public DataElement Serialize()
            {
                DataElement ret = new DataElement();
                ret.SetInt("nC", this.NumColumns);
                ret.SetInt("nR", this.NumRows);
                ret.SetInt("nS", this.NumSprites);
                ret.SetEnum("sM", this.Selection);
                ret.SetArray("sC", this.SelectionWeights, (n, c, v) => c.SetInt(n, v));
                return ret;
            }

            public void Deserialize(DataElement e)
            {
                this.NumColumns = e.GetInt("nC");
                this.NumRows = e.GetInt("nR");
                this.NumSprites = e.GetInt("nS");
                this.Selection = e.GetEnum<SelectionMode>("sM");
                this.SelectionWeights = e.GetArray("sC", (n, c) => c.GetInt(n, 0), Array.Empty<int>());
                this.SelectionWeightsList.Clear();
                for (int i = 0; i < this.SelectionWeights.Length; ++i)
                {
                    this.SelectionWeightsList.Add(new WeightedItem<int>(i, this.SelectionWeights[i]));
                }
            }

            public SpriteSheetData Clone()
            {
                SpriteSheetData ret = new SpriteSheetData()
                {
                    NumColumns = this.NumColumns,
                    NumRows = this.NumRows,
                    NumSprites = this.NumSprites,
                    Selection = this.Selection,
                    SelectionWeights = (int[])this.SelectionWeights.Clone(),
                };

                ret.SelectionWeightsList.Clear();
                ret.SelectionWeightsList.AddRange(this.SelectionWeightsList);
                return ret;
            }

            public enum SelectionMode
            {
                Progressive,
                Regressive,
                Random,
                First
            }
        }

        public enum EmissionMode
        {
            Point,
            Sphere,
            SphereSurface,
            Cube,
            CubeSurface,
            SquareVolume,
            SquareBoundary,
            CircleVolume,
            CircleBoundary,
            Volume,
            MeshSurface,
            Mask
        }
    }

    public unsafe sealed class ParticleSystemInstance
    {
        public ParticleSystem Template { get; set; }
        public ParticleContainer Container { get; set; }
        public bool IsFake { get; set; }

        private Particle* _allParticles;
        private GLParticleData* _buffer;

        private int _numParticles;
        private int _nLastActiveParticles;
        private bool _glInit;
        private uint _glBufferTexture;
        private uint _glTextureBuffer;
        private int _lastParticleIndex;
        private int _sizeInBytes;
        private int _emissionCd;
        private uint _frameAmount;
        private readonly Random _rand;
        private int _lastSpriteIndex = 0;


        public ParticleSystemInstance(ParticleSystem template, ParticleContainer container)
        {
            this.Template = template;
            this.Container = container;
            this.IsFake = container == null;
            this._rand = new Random();
            this.Resize();
        }

        public int NumActiveParticles => this._nLastActiveParticles;

        public void Resize()
        {
            int numPossibleParticles = this.Template.MaxParticles;
            if (numPossibleParticles == 0)
            {
                numPossibleParticles = this.Template.EmissionAmount.Max * this.Template.Lifetime.Max; // Max amount emitted per tick to NumTicks particles can be alive
            }

            if (numPossibleParticles == 0)
            {
                numPossibleParticles = 1;
            }

            this.Free();
            this._allParticles = MemoryHelper.Allocate<Particle>((nuint)numPossibleParticles);
            this._numParticles = numPossibleParticles;
            this._sizeInBytes = numPossibleParticles * sizeof(GLParticleData);
            this._buffer = MemoryHelper.Allocate<GLParticleData>((nuint)numPossibleParticles);
            for (int i = 0; i < numPossibleParticles; ++i)
            {
                this._allParticles[i] = new Particle() { active = 0, age = 0, color = Vector4.Zero, lifespan = 0, velocity = Vector3.Zero, worldPosition = Vector3.Zero };
            }
        }

        public void Render(FastAccessShader particleShader, Vector3 cameraPosition, Camera cam)
        {
            if (!this._glInit || this.Template.AssetID.Equals(Guid.Empty))
            {
                return;
            }

            if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(this.Template.AssetID, AssetType.Model, out Asset a) != AssetStatus.Return || a == null || !a.ModelGlReady)
            {
                return;
            }

            particleShader.Particle.DoBillboard.Set(this.Template.DoBillboard);
            particleShader.Particle.DoFOW.Set(this.Template.DoFow);
            particleShader.Particle.IsSpriteSheet.Set(this.Template.IsSpriteSheet);
            particleShader.Particle.SpriteSheetData.Set(new Vector2(this.Template.SpriteData.NumColumns, this.Template.SpriteData.NumRows));
            this._frameAmount = (uint)a.Model.GLMdl.Materials.Max(m => m.BaseColorAnimation.Frames.Length);
            GL.ActiveTexture(14);
            GL.BindTexture(TextureTarget.Buffer, this._glBufferTexture);
            a.Model.GLMdl.Render(particleShader, Matrix4x4.Identity, cam.Projection, cam.View, 0, null, 0, null, m => GL.DrawElementsInstanced(PrimitiveType.Triangles, m.AmountToRender, ElementsType.UnsignedInt, IntPtr.Zero, this._numParticles));
            GL.ActiveTexture(0);
        }

        private readonly List<WeightedItem<GlbMesh>> _meshRefs = new List<WeightedItem<GlbMesh>>();

        public void Update(Vector3 cameraPosition)
        {
            int nActive = 0;
            for (int i = 0; i < this._numParticles; i++)
            {
                this._allParticles[i].Update(this);
            }

            for (int i = 0; i < this._numParticles; ++i)
            {
                Particle* p = this._allParticles + i;
                GLParticleData* b = this._buffer + i;
                b->worldPosition = p->worldPosition;
                b->scale = p->active == 1 ? p->scale : 0.0f;
                b->color = VTTMath.UInt32BitsToSingle(Extensions.Rgba(p->color));
                b->animationFrame = VTTMath.UInt32BitsToSingle(p->lifespan == 0 ? 0 : (uint)(this._frameAmount * ((float)p->age / p->lifespan)));
                b->spritemapIndex = VTTMath.Int32BitsToSingle(p->spriteIndex);
                b->lifespan = (float)p->age / p->lifespan;
                if (p->active == 1)
                {
                    ++nActive;
                }
            }

            this._nLastActiveParticles = nActive;
            if (this._emissionCd-- <= 0 && (this.Template.EmissionChance >= 1.0f - float.Epsilon || this._rand.NextDouble() <= this.Template.EmissionChance))
            {
                this._emissionCd = this.Template.EmissionCooldown.Value(this._rand.NextSingle());
                int nNew = this.Template.EmissionAmount.Value(this._rand.NextSingle());
                Vector3 emissionPoint = default;
                for (int i = 0; i < nNew; ++i)
                {
                    if (this.Container != null && this.Container.IsFXEmitter)
                    {
                        if (this.Container.ParticlesToEmit <= 0)
                        {
                            break;
                        }
                        else
                        {
                            ++this._nLastActiveParticles;
                            --this.Container.ParticlesToEmit;
                        }
                    }

                    Particle* p = this.FindFirstDeactivatedParticle();
                    p->active = 1;
                    p->age = 0;
                    p->color = this.Template.ColorOverLifetime.Interpolate(0, GradientInterpolators.LerpVec4);
                    p->lifespan = this.Template.Lifetime.Value(this._rand.NextSingle());
                    p->scaleMod = this.Template.ScaleVariation.Value(this._rand.NextSingle());
                    p->scale = this.Template.ScaleOverLifetime.Interpolate(0, GradientInterpolators.Lerp);
                    p->spriteIndex = 0;


                    if (this.Template.IsSpriteSheet)
                    {
                        switch (this.Template.SpriteData.Selection)
                        {
                            case ParticleSystem.SpriteSheetData.SelectionMode.Progressive:
                            {
                                this._lastSpriteIndex += 1;
                                if (this._lastSpriteIndex >= this.Template.SpriteData.NumSprites)
                                {
                                    this._lastSpriteIndex = 0;
                                }

                                p->spriteIndex = this._lastSpriteIndex;
                                break;
                            }

                            case ParticleSystem.SpriteSheetData.SelectionMode.Regressive:
                            {
                                this._lastSpriteIndex -= 1;
                                if (this._lastSpriteIndex < 0)
                                {
                                    this._lastSpriteIndex = this.Template.SpriteData.NumSprites - 1;
                                }

                                p->spriteIndex = this._lastSpriteIndex;
                                break;
                            }

                            case ParticleSystem.SpriteSheetData.SelectionMode.Random:
                            {
                                p->spriteIndex = WeightedRandom.GetWeightedItem(this.Template.SpriteData.SelectionWeightsList, this._rand).Item;
                                break;
                            }

                            case ParticleSystem.SpriteSheetData.SelectionMode.First:
                            {
                                p->spriteIndex = 0;
                                break;
                            }
                        }
                    }

                    Vector3 baseOffset;
                    if (this.IsFake)
                    {
                        baseOffset = Vector3.Zero;
                    }
                    else
                    {
                        Vector4 bo4 = new Vector4(this.Container.ContainerPositionOffset, 1.0f);
                        Quaternion q = this.Container.IsFXEmitter ? Quaternion.Identity : this.Container.UseContainerOrientation ? this.Container.Container.Rotation : Quaternion.Identity;
                        bo4 = Vector4.Transform(bo4, q);
                        baseOffset = bo4.Xyz() / bo4.W;
                        baseOffset *= this.Container.IsFXEmitter ? Vector3.One : this.Container.Container.Scale;
                        baseOffset += this.Container.IsFXEmitter ? Vector3.Zero : this.Container.Container.Position;
                    }

                    if (!this.Template.ClusterEmission || i == 0)
                    {
                        switch (this.Template.EmissionType)
                        {
                            case ParticleSystem.EmissionMode.Sphere:
                            case ParticleSystem.EmissionMode.SphereSurface:
                            {
                                Vector3 rndUnitVector = new Vector3(
                                    (this._rand.NextSingle() - this._rand.NextSingle()),
                                    (this._rand.NextSingle() - this._rand.NextSingle()),
                                    (this._rand.NextSingle() - this._rand.NextSingle())).Normalized();
                                rndUnitVector *= this.Template.EmissionRadius;
                                if (this.Template.EmissionType == ParticleSystem.EmissionMode.Sphere)
                                {
                                    rndUnitVector *= (float)this._rand.NextSingle();
                                }

                                rndUnitVector *= this.Template.EmissionVolume;
                                baseOffset += rndUnitVector;
                                break;
                            }

                            case ParticleSystem.EmissionMode.Cube:
                            case ParticleSystem.EmissionMode.CubeSurface:
                            {
                                bool surf = this.Template.EmissionType == ParticleSystem.EmissionMode.CubeSurface;
                                float oX = this._rand.NextSingle() - this._rand.NextSingle();
                                float oY = this._rand.NextSingle() - this._rand.NextSingle();
                                float oZ = this._rand.NextSingle() - this._rand.NextSingle();
                                if (surf)
                                {
                                    float rF = this._rand.NextSingle();
                                    if (rF <= 0.333333f)
                                    {
                                        oX = this._rand.NextSingle() < 0.5f ? -1 : 1;
                                    }
                                    else
                                    {
                                        if (rF <= 0.6666666f)
                                        {
                                            oY = this._rand.NextSingle() < 0.5f ? -1 : 1;
                                        }
                                        else
                                        {
                                            oZ = this._rand.NextSingle() < 0.5f ? -1 : 1;
                                        }
                                    }
                                }

                                oX *= this.Template.EmissionVolume.X;
                                oY *= this.Template.EmissionVolume.Y;
                                oZ *= this.Template.EmissionVolume.Z;
                                baseOffset += new Vector3(oX, oY, oZ);
                                break;
                            }

                            case ParticleSystem.EmissionMode.CircleVolume:
                            case ParticleSystem.EmissionMode.CircleBoundary:
                            {
                                bool vol = this.Template.EmissionType == ParticleSystem.EmissionMode.CircleVolume;
                                float vX = this.Template.EmissionVolume.X;
                                float vY = this.Template.EmissionVolume.Y;
                                float vZ = this.Template.EmissionVolume.Z;
                                if (vol)
                                {
                                    vX *= this._rand.NextSingle();
                                    vY *= this._rand.NextSingle();
                                    vZ *= this._rand.NextSingle();
                                }

                                float rngRad = this._rand.NextSingle() * MathF.PI * 2;
                                if (vX > 0 && vY > 0)
                                {
                                    baseOffset += new Vector3(MathF.Cos(rngRad) * vX, MathF.Sin(rngRad) * vY, 0);
                                }
                                else
                                {
                                    if (vY > 0 && vZ > 0)
                                    {
                                        baseOffset += new Vector3(0, MathF.Cos(rngRad) * vY, MathF.Sin(rngRad) * vZ);
                                    }
                                    else
                                    {
                                        baseOffset += new Vector3(MathF.Cos(rngRad) * vX, 0, MathF.Sin(rngRad) * vZ);
                                    }
                                }

                                break;
                            }

                            case ParticleSystem.EmissionMode.SquareVolume:
                            {
                                float vX = this.Template.EmissionVolume.X;
                                float vY = this.Template.EmissionVolume.Y;
                                float vZ = this.Template.EmissionVolume.Z;

                                float o1 = (this._rand.NextSingle() - this._rand.NextSingle());
                                float o2 = (this._rand.NextSingle() - this._rand.NextSingle());
                                if (vX > 0 && vY > 0)
                                {
                                    baseOffset += new Vector3(o1 * vX, o2 * vY, 0);
                                }
                                else
                                {
                                    if (vY > 0 && vZ > 0)
                                    {
                                        baseOffset += new Vector3(0, o1 * vY, o2 * vZ);
                                    }
                                    else
                                    {
                                        baseOffset += new Vector3(o1 * vX, 0, o2 * vZ);
                                    }
                                }

                                break;
                            }

                            case ParticleSystem.EmissionMode.SquareBoundary:
                            {
                                float vX = this.Template.EmissionVolume.X;
                                float vY = this.Template.EmissionVolume.Y;
                                float vZ = this.Template.EmissionVolume.Z;
                                if (vX > 0 && vY > 0)
                                {
                                    // Emitting on a horizontal square
                                    bool isLR = (this._rand.Next() & 1) == 0;
                                    if (isLR)
                                    {
                                        float oX = (this._rand.Next() & 1) == 0 ? -this.Template.EmissionVolume.X * 0.5f : this.Template.EmissionVolume.X * 0.5f;
                                        float oY = (this._rand.NextSingle() - this._rand.NextSingle()) * this.Template.EmissionVolume.Y;
                                        baseOffset += new Vector3(oX, oY, 0);
                                    }
                                    else
                                    {
                                        float oY = (this._rand.Next() & 1) == 0 ? -this.Template.EmissionVolume.Y * 0.5f : this.Template.EmissionVolume.Y * 0.5f;
                                        float oX = (this._rand.NextSingle() - this._rand.NextSingle()) * this.Template.EmissionVolume.X;
                                        baseOffset += new Vector3(oX, oY, 0);
                                    }
                                }
                                else
                                {
                                    if (vY > 0 && vZ > 0)
                                    {
                                        // Emitting on front face
                                        bool isLR = (this._rand.Next() & 1) == 0;
                                        if (isLR)
                                        {
                                            float oZ = (this._rand.Next() & 1) == 0 ? -this.Template.EmissionVolume.Z * 0.5f : this.Template.EmissionVolume.Z * 0.5f;
                                            float oY = (this._rand.NextSingle() - this._rand.NextSingle()) * this.Template.EmissionVolume.Y;
                                            baseOffset += new Vector3(0, oY, oZ);
                                        }
                                        else
                                        {
                                            float oZ = (this._rand.NextSingle() - this._rand.NextSingle()) * this.Template.EmissionVolume.Z;
                                            float oY = (this._rand.Next() & 1) == 0 ? -this.Template.EmissionVolume.Y * 0.5f : this.Template.EmissionVolume.Y * 0.5f;
                                            baseOffset += new Vector3(0, oY, oZ);
                                        }
                                    }
                                    else
                                    {
                                        // Emitting on side face
                                        bool isLR = (this._rand.Next() & 1) == 0;
                                        if (isLR)
                                        {
                                            float oX = (this._rand.Next() & 1) == 0 ? -this.Template.EmissionVolume.X * 0.5f : this.Template.EmissionVolume.X * 0.5f;
                                            float oZ = (this._rand.NextSingle() - this._rand.NextSingle()) * this.Template.EmissionVolume.Z;
                                            baseOffset += new Vector3(oX, 0, oZ);
                                        }
                                        else
                                        {
                                            float oZ = (this._rand.Next() & 1) == 0 ? -this.Template.EmissionVolume.Z * 0.5f : this.Template.EmissionVolume.Z * 0.5f;
                                            float oX = (this._rand.NextSingle() - this._rand.NextSingle()) * this.Template.EmissionVolume.X;
                                            baseOffset += new Vector3(oX, 0, oZ);
                                        }
                                    }
                                }

                                break;
                            }

                            case ParticleSystem.EmissionMode.Mask:
                            {
                                if (this.IsFake)
                                {
                                    goto case ParticleSystem.EmissionMode.Point;
                                }

                                if (this.Template.MaskID.IsEmpty())
                                {
                                    goto case ParticleSystem.EmissionMode.Point;
                                }

                                if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(this.Template.MaskID, AssetType.Texture, out Asset a) != AssetStatus.Return || a == null || !(a.Texture?.glReady ?? false))
                                {
                                    goto case ParticleSystem.EmissionMode.Point;
                                }

                                List<WeightedList<Vector2>> masks;
                                lock (ParticleSystem.imageEmissionLock)
                                {
                                    if (!ParticleSystem.ImageEmissionLocations.TryGetValue(this.Template.MaskID, out masks))
                                    {
                                        Image<Rgba32> img = a.Texture.CompoundImage();
                                        TextureAnimation anima = a.Texture.CachedAnimation ?? new TextureAnimation(new TextureAnimation.Frame[] {
                                            new TextureAnimation.Frame(){ Index = 0, Duration = 1, Location = new RectangleF(0, 0, 1, 1) }
                                        });

                                        masks = this.AnalyzeImage(img, anima);
                                        img.Dispose();
                                    }
                                }

                                if (masks.Count == 0)
                                {
                                    goto case ParticleSystem.EmissionMode.Point;
                                }

                                TextureAnimation anim = a?.Texture?.CachedAnimation;
                                uint maskIndex = anim?.FindFrameForIndex(double.NaN)?.Index ?? 0u;
                                Vector2 vec = masks[(int)maskIndex].GetRandomItem(this._rand).Item;
                                float vX = this.Template.EmissionVolume.X;
                                float vY = this.Template.EmissionVolume.Y;
                                float vZ = this.Template.EmissionVolume.Z;
                                Vector3 rPt = Vector3.Zero;
                                if (vX > 0 && vY > 0)
                                {
                                    rPt += new Vector3((vec.X - 0.5f) * vX, (vec.Y - 0.5f) * vY, 0);
                                }
                                else
                                {
                                    if (vY > 0 && vZ > 0)
                                    {
                                        rPt += new Vector3(0, (vec.Y - 0.5f) * vY, (vec.X - 0.5f) * vZ);
                                    }
                                    else
                                    {
                                        rPt += new Vector3((vec.X - 0.5f) * vX, 0, (vec.Y - 0.5f) * vZ);
                                    }
                                }

                                if (this.Container.UseContainerOrientation)
                                {
                                    rPt = this.Container.IsFXEmitter ? rPt : Vector4.Transform(new Vector4(rPt, 1.0f), this.Container.Container.Rotation).Xyz();
                                    rPt *= this.Container.IsFXEmitter ? Vector3.One : this.Container.Container.Scale;
                                }

                                baseOffset += rPt;

                                break;
                            }

                            case ParticleSystem.EmissionMode.MeshSurface:
                            case ParticleSystem.EmissionMode.Volume:
                            {
                                if (this.IsFake)
                                {
                                    goto case ParticleSystem.EmissionMode.Point;
                                }

                                if (Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(this.Container.Container.AssetID, AssetType.Model, out Asset a) != AssetStatus.Return || a == null || !a.ModelGlReady)
                                {
                                    goto case ParticleSystem.EmissionMode.Point;
                                }

                                if (this.Container.IsFXEmitter)
                                {
                                    goto case ParticleSystem.EmissionMode.Point;
                                }

                                this._meshRefs.Clear();
                                if (!string.IsNullOrEmpty(this.Container.AttachmentPoint))
                                {
                                    GlbObject o = a.Model.GLMdl.Meshes.Find(p => p.Name.Equals(this.Container.AttachmentPoint));
                                    if (o != null)
                                    {
                                        foreach (GlbMesh m in o.Meshes)
                                        {
                                            if (m.simplifiedTriangles != null && m.simplifiedTriangles.Length > 0)
                                            {
                                                this._meshRefs.Add(new WeightedItem<GlbMesh>(m, (int)MathF.Ceiling(m.areaSums[^1])));
                                            }
                                        }
                                    }
                                }

                                if (this._meshRefs.Count == 0)
                                {
                                    foreach (GlbMesh m in a.Model.GLMdl.Meshes.SelectMany(o => o.Meshes))
                                    {
                                        if (m.simplifiedTriangles != null && m.simplifiedTriangles.Length > 0)
                                        {
                                            this._meshRefs.Add(new WeightedItem<GlbMesh>(m, (int)MathF.Ceiling(m.areaSums[^1])));
                                        }
                                    }
                                }

                                if (this._meshRefs.Count > 0)
                                {
                                    GlbMesh sMesh = WeightedRandom.GetWeightedItem(this._meshRefs, this._rand).Item;
                                    float totalArea = sMesh.areaSums[^1];
                                    float rArea = this._rand.NextSingle() * totalArea;
                                    //int rIdx = this._rand.Next(sMesh.simplifiedTriangles.Length / 3) * 3;
                                    int rIdx = sMesh.FindAreaSumIndex(rArea);
                                    rIdx *= 3;
                                    Vector3 v1 = sMesh.simplifiedTriangles[rIdx + 0];
                                    Vector3 v2 = sMesh.simplifiedTriangles[rIdx + 1];
                                    Vector3 v3 = sMesh.simplifiedTriangles[rIdx + 2];
                                    float r1 = MathF.Sqrt(this._rand.NextSingle());
                                    float r2 = this._rand.NextSingle();
                                    Vector3 rPt = (((1 - r1) * v1) + (r1 * (1 - r2) * v2) + (r2 * r1 * v3));
                                    if (this.Container.UseContainerOrientation)
                                    {
                                        if (a.Model.GLMdl.IsAnimated && sMesh.IsAnimated && sMesh.boneData.Length > 0)
                                        {
                                            GlbAnimation anim = this.Container.Container.AnimationContainer.CurrentAnimation;
                                            if (anim != null)
                                            {
                                                double time = this.Container.Container.AnimationContainer.GetTime(0);
                                                GlbMesh.BoneData bd1 = sMesh.boneData[rIdx + 0];
                                                GlbMesh.BoneData bd2 = sMesh.boneData[rIdx + 1];
                                                GlbMesh.BoneData bd3 = sMesh.boneData[rIdx + 2];
                                                float inf1 = 1 - r1;
                                                float inf2 = r1 * (1 - r2);
                                                float inf3 = r2 * r1;
                                                GlbMesh.BoneData transformData = inf1 > inf2 && inf1 > inf3 ? bd1 : inf2 > inf3 && inf2 > inf1 ? bd2 : bd3;
                                                lock (this.Container.Container.AnimationContainer.animationStorageLock)
                                                {
                                                    uint mx = Math.Max(transformData.index0, Math.Max(transformData.index1, Math.Max(transformData.index2, transformData.index3)));
                                                    if (mx < this.Container.Container.AnimationContainer.StoredBoneData.Count)
                                                    {
                                                        Matrix4x4 m0 = this.Container.Container.AnimationContainer.StoredBoneData[(int)transformData.index0].Transform * transformData.weight1;
                                                        Matrix4x4 m1 = this.Container.Container.AnimationContainer.StoredBoneData[(int)transformData.index1].Transform * transformData.weight2;
                                                        Matrix4x4 m2 = this.Container.Container.AnimationContainer.StoredBoneData[(int)transformData.index2].Transform * transformData.weight3;
                                                        Matrix4x4 m3 = this.Container.Container.AnimationContainer.StoredBoneData[(int)transformData.index3].Transform * transformData.weight4;
                                                        Matrix4x4 mf = m0 + m1 + m2 + m3;
                                                        rPt = Vector4.Transform(new Vector4(rPt, 1.0f), mf).Xyz();
                                                    }
                                                }
                                            }
                                        }

                                        rPt = Vector4.Transform(new Vector4(rPt, 1.0f), this.Container.Container.Rotation).Xyz();
                                        rPt *= this.Container.Container.Scale;
                                    }

                                    baseOffset += rPt;
                                }

                                break;
                            }

                            case ParticleSystem.EmissionMode.Point:
                            default:
                            {
                                break;
                            }
                        }

                        emissionPoint = baseOffset;
                    }

                    if (this.Template.ClusterEmission && i != 0)
                    {
                        baseOffset = emissionPoint;
                    }

                    p->worldPosition = baseOffset;
                    p->velocity = this.Template.InitialVelocity.Value(this._rand.NextSingle(), this._rand.NextSingle(), this._rand.NextSingle());
                    if (this.Template.InitialVelocityRandomAngle > float.Epsilon)
                    {
                        Vector3 rndUnitVector = new Vector3(
                                (this._rand.NextSingle() - this._rand.NextSingle()),
                                (this._rand.NextSingle() - this._rand.NextSingle()),
                                (this._rand.NextSingle() - this._rand.NextSingle())).Normalized();
                        Quaternion rndUnitQuaternion = Quaternion.CreateFromAxisAngle(rndUnitVector, this.Template.InitialVelocityRandomAngle * this._rand.NextSingle());
                        Vector4 v = Vector4.Transform(new Vector4(p->velocity, 1.0f), rndUnitQuaternion);
                        p->velocity = v.Xyz() / v.W;
                    }

                    if (!this.IsFake)
                    {
                        if (this.Container.RotateVelocityByOrientation && !this.Container.IsFXEmitter && this.Container?.Container != null)
                        {
                            Quaternion cRot = this.Container.Container.Rotation;
                            Vector4 v = Vector4.Transform(new Vector4(p->velocity, 1.0f), cRot);
                            p->velocity = v.Xyz() / v.W;
                        }
                    }
                }
            }
        }

        private List<WeightedList<Vector2>> AnalyzeImage(Image<Rgba32> image, TextureAnimation animation)
        {
            List<WeightedList<Vector2>> apts = new List<WeightedList<Vector2>>();
            for (int i = 0; i < animation.Frames.Length; ++i)
            {
                WeightedList<Vector2> pts = new WeightedList<Vector2>();
                TextureAnimation.Frame frame = animation.Frames[i];
                int sX = (int)(frame.Location.X * image.Width);
                int sY = (int)(frame.Location.Y * image.Height);
                int fw = (int)(frame.Location.Width * image.Width);
                int fh = (int)(frame.Location.Height * image.Height);
                int eX = fw + sX;
                int eY = fh + sY;
                for (int y = sY; y < eY; ++y)
                {
                    for (int x = sX; x < eX; ++x)
                    {
                        Rgba32 pixel = image[x, y];
                        if (pixel.R > 0 && pixel.G > 0 && pixel.B > 0)
                        {
                            int w = (int)((pixel.R + pixel.G + pixel.B) / 3f);
                            if (w > 0)
                            {
                                pts.Add(new WeightedItem<Vector2>(new Vector2((x - sX) / (float)fw, 1.0f - ((y - sY) / (float)fh)), w));
                            }
                        }
                    }
                }

                apts.Add(pts);
            }

            ParticleSystem.ImageEmissionLocations[this.Template.MaskID] = apts;
            return apts;
        }

        public void UpdateBufferState()
        {
            if (!this._glInit)
            {
                this._glTextureBuffer = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.Texture, this._glTextureBuffer);
                GL.BufferData(BufferTarget.Texture, this._sizeInBytes, IntPtr.Zero, BufferUsage.DynamicDraw);

                this._glBufferTexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Buffer, this._glBufferTexture);
                GL.TexBuffer(SizedInternalFormat.RgbaFloat, this._glTextureBuffer);

                this._glInit = true;
            }

            GL.BindBuffer(BufferTarget.Texture, this._glTextureBuffer);
            GL.BufferSubData(BufferTarget.Texture, IntPtr.Zero, this._sizeInBytes, (IntPtr)this._buffer);
        }

        public Particle* FindFirstDeactivatedParticle()
        {
            int initial = this._lastParticleIndex;
            while (true)
            {
                if (this._lastParticleIndex >= this._numParticles)
                {
                    this._lastParticleIndex = 0;
                    return this._allParticles;
                }

                int pi = this._lastParticleIndex;
                ++this._lastParticleIndex;
                if (this._lastParticleIndex == this._numParticles)
                {
                    this._lastParticleIndex = 0;
                }

                if (this._lastParticleIndex == initial) // Could not find particle
                {
                    return this._allParticles + pi;
                }

                if (this._allParticles[pi].active == 0)
                {
                    return this._allParticles + pi;
                }
            }
        }

        public void Free()
        {
            GL.DeleteBuffer(this._glTextureBuffer);
            GL.DeleteTexture(this._glBufferTexture);

            if (this._buffer != null)
            {
                MemoryHelper.Free(this._buffer);
                this._buffer = null;
            }

            if (this._allParticles != null)
            {
                MemoryHelper.Free(this._allParticles);
                this._allParticles = null;
            }

            this._glInit = false;
            this._lastParticleIndex = 0;
        }
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct GLParticleData
    {
        [FieldOffset(0)]
        public Vector3 worldPosition;
        [FieldOffset(12)]
        public float scale;

        [FieldOffset(16)]
        public float color;
        [FieldOffset(20)]
        public float animationFrame;
        [FieldOffset(24)]
        public float spritemapIndex;
        [FieldOffset(28)]
        public float lifespan;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct Particle
    {
        [FieldOffset(0)]
        public Vector4 color;

        [FieldOffset(16)]
        public Vector3 worldPosition;

        [FieldOffset(28)]
        public Vector3 velocity;

        [FieldOffset(40)]
        public int lifespan;

        [FieldOffset(44)]
        public int age;

        [FieldOffset(48)]
        public float scale;

        [FieldOffset(52)]
        public float scaleMod;

        [FieldOffset(56)]
        public int active;

        [FieldOffset(60)]
        public int spriteIndex;

        public void Update(ParticleSystemInstance instance)
        {
            if (this.active == 0)
            {
                return;
            }

            if (++this.age >= this.lifespan)
            {
                this.active = 0;
                return;
            }

            // Update position + velocity
            this.worldPosition += this.velocity;
            this.velocity *= instance.Template.VelocityDampenFactor;
            this.velocity += instance.Template.Gravity;
            float a = (float)this.age / this.lifespan;

            this.color = instance.Template.ColorOverLifetime.Interpolate(a, GradientInterpolators.LerpVec4);
            this.scale = instance.Template.ScaleOverLifetime.Interpolate(a, GradientInterpolators.Lerp) * this.scaleMod;
            if (instance.Template.SpriteSheetIsAnimation)
            {
                this.spriteIndex = (int)MathF.Floor(a * instance.Template.SpriteData.NumSprites);
            }
        }
    }
}
