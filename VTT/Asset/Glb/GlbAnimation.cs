namespace VTT.Asset.Glb
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;

    public class GlbAnimation
    {
        public string Name { get; set; }
        public float Duration { get; set; }
        public List<Channel> Channels { get; } = new List<Channel>();
        public List<Sampler> Samplers { get; } = new List<Sampler>();
        public GlbScene Container { get; internal set; }

        public void ProvideTransforms(GlbArmature skeleton, float time)
        {
            foreach (Channel c in this.Channels)
            {
                if (skeleton.BonesByModelIndex.TryGetValue(c.BoneIndex, out GlbBone bone))
                {
                    switch (c.Path)
                    {
                        case Path.Translation:
                        {
                            bone.CachedLocalTranslation = c.Sampler.GetValue(time, false).Xyz;
                            break;
                        }

                        case Path.Scale:
                        {
                            bone.CachedLocalScale = c.Sampler.GetValue(time, false).Xyz;
                            break;
                        }

                        case Path.Rotation:
                        {
                            Vector4 val = c.Sampler.GetValue(time, true);
                            bone.CachedLocalRotation = new Quaternion(val.X, val.Y, val.Z, val.W);
                            break;
                        }
                    }
                }
            }
        }

        public enum Path
        {
            Translation,
            Rotation,
            Scale
        }

        public enum Interpolation
        {
            Linear,
            Step,
            CubicSpline
        }

        public class Channel
        {
            public Path Path { get; set; }
            public Sampler Sampler { get; set; }
            public int BoneIndex { get; set; }
        }

        public class Sampler
        {
            public float[] Timestamps { get; set; }
            public Vector4[] Values { get; set; }
            public Interpolation Interpolation { get; set; }

            public Vector4 GetValue(float time, bool isQuaternion)
            {
                if (time < this.Timestamps[0])
                {
                    return this.Values[0];
                }

                if (time >= this.Timestamps[^1])
                {
                    return this.Values[^1];
                }

                if (this.Timestamps.Length == 1) // Only one keyframe provided
                {
                    return this.Values[0];
                }

                static Quaternion QFromV4(Vector4 vec) => new Quaternion(vec.X, vec.Y, vec.Z, vec.W);
                static Vector4 V4FromQ(Quaternion q) => new Vector4(q.X, q.Y, q.Z, q.W);

                for (int i = 0; i < this.Timestamps.Length; ++i)
                {
                    float ts = this.Timestamps[i];
                    if (time < ts)
                    {
                        float tl = this.Timestamps[i - 1];
                        float a = (time - tl) / (ts - tl);
                        Vector4 l = this.Values[i - 1];
                        Vector4 r = this.Values[i];
                        switch (this.Interpolation)
                        {
                            case Interpolation.CubicSpline:
                            case Interpolation.Linear:
                            {
                                return isQuaternion ? V4FromQ(Quaternion.Normalize(Quaternion.Slerp(QFromV4(l), QFromV4(r), a))) : Vector4.Lerp(l, r, a);
                            }

                            case Interpolation.Step:
                            {
                                float tdl = MathF.Abs(tl - time);
                                float tdr = ts - time;
                                if (tdl < tdr) // Closer to left
                                {
                                    return l;
                                }

                                return r;
                            }

                            default:
                            {
                                return l;
                            }
                        }
                    }
                }

                // Can't get here
                throw new Exception("Animation provided bad keyframe data!");
            }
        }
    }

    public class GlbBone
    {
        public GlbBone Parent { get; set; }
        public GlbBone[] Children { get; set; }
        public int ModelIndex { get; set; }
        public Matrix4 InverseBindMatrix { get; set; }
        public Matrix4 InverseWorldTransform { get; set; }

        public Vector3 CachedLocalTranslation { get; set; } = Vector3.Zero;
        public Vector3 CachedLocalScale { get; set; } = Vector3.One;
        public Quaternion CachedLocalRotation { get; set; } = Quaternion.Identity;
        public Matrix4 CachedGlobalTransform { get; set; }
        public Matrix4 Transform { get; set; }

        public Matrix4 CalculateGlobalTransform() => this.InverseBindMatrix * this.CachedGlobalTransform;
        public void ResetTransforms()
        {
            this.CachedLocalTranslation = Vector3.Zero;
            this.CachedLocalScale = Vector3.One;
            this.CachedLocalRotation = Quaternion.Identity;
        }
        
    }

    public class GlbArmature
    {
        public List<GlbBone> UnsortedBones { get; } = new List<GlbBone>();
        public List<GlbBone> SortedBones { get; } = new List<GlbBone>();
        public List<GlbBone> Root { get; } = new List<GlbBone>();
        public Dictionary<int, GlbBone> BonesByModelIndex { get; } = new Dictionary<int, GlbBone>();

        public void CalculateAllTransforms(GlbAnimation animation, float time)
        {
            void UpdateBone(GlbBone bone)
            {
                GlbBone parent = bone.Parent;
                if (parent != null)
                {
                    bone.CachedGlobalTransform = bone.CachedGlobalTransform * parent.CachedGlobalTransform;
                }

                foreach (GlbBone child in bone.Children)
                {
                    UpdateBone(child);
                }
            }

            animation.ProvideTransforms(this, time);
            foreach (GlbBone bone in this.SortedBones)
            {
                // Calculate animation data (local translation, local scale, local rotation, global transform)
                bone.CachedGlobalTransform = Matrix4.CreateScale(bone.CachedLocalScale) * Matrix4.CreateFromQuaternion(bone.CachedLocalRotation) * Matrix4.CreateTranslation(bone.CachedLocalTranslation);
                if (bone.Parent != null)
                {
                    bone.CachedGlobalTransform = bone.CachedGlobalTransform * bone.Parent.CachedGlobalTransform;
                }

                bone.Transform = bone.CalculateGlobalTransform();
            }

            /*
            foreach (GlbBone bone in this.Root)
            {
                UpdateBone(bone);
            }

            foreach (GlbBone bone in this.SortedBones)
            {
                bone.Transform = bone.CalculateGlobalTransform();
            }
            */
        }

        public void ResetAllBones()
        {
            foreach (GlbBone b in this.UnsortedBones)
            {
                b.ResetTransforms();
            }
        }
    }
}
