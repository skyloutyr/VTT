namespace VTT.Render.Shaders.UBO
{
    using System;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using VTT.Asset.Glb;
    using VTT.GL.Bindings;
    using VTT.Network;
    using VTT.Util;

    public class UniformBufferMaterial : UniformBufferObject<MaterialUBO>
    {
        public bool UseMapBuffer => Client.Instance.Settings.EnableUBODMA;

        public UniformBufferMaterial(GlbMaterial material, bool anyAnimated) : base(3, 1, anyAnimated ? BufferUsage.StreamDraw : BufferUsage.StaticDraw) => this.Initialize(material);

        public unsafe void Initialize(GlbMaterial material)
        {
            this._cpuMemory->ColorDiffuse = material.BaseColorFactor;
            this._cpuMemory->metalness = material.MetallicFactor;
            this._cpuMemory->roughness = material.RoughnessFactor;
            this._cpuMemory->alpha_cutoff = material.AlphaCutoff;
            this._cpuMemory->frameDiffuse = material.BaseColorAnimation.FindFrameForIndex(0).LocationUniform;
            this._cpuMemory->frameNormal = material.NormalAnimation.FindFrameForIndex(0).LocationUniform;
            this._cpuMemory->frameEmissive = material.EmissionAnimation.FindFrameForIndex(0).LocationUniform;
            this._cpuMemory->frameAOMR = material.OcclusionMetallicRoughnessAnimation.FindFrameForIndex(0).LocationUniform;
            this._cpuMemory->MaterialIndex = material.Index;
            this.Upload();
        }

        public unsafe void UpdateFull(GlbMaterial material, double animationIndex)
        {
            // No MapBuffer tricks here bc full update is very rare
            this._cpuMemory->ColorDiffuse = material.BaseColorFactor;
            this._cpuMemory->metalness = material.MetallicFactor;
            this._cpuMemory->roughness = material.RoughnessFactor;
            this._cpuMemory->alpha_cutoff = material.AlphaCutoff;
            this._cpuMemory->frameDiffuse = material.BaseColorAnimation.FindFrameForIndex(animationIndex).LocationUniform;
            this._cpuMemory->frameNormal = material.NormalAnimation.FindFrameForIndex(animationIndex).LocationUniform;
            this._cpuMemory->frameEmissive = material.EmissionAnimation.FindFrameForIndex(animationIndex).LocationUniform;
            this._cpuMemory->frameAOMR = material.OcclusionMetallicRoughnessAnimation.FindFrameForIndex(animationIndex).LocationUniform;
            this._cpuMemory->MaterialIndex = material.Index;
            this.Upload();
        }

        public unsafe void UpdateAnimationsOnly(GlbMaterial material, double animationIndex)
        {
            this.BindAsGLObject();
            if (UseMapBuffer)
            {
                // Fancy mapping only the animation section as a range of 4 vec4s
                Vector4* gpuMemory = (Vector4*)GL.MapBufferRange(BufferTarget.Uniform, 16, sizeof(float) * 4 * 4, BufferRangeAccessMask.Write | BufferRangeAccessMask.InvalidateRange | BufferRangeAccessMask.Unsynchronized);
                gpuMemory[0] = material.BaseColorAnimation.FindFrameForIndex(animationIndex).LocationUniform;
                gpuMemory[1] = material.NormalAnimation.FindFrameForIndex(animationIndex).LocationUniform;
                gpuMemory[2] = material.EmissionAnimation.FindFrameForIndex(animationIndex).LocationUniform;
                gpuMemory[3] = material.OcclusionMetallicRoughnessAnimation.FindFrameForIndex(animationIndex).LocationUniform;
                if (GL.UnmapBuffer(BufferTarget.Uniform))
                {
                    return; // Goto normal SubData method if data got corrupted for some reason (must do so by spec!)
                }
                else
                {
                    // Spec states that buffer content must be reinitialized if we failed to do a dma. subdata would reinitialize it in theory, but w/e
                    GL.BufferData(BufferTarget.Uniform, (int)this.BufferPhysicalSize, this._cpuMemory, BufferUsage.StreamDraw); // Stream draw guaranteed here as this is the animations path
                    Client.Instance.Logger.Log(LogLevel.Warn, "Material uniform buffer object experienced a DMA failure. Slower path will be taken!");
                    this.UpdateFull(material, animationIndex);
                    return;
                }
            }
            else
            {
                this._cpuMemory->frameDiffuse = material.BaseColorAnimation.FindFrameForIndex(animationIndex).LocationUniform;
                this._cpuMemory->frameNormal = material.NormalAnimation.FindFrameForIndex(animationIndex).LocationUniform;
                this._cpuMemory->frameEmissive = material.EmissionAnimation.FindFrameForIndex(animationIndex).LocationUniform;
                this._cpuMemory->frameAOMR = material.OcclusionMetallicRoughnessAnimation.FindFrameForIndex(animationIndex).LocationUniform;
                this._buffer.SetSubData((IntPtr)((byte*)this._cpuMemory + 16), 64, 16); // 16 byte offset is the start for animation uniforms
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 96, Pack = 1)]
    public struct MaterialUBO
    {
        [FieldOffset(0)] public float colorRgba;
        [FieldOffset(4)] public float metalness;
        [FieldOffset(8)] public float roughness;
        [FieldOffset(12)] public float alpha_cutoff;
        [FieldOffset(16)] public Vector4 frameDiffuse;
        [FieldOffset(32)] public Vector4 frameNormal;
        [FieldOffset(48)] public Vector4 frameEmissive;
        [FieldOffset(64)] public Vector4 frameAOMR;
        [FieldOffset(80)] public float index;
        [FieldOffset(84)] public float padding_84;
        [FieldOffset(88)] public float padding_88;
        [FieldOffset(92)] public float padding_92;

        public Vector4 ColorDiffuse
        {
            set => this.colorRgba = VTTMath.UInt32BitsToSingle(value.Rgba());
        }

        public uint MaterialIndex
        {
            set => this.index = VTTMath.UInt32BitsToSingle(value);
        }
    }
}
