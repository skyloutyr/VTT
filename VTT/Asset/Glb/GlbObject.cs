namespace VTT.Asset.Glb
{
    using glTFLoader.Schema;
    using OpenTK.Mathematics;
    using System;
    using System.Collections.Generic;
    using VTT.GL;
    using VTT.Util;
    using Camera = glTFLoader.Schema.Camera;

    public class GlbObject
    {
        public string Name { get; set; }

        private Matrix4 _matCached = Matrix4.Identity;

        public Vector3 Position
        {
            get => this._position; 
            set
            {
                this._position = value;
                this._matCached = Matrix4.CreateScale(this.Scale) * Matrix4.CreateFromQuaternion(this.Rotation) * Matrix4.CreateTranslation(this.Position);
            }
        }
        public Quaternion Rotation
        {
            get => this._rotation; 
            set
            {
                this._rotation = value;
                this._matCached = Matrix4.CreateScale(this.Scale) * Matrix4.CreateFromQuaternion(this.Rotation) * Matrix4.CreateTranslation(this.Position);
            }
        }
        public Vector3 Scale
        {
            get => this._scale; 
            set
            {
                this._scale = value;
                this._matCached = Matrix4.CreateScale(this.Scale) * Matrix4.CreateFromQuaternion(this.Rotation) * Matrix4.CreateTranslation(this.Position);
            }
        }

        public GlbObjectType Type { get; set; } = GlbObjectType.Node;

        public GlbObject Parent { get; set; }
        public List<GlbObject> Children { get; } = new List<GlbObject>();


        public Camera Camera { get; set; }

        public List<GlbMesh> Meshes { get; } = new List<GlbMesh>();
        public GlbLight Light { get; set; }
        public VTT.Util.AABox Bounds { get; set; }

        internal Node _node;
        private Vector3 _position;
        private Quaternion _rotation;
        private Vector3 _scale;

        public Matrix4 CachedMatrix => this._matCached;

        public GlbObject(Node node) => this._node = node;

        public void Render(ShaderProgram shader, MatrixStack matrixStack, Matrix4 projection, Matrix4 view, double textureAnimationIndex, Action<GlbMesh> renderer = null)
        {
            matrixStack.Push(this._matCached);

            if (this.Type == GlbObjectType.Mesh)
            {
                foreach (GlbMesh mesh in this.Meshes)
                {
                    mesh.Render(shader, matrixStack, projection, view, textureAnimationIndex, renderer);
                }
            }

            foreach (GlbObject child in this.Children)
            {
                child.Render(shader, matrixStack, projection, view, textureAnimationIndex, renderer);
            }

            matrixStack.Pop();
        }

        public void Dispose()
        {
            if (this.Type == GlbObjectType.Mesh)
            {
                foreach (GlbMesh m in this.Meshes)
                {
                    m.Dispose();
                }
            }

            foreach (GlbObject child in this.Children)
            {
                child.Dispose();
            }
        }
    }
}
