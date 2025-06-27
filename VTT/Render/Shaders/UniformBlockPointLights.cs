namespace VTT.Render.Shaders
{
    using System.Numerics;

    public class UniformBlockPointLights
    {
        [UniformReference("pl_position", Array = true)]
        public UniformState<Vector3> Positions { get; set; }

        [UniformReference("pl_color", Array = true)]
        public UniformState<Vector3> Colors { get; set; }

        [UniformReference("pl_cutout", Array = true)]
        public UniformState<Vector2> Thresholds { get; set; }

        [UniformReference("pl_index", Array = true)]
        public UniformState<int> Indices { get; set; }

        [UniformReference("pl_num")]
        public UniformState<int> Amount { get; set; }

        [UniformReference("pl_shadow_maps")]
        public UniformState<int> ShadowMapsSampler { get; set; }
    }
}
