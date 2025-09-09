namespace VTT.Render.Shaders.UBO
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using VTT.Asset.Glb;
    using VTT.Control;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Util;

    public class UniformBufferBones : UniformBufferObject<Matrix4x4>
    {
        public const nuint MaxBoneCapacity = 256;
        public bool UseMapBuffer => Client.Instance.Settings.EnableUBODMA;

        public UniformBufferBones() : base(2, MaxBoneCapacity)
        {
            this.BindAsUniformBuffer();
            OpenGLUtil.NameObject(GLObjectType.Buffer, this._buffer, "Bones UBO");
        }

        public unsafe void LoadAll(IAnimationStorage armature)
        {
            IEnumerable<IAnimationStorage.BoneData> bones = armature.ProvideBones();
            int i = 0;
            this.BindAsGLObject();
            if (UseMapBuffer)
            {
                Matrix4x4* gpuMem = (Matrix4x4*)GL.MapBufferRange(BufferTarget.Uniform, 0, (nint)this.BufferPhysicalSize, BufferRangeAccessMask.Write | BufferRangeAccessMask.InvalidateBuffer | BufferRangeAccessMask.InvalidateRange | BufferRangeAccessMask.Unsynchronized);
                foreach (IAnimationStorage.BoneData bone in bones)
                {
                    gpuMem[i++] = bone.Transform;
                }

                if (GL.UnmapBuffer(BufferTarget.Uniform))
                {
                    return; // Goto normal SubData method if data got corrupted for some reason (must do so by spec!)
                }
                else
                {
                    // Spec states that buffer content must be reinitialized if we failed to do a dma. subdata would reinitialize it in theory, but w/e
                    GL.BufferData(BufferTarget.Uniform, (int)this.BufferPhysicalSize, 0, BufferUsage.StreamDraw);
                    Client.Instance.Logger.Log(LogLevel.Warn, "Bones uniform buffer object experienced a DMA failure. Slower path will be taken!");
                }
            }

            foreach (IAnimationStorage.BoneData bone in bones)
            {
                this._cpuMemory[i++] = bone.Transform;
            }

            this._buffer.SetSubData((IntPtr)this._cpuMemory, sizeof(Matrix4x4) * i, 0);
        }

        public unsafe void LoadAll(GlbArmature armature)
        {
            this.BindAsGLObject();
            if (UseMapBuffer)
            {
                Matrix4x4* gpuMem = (Matrix4x4*)GL.MapBufferRange(BufferTarget.Uniform, 0, (nint)this.BufferPhysicalSize, BufferRangeAccessMask.Write | BufferRangeAccessMask.InvalidateBuffer | BufferRangeAccessMask.InvalidateRange | BufferRangeAccessMask.Unsynchronized);
                for (int i = 0; i < armature.UnsortedBones.Count; ++i)
                {
                    GlbBone bone = armature.UnsortedBones[i];
                    gpuMem[i] = bone.Transform;
                }

                if (GL.UnmapBuffer(BufferTarget.Uniform))
                {
                    return; // Goto normal SubData method if data got corrupted for some reason (must do so by spec!)
                }
                else
                {
                    // Spec states that buffer content must be reinitialized if we failed to do a dma. subdata would reinitialize it in theory, but w/e
                    GL.BufferData(BufferTarget.Uniform, (int)this.BufferPhysicalSize, 0, BufferUsage.StreamDraw);
                    Client.Instance.Logger.Log(LogLevel.Warn, "Bones uniform buffer object experienced a DMA failure. Slower path will be taken!");
                }
            }

            for (int i = 0; i < armature.UnsortedBones.Count; ++i)
            {
                GlbBone bone = armature.UnsortedBones[i];
                this._cpuMemory[i] = bone.Transform;
            }

            this._buffer.SetSubData((IntPtr)this._cpuMemory, sizeof(Matrix4x4) * armature.UnsortedBones.Count, 0);
        }
    }
}
