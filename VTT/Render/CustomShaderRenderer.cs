namespace VTT.Render
{
    using System;
    using System.Numerics;
    using VTT.Asset.Glb;
    using VTT.Control;
    using VTT.GL;
    using VTT.Network;
    using VTT.Util;
    using GL = VTT.GL.Bindings.GL;

    public static class CustomShaderRenderer
    {
        public static bool Render(Guid shaderAssetID, Map m, ShaderContainerLocalPassthroughData passthroughData, double textureAnimationIndex, double delta, out ShaderProgram shader)
        {
            shader = null;
            if (!Client.Instance.Settings.EnableCustomShaders)
            {
                return false;
            }

            if (!shaderAssetID.IsEmpty() && Client.Instance.AssetManager.ClientAssetLibrary.GetOrRequestAsset(shaderAssetID, Asset.AssetType.Shader, out Asset.Asset a) == Asset.AssetStatus.Return)
            {
                if (a.Type == Asset.AssetType.Shader && a.Shader != null && a.Shader.NodeGraph != null && a.Shader.NodeGraph.IsLoaded)
                {
                    shader = a.Shader.NodeGraph.GetGLShader(false);
                    if (shader != null)
                    {
                        if (!ShaderProgram.IsLastShaderSame(shader))
                        {
                            shader.Bind();
                            Client.Instance.Frontend.Renderer.ObjectRenderer.UniformMainShaderData(m, shader, delta);
                        }

                        shader["tint_color"].Set(passthroughData.TintColor);
                        shader["alpha"].Set(passthroughData.Alpha);
                        shader["grid_alpha"].Set(passthroughData.GridAlpha);
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

                        GL.ActiveTexture(0);
                        return true;
                    }
                }
                else
                {
                    if (a.Type == Asset.AssetType.GlslFragmentShader && a.GlslFragment != null && !string.IsNullOrEmpty(a.GlslFragment.Data))
                    {

                    }
                }
            }

            return false;
        }
    }

    public class ShaderContainerLocalPassthroughData
    {
        public float Alpha { get; set; }
        public float GridAlpha { get; set; }
        public Vector4 TintColor { get; set; }
    }
}
