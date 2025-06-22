namespace VTT.Render
{
    using System;
    using System.Numerics;
    using VTT.Asset.Glb;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;
    using GL = GL.Bindings.GL;

    public static class CustomShaderRenderer
    {
        public static bool Render(Guid shaderAssetID, Map m, ShaderContainerLocalPassthroughData passthroughData, double textureAnimationIndex, double delta, out FastAccessShader shader)
        {
            shader = null;
            if (!Client.Instance.Settings.EnableCustomShaders)
            {
                return false;
            }

            if (!shaderAssetID.IsEmpty() && Client.Instance.AssetManager.ClientAssetLibrary.Assets.Get(shaderAssetID, Asset.AssetType.Shader, out Asset.Asset a) == Asset.AssetStatus.Return)
            {
                switch (a.Type)
                {
                    case Asset.AssetType.Shader:
                    {
                        if (a.Shader != null && a.Shader.NodeGraph != null && a.Shader.NodeGraph.IsLoaded)
                        {
                            shader = a.Shader.NodeGraph.GetGLShader(false);
                        }

                        break;
                    }

                    case Asset.AssetType.GlslFragmentShader:
                    {
                        if (a.GlslFragment != null && !string.IsNullOrEmpty(a.GlslFragment.Data))
                        {
                            shader = a.GlslFragment.GetGLShader(false);
                        }

                        break;
                    }

                    default:
                    {
                        break;
                    }
                }

                if (shader != null)
                {
                    if (!ShaderProgram.IsLastShaderSame(shader))
                    {
                        shader.Program.Bind();
                        Client.Instance.Frontend.Renderer.ObjectRenderer.UniformMainShaderData(m, shader, delta);
                    }

                    shader.Essentials.TintColor.Set(passthroughData.TintColor);
                    shader.Essentials.Alpha.Set(passthroughData.Alpha);
                    shader.Essentials.GridAlpha.Set(passthroughData.GridAlpha);
                    shader.Essentials.GridType.Set(passthroughData.GridType);
                    if (a.Type == Asset.AssetType.Shader) // Custom GLSL shaders can't define extra textures due to being raw GLSL data
                    {
                        GL.ActiveTexture(12);
                        if (a.Shader.NodeGraph.ExtraTextures.GetExtraTexture(out Texture t, out Vector2[] sz, out TextureAnimation[] anims) == Asset.AssetStatus.Return && t != null)
                        {
                            t.Bind();
                            for (int i = 0; i < sz.Length; ++i)
                            {
                                shader[$"unifiedTextureData[{i}]"].Set(sz[i]);
                                shader[$"unifiedTextureFrames[{i}]"].Set(anims[i].FindFrameForIndex(textureAnimationIndex).LocationUniform);
                            }
                        }
                        else
                        {
                            Client.Instance.Frontend.Renderer.White.Bind();
                        }
                    }

                    GL.ActiveTexture(0);
                    return true;
                }
            }

            return false;
        }
    }

    public class ShaderContainerLocalPassthroughData
    {
        public float Alpha { get; set; }
        public float GridAlpha { get; set; }
        public uint GridType { get; set; }
        public Vector4 TintColor { get; set; }
    }
}
