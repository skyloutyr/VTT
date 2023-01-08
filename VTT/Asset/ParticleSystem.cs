namespace VTT.Asset
{
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using VTT.Asset.Glb;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;

    public class ParticleSystem
    {
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

        public RangeVector3 InitialVelocity { get; set; } = new RangeVector3(Vector3.Zero, Vector3.Zero);
        public float InitialVelocityRandomAngle { get; set; } = 0f;
        public Vector3 Gravity { get; set; } = new Vector3(0, 0, -1 / 60.0f);
        public float VelocityDampenFactor { get; set; } = 0.98f;

        public Gradient<Vector4> ColorOverLifetime { get; set; } = new Gradient<Vector4>() { [0] = Vector4.One, [1] = new Vector4(1, 1, 1, 0) };
        public Gradient<float> ScaleOverLifetime { get; set; } = new Gradient<float>() { [0] = 1f, [1] = 1f };

        public Guid AssetID { get; set; }

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
        }

        public void WriteV2(BinaryWriter bw)
        {
            DataElement ret = new DataElement();
            ret.SetEnum("EmissionType", this.EmissionType);
            ret.Set("EmissionRadius", this.EmissionRadius);
            ret.SetVec3("EmissionVolume", this.EmissionVolume);
            ret.Set("EmissionChance", this.EmissionChance);
            ret.Set("EmissionAmountLo", this.EmissionAmount.Min);
            ret.Set("EmissionAmountHi", this.EmissionAmount.Max);
            ret.Set("EmissionCooldownLo", this.EmissionCooldown.Min);
            ret.Set("EmissionCooldownHi", this.EmissionCooldown.Max);
            ret.Set("LifetimeLo", this.Lifetime.Min);
            ret.Set("LifetimeHi", this.Lifetime.Max);
            ret.Set("ScaleVariationLo", this.ScaleVariation.Min);
            ret.Set("ScaleVariationHi", this.ScaleVariation.Max);
            ret.Set("MaxParticles", this.MaxParticles);
            ret.SetVec3("InitialVelocityLo", this.InitialVelocity.Min);
            ret.SetVec3("InitialVelocityHi", this.InitialVelocity.Max);
            ret.Set("InitialVelocityRandomAngle", this.InitialVelocityRandomAngle);
            ret.SetVec3("Gravity", this.Gravity);
            ret.Set("VelocityDampenFactor", this.VelocityDampenFactor);
            ret.SetArray("ColorOverLifetime", this.ColorOverLifetime.Select(x =>
            {
                DataElement ret = new DataElement();
                ret.Set("k", x.Key);
                ret.SetVec4("v", x.Value);
                return ret;
            }).ToArray(), (n, c, e) => c.Set(n, e));

            ret.SetArray("ScaleOverLifetime", this.ScaleOverLifetime.Select(x =>
            {
                DataElement ret = new DataElement();
                ret.Set("k", x.Key);
                ret.Set("v", x.Value);
                return ret;
            }).ToArray(), (n, c, e) => c.Set(n, e));

            ret.SetGuid("AssetID", this.AssetID);
            ret.Set("DoBillboard", this.DoBillboard);
            ret.Write(bw);
        }

        public void ReadV2(BinaryReader br)
        {
            DataElement de = new DataElement(br);
            this.EmissionType = de.GetEnum<EmissionMode>("EmissionType");
            this.EmissionRadius = de.Get<float>("EmissionRadius");
            this.EmissionVolume = de.GetVec3("EmissionVolume");
            this.EmissionChance = de.Get<float>("EmissionChance");
            this.EmissionAmount = new RangeInt(de.Get<int>("EmissionAmountLo"), de.Get<int>("EmissionAmountHi"));
            this.EmissionCooldown = new RangeInt(de.Get<int>("EmissionCooldownLo"), de.Get<int>("EmissionCooldownHi"));
            this.Lifetime = new RangeInt(de.Get<int>("LifetimeLo"), de.Get<int>("LifetimeHi"));
            this.ScaleVariation = new RangeSingle(de.Get<float>("ScaleVariationLo"), de.Get<float>("ScaleVariationHi"));
            this.MaxParticles = de.Get<int>("MaxParticles");
            this.InitialVelocity = new RangeVector3(de.GetVec3("InitialVelocityLo"), de.GetVec3("InitialVelocityHi"));
            this.InitialVelocityRandomAngle = de.Get<float>("InitialVelocityRandomAngle");
            this.Gravity = de.GetVec3("Gravity");
            this.VelocityDampenFactor = de.Get<float>("VelocityDampenFactor");
            DataElement[] col = de.GetArray("ColorOverLifetime", (n, c) => c.Get<DataElement>(n), Array.Empty<DataElement>());
            this.ColorOverLifetime.Clear();
            foreach (DataElement e in col)
            {
                this.ColorOverLifetime[e.Get<float>("k")] = e.GetVec4("v");
            }

            col = de.GetArray("ScaleOverLifetime", (n, c) => c.Get<DataElement>(n), Array.Empty<DataElement>());
            this.ScaleOverLifetime.Clear();
            foreach (DataElement e in col)
            {
                this.ScaleOverLifetime[e.Get<float>("k")] = e.Get<float>("v");
            }

            this.AssetID = de.GetGuid("AssetID");
            this.DoBillboard = de.Get<bool>("DoBillboard", true);
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
            this.DoBillboard = true;
        }

        public ParticleSystem Copy() => new ParticleSystem() 
        { 
            EmissionType = this.EmissionType,
            EmissionRadius = this.EmissionRadius,
            EmissionVolume = new Vector3(this.EmissionVolume),
            EmissionChance = this.EmissionChance,
            EmissionAmount = new RangeInt(this.EmissionAmount.Min, this.EmissionAmount.Max),
            EmissionCooldown = new RangeInt(this.EmissionCooldown.Min, this.EmissionCooldown.Max),
            Lifetime = new RangeInt(this.Lifetime.Min, this.Lifetime.Max),
            ScaleVariation = new RangeSingle(this.ScaleVariation.Min, this.ScaleVariation.Max),
            MaxParticles = this.MaxParticles,
            InitialVelocity = new RangeVector3(new Vector3(this.InitialVelocity.Min), new Vector3(this.InitialVelocity.Max)),
            InitialVelocityRandomAngle = this.InitialVelocityRandomAngle,
            Gravity = new Vector3(this.Gravity),
            VelocityDampenFactor = this.VelocityDampenFactor,
            ColorOverLifetime = new Gradient<Vector4>(this.ColorOverLifetime),
            ScaleOverLifetime = new Gradient<float>(this.ScaleOverLifetime),
            AssetID = this.AssetID,
            DoBillboard = this.DoBillboard
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

        public enum EmissionMode
        {
            Point,
            Sphere,
            SphereSurface,
            Cube,
            CubeSurface,
            Volume,
            MeshSurface
        }
    }

    public unsafe sealed class ParticleSystemInstance : IDisposable
    {
        public ParticleSystem Template { get; set; }
        public ParticleContainer Container { get; set; }
        public Particle[] AllParticles { get; set; }
        public bool IsFake { get; set; }

        private bool _glInit;
        private uint _glBufferTexture;
        private uint _glTextureBuffer;
        private int _lastParticleIndex;
        private int _sizeInBytes;
        private int _emissionCd;
        private uint _frameAmount;
        private readonly Random _rand;

        private float* _buffer;
        private GCHandle _bufferPtr;
        private readonly List<Particle> _sortedParticles = new List<Particle>();

        public ParticleSystemInstance(ParticleSystem template, ParticleContainer container)
        {
            this.Template = template;
            this.Container = container;
            this.IsFake = container == null;
            this._rand = new Random();
            this.Resize();
        }

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

            this.Dispose();
            this.AllParticles = new Particle[numPossibleParticles];
            this._sizeInBytes = numPossibleParticles * 6 * sizeof(float);
            this._bufferPtr = GCHandle.Alloc(new float[numPossibleParticles * 6], GCHandleType.Pinned);
            this._buffer = (float*)this._bufferPtr.AddrOfPinnedObject();
            for (int i = 0; i < this.AllParticles.Length; ++i)
            {
                this.AllParticles[i] = new Particle() { Active = false, Age = 0, Color = Vector4.Zero, Lifespan = 0, Owner = this, Velocity = Vector3.Zero, WorldPosition = Vector3.Zero };
            }

            this._sortedParticles.Clear();
            this._sortedParticles.AddRange(this.AllParticles);
        }

        public void Render(ShaderProgram particleShader, Vector3 cameraPosition, Camera cam)
        {
            if (!this._glInit || this.Template.AssetID.Equals(Guid.Empty))
            {
                return;
            }

            if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(this.Template.AssetID, AssetType.Model, out Asset a) != AssetStatus.Return || a == null || !(a?.Model?.GLMdl?.glReady ?? false))
            {
                return;
            }

            particleShader["billboard"].Set(this.Template.DoBillboard);
            this._frameAmount = (uint)a.Model.GLMdl.Materials.Max(m => m.BaseColorAnimation.Frames.Length);
            GL.ActiveTexture(TextureUnit.Texture14);
            GL.BindTexture(TextureTarget.TextureBuffer, this._glBufferTexture);
            GL.ActiveTexture(TextureUnit.Texture0);
            a.Model.GLMdl.Render(particleShader, Matrix4.Identity, cam.Projection, cam.View, 0, m => GL.DrawElementsInstanced(PrimitiveType.Triangles, m.AmountToRender, DrawElementsType.UnsignedInt, IntPtr.Zero, this.AllParticles.Length));
        }

        private readonly List<WeightedItem<GlbMesh>> _meshRefs = new List<WeightedItem<GlbMesh>>();
        public void Update(Vector3 cameraPosition)
        {
            int nActive = 0;
            int bIdx;
            for (int i = 0; i < this.AllParticles.Length; i++)
            {
                Particle p = this.AllParticles[i];
                p.Update();
            }

            if (this.IsFake)
            {
                this._sortedParticles.Sort((l, r) =>
                {
                    float dL = (l.WorldPosition - cameraPosition).LengthSquared;
                    float dR = (r.WorldPosition - cameraPosition).LengthSquared;
                    return dR.CompareTo(dL);
                });
            }

            for (int i = 0; i < this._sortedParticles.Count; ++i)
            {
                Particle p = this._sortedParticles[i];
                bIdx = i * 6;
                this._buffer[bIdx + 0] = p.WorldPosition.X;
                this._buffer[bIdx + 1] = p.WorldPosition.Y;
                this._buffer[bIdx + 2] = p.WorldPosition.Z;
                this._buffer[bIdx + 3] = p.Active ? p.Scale : 0.0f;
                this._buffer[bIdx + 4] = VTTMath.UInt32BitsToSingle(Extensions.Rgba(p.Color));
                this._buffer[bIdx + 5] = VTTMath.UInt32BitsToSingle(p.Lifespan == 0 ? 0 : (uint)(this._frameAmount * ((float)p.Age / p.Lifespan)));
                if (p.Active)
                {
                    ++nActive;
                }
            }

            if (this._emissionCd-- <= 0 && (this.Template.EmissionChance >= 1.0f - float.Epsilon || this._rand.NextDouble() <= this.Template.EmissionChance))
            {
                this._emissionCd = this.Template.EmissionCooldown.Value(this._rand.NextSingle());
                int nNew = this.Template.EmissionAmount.Value(this._rand.NextSingle());
                for (int i = 0; i < nNew; ++i)
                {
                    Particle p = this.FindFirstDeactivatedParticle();
                    p.Active = true;
                    p.Age = 0;
                    p.Color = this.Template.ColorOverLifetime.Interpolate(0, GradientInterpolators.LerpVec4);
                    p.Lifespan = this.Template.Lifetime.Value(this._rand.NextSingle());
                    p.ScaleMod = this.Template.ScaleVariation.Value(this._rand.NextSingle());
                    p.Scale = this.Template.ScaleOverLifetime.Interpolate(0, GradientInterpolators.Lerp);
                    Vector3 baseOffset;
                    if (this.IsFake)
                    {
                        baseOffset = Vector3.Zero;
                    }
                    else
                    {
                        Vector4 bo4 = new Vector4(this.Container.ContainerPositionOffset, 1.0f);
                        Quaternion q = this.Container.UseContainerOrientation ? this.Container.Container.Rotation : Quaternion.Identity;
                        bo4 = q * bo4;
                        baseOffset = bo4.Xyz / bo4.W;
                        baseOffset *= this.Container.Container.Scale;
                        baseOffset += this.Container.Container.Position;
                    }

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

                        case ParticleSystem.EmissionMode.MeshSurface:
                        case ParticleSystem.EmissionMode.Volume:
                        {
                            if (this.IsFake)
                            {
                                goto case ParticleSystem.EmissionMode.Point;
                            }

                            if (Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(this.Container.Container.AssetID, AssetType.Model, out Asset a) != AssetStatus.Return || a == null || !(a?.Model?.GLMdl?.glReady ?? false))
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
                                int rIdx = this._rand.Next(sMesh.simplifiedTriangles.Length / 3) * 3;
                                System.Numerics.Vector3 v1 = sMesh.simplifiedTriangles[rIdx + 0];
                                System.Numerics.Vector3 v2 = sMesh.simplifiedTriangles[rIdx + 1];
                                System.Numerics.Vector3 v3 = sMesh.simplifiedTriangles[rIdx + 2];
                                float r1 = MathF.Sqrt(this._rand.NextSingle());
                                float r2 = this._rand.NextSingle();
                                Vector3 rPt = (((1 - r1) * v1) + (r1 * (1 - r2) * v2) + (r2 * r1 * v3)).GLVector();
                                if (this.Container.UseContainerOrientation)
                                {
                                    rPt = (this.Container.Container.Rotation * new Vector4(rPt, 1.0f)).Xyz;
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

                    p.WorldPosition = baseOffset;
                    p.Velocity = this.Template.InitialVelocity.Value(this._rand.NextSingle(), this._rand.NextSingle(), this._rand.NextSingle());
                    if (this.Template.InitialVelocityRandomAngle > float.Epsilon)
                    {
                        Vector3 rndUnitVector = new Vector3(
                                (this._rand.NextSingle() - this._rand.NextSingle()),
                                (this._rand.NextSingle() - this._rand.NextSingle()),
                                (this._rand.NextSingle() - this._rand.NextSingle())).Normalized();
                        Quaternion rndUnitQuaternion = Quaternion.FromAxisAngle(rndUnitVector, this.Template.InitialVelocityRandomAngle * this._rand.NextSingle());
                        Vector4 v = (rndUnitQuaternion * new Vector4(p.Velocity, 1.0f));
                        p.Velocity = v.Xyz / v.W;
                    }
                }
            }
        }

        public void UpdateBufferState()
        {
            if (!this._glInit)
            {
                GL.GenBuffers(1, out this._glTextureBuffer);
                GL.BindBuffer(BufferTarget.TextureBuffer, this._glTextureBuffer);
                GL.BufferData(BufferTarget.TextureBuffer, this._sizeInBytes, IntPtr.Zero, BufferUsageHint.DynamicDraw);

                GL.GenTextures(1, out this._glBufferTexture);
                GL.BindTexture(TextureTarget.TextureBuffer, this._glBufferTexture);
                GL.TexBuffer(TextureBufferTarget.TextureBuffer, (SizedInternalFormat)Version30.Rgb32f, this._glTextureBuffer);

                this._glInit = true;
            }

            GL.BindBuffer(BufferTarget.TextureBuffer, this._glTextureBuffer);
            GL.BufferSubData(BufferTarget.TextureBuffer, IntPtr.Zero, this._sizeInBytes, (IntPtr)this._buffer);
        }

        public Particle FindFirstDeactivatedParticle()
        {
            int initial = this._lastParticleIndex;
            while (true)
            {
                if (this._lastParticleIndex >= this.AllParticles.Length)
                {
                    this._lastParticleIndex = 0;
                    return this.AllParticles[0];
                }

                Particle p = this.AllParticles[this._lastParticleIndex];
                ++this._lastParticleIndex;
                if (this._lastParticleIndex == this.AllParticles.Length)
                {
                    this._lastParticleIndex = 0;
                }

                if (this._lastParticleIndex == initial) // Could not find particle
                {
                    return p;
                }

                if (!p.Active)
                {
                    return p;
                }
            }
        }

        public void Dispose()
        {
            GL.DeleteBuffer(this._glTextureBuffer);
            GL.DeleteTexture(this._glBufferTexture);

            if (this._bufferPtr.IsAllocated)
            {
                this._bufferPtr.Free();
                this._buffer = (float*)IntPtr.Zero;
            }

            this._glInit = false;
        }
    }

    public class Particle
    {
        public Vector3 WorldPosition { get; set; }
        public Vector3 Velocity { get; set; }
        public int Lifespan { get; set; }
        public int Age { get; set; }
        public Vector4 Color { get; set; }
        public float Scale { get; set; }
        public float ScaleMod { get; set; }

        public bool Active { get; set; }
        public ParticleSystemInstance Owner { get; set; }

        public void Update()
        {
            if (!this.Active)
            {
                return;
            }

            if (++this.Age >= this.Lifespan)
            {
                this.Active = false;
                return;
            }

            // Update position + velocity
            this.WorldPosition += this.Velocity;
            this.Velocity *= this.Owner.Template.VelocityDampenFactor;
            this.Velocity += this.Owner.Template.Gravity;
            float a = (float)this.Age / this.Lifespan;

            this.Color = this.Owner.Template.ColorOverLifetime.Interpolate(a, GradientInterpolators.LerpVec4);
            this.Scale = this.Owner.Template.ScaleOverLifetime.Interpolate(a, GradientInterpolators.Lerp) * this.ScaleMod;
        }
    }
}
