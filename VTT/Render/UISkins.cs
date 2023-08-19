namespace VTT.Render
{
    using ImGuiNET;
    using System.Numerics;

    public static class UISkins
    {
        public static ImGuiStyle DefaultStyleData { get; set; }

        public static void Reset()
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            DefaultStyleData.Apply(style);
        }

        public static void SkinSharpGray()
        {

            ImGuiStylePtr style = ImGui.GetStyle();
            style.WindowRounding = 5.3f;
            style.FrameRounding = 2.3f;
            style.ScrollbarRounding = 0;

            style.Colors[(int)ImGuiCol.Text] = new Vector4(0.90f, 0.90f, 0.90f, 0.90f);
            style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.60f, 0.60f, 0.60f, 1.00f);
            style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.09f, 0.09f, 0.15f, 1.00f);
            style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.05f, 0.05f, 0.10f, 0.85f);
            style.Colors[(int)ImGuiCol.Border] = new Vector4(0.70f, 0.70f, 0.70f, 0.65f);
            style.Colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.00f, 0.00f, 0.01f, 1.00f);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.90f, 0.80f, 0.80f, 0.40f);
            style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.90f, 0.65f, 0.65f, 0.45f);
            style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.83f);
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.40f, 0.40f, 0.80f, 0.20f);
            style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.00f, 0.00f, 0.00f, 0.87f);
            style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.01f, 0.01f, 0.02f, 0.80f);
            style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.20f, 0.25f, 0.30f, 0.60f);
            style.Colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.55f, 0.53f, 0.55f, 0.51f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.56f, 0.56f, 0.56f, 1.00f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.56f, 0.56f, 0.56f, 0.91f);
            style.Colors[(int)ImGuiCol.CheckMark] = new Vector4(0.90f, 0.90f, 0.90f, 0.83f);
            style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.70f, 0.70f, 0.70f, 0.62f);
            style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.30f, 0.30f, 0.30f, 0.84f);
            style.Colors[(int)ImGuiCol.Button] = new Vector4(0.48f, 0.72f, 0.89f, 0.49f);
            style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.50f, 0.69f, 0.99f, 0.68f);
            style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.80f, 0.50f, 0.50f, 1.00f);
            style.Colors[(int)ImGuiCol.Header] = new Vector4(0.30f, 0.69f, 1.00f, 0.53f);
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.44f, 0.61f, 0.86f, 1.00f);
            style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.38f, 0.62f, 0.83f, 1.00f);
            style.Colors[(int)ImGuiCol.ResizeGrip] = new Vector4(1.00f, 1.00f, 1.00f, 0.85f);
            style.Colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(1.00f, 1.00f, 1.00f, 0.60f);
            style.Colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(1.00f, 1.00f, 1.00f, 0.90f);
            style.Colors[(int)ImGuiCol.PlotLines] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
            style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.00f, 0.00f, 1.00f, 0.35f);
        }

        public static void DarkRounded()
        {
            ImGui.GetStyle().FrameRounding = 4.0f;
            ImGui.GetStyle().GrabRounding = 4.0f;

            RangeAccessor<Vector4> colors = ImGui.GetStyle().Colors;
            colors[(int)ImGuiCol.Text] = new Vector4(0.95f, 0.96f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.36f, 0.42f, 0.47f, 1.00f);
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.11f, 0.15f, 0.17f, 1.00f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.15f, 0.18f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.08f, 0.10f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.20f, 0.25f, 0.29f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.12f, 0.20f, 0.28f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.09f, 0.12f, 0.14f, 1.00f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.09f, 0.12f, 0.14f, 0.65f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.08f, 0.10f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.00f, 0.00f, 0.00f, 0.51f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.15f, 0.18f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.02f, 0.02f, 0.02f, 0.39f);
            colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.20f, 0.25f, 0.29f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.18f, 0.22f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.09f, 0.21f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.28f, 0.56f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.28f, 0.56f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.37f, 0.61f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.Button] = new Vector4(0.20f, 0.25f, 0.29f, 1.00f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.28f, 0.56f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.06f, 0.53f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.20f, 0.25f, 0.29f, 0.55f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.80f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.Separator] = new Vector4(0.20f, 0.25f, 0.29f, 1.00f);
            colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.10f, 0.40f, 0.75f, 0.78f);
            colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.10f, 0.40f, 0.75f, 1.00f);
            colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.26f, 0.59f, 0.98f, 0.25f);
            colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.67f);
            colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.26f, 0.59f, 0.98f, 0.95f);
            colors[(int)ImGuiCol.Tab] = new Vector4(0.11f, 0.15f, 0.17f, 1.00f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.80f);
            colors[(int)ImGuiCol.TabActive] = new Vector4(0.20f, 0.25f, 0.29f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.11f, 0.15f, 0.17f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.11f, 0.15f, 0.17f, 1.00f);
            colors[(int)ImGuiCol.PlotLines] = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
            colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
            colors[(int)ImGuiCol.DragDropTarget] = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);
            colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
            colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
            colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);
        }

        public static void Source()
        {
            RangeAccessor<Vector4> colors = ImGui.GetStyle().Colors;
            colors[(int)ImGuiCol.Text] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.29f, 0.34f, 0.26f, 1.00f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.29f, 0.34f, 0.26f, 1.00f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.24f, 0.27f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.54f, 0.57f, 0.51f, 0.50f);
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.14f, 0.16f, 0.11f, 0.52f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.24f, 0.27f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.27f, 0.30f, 0.23f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.30f, 0.34f, 0.26f, 1.00f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.24f, 0.27f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.29f, 0.34f, 0.26f, 1.00f);
            colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.00f, 0.00f, 0.00f, 0.51f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.24f, 0.27f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.35f, 0.42f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.28f, 0.32f, 0.24f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.25f, 0.30f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.23f, 0.27f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.59f, 0.54f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.35f, 0.42f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.54f, 0.57f, 0.51f, 0.50f);
            colors[(int)ImGuiCol.Button] = new Vector4(0.29f, 0.34f, 0.26f, 0.40f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.35f, 0.42f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.54f, 0.57f, 0.51f, 0.50f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.35f, 0.42f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.35f, 0.42f, 0.31f, 0.6f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.54f, 0.57f, 0.51f, 0.50f);
            colors[(int)ImGuiCol.Separator] = new Vector4(0.14f, 0.16f, 0.11f, 1.00f);
            colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.54f, 0.57f, 0.51f, 1.00f);
            colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.59f, 0.54f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.19f, 0.23f, 0.18f, 0.00f); // grip invis
            colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.54f, 0.57f, 0.51f, 1.00f);
            colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.59f, 0.54f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.Tab] = new Vector4(0.35f, 0.42f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.54f, 0.57f, 0.51f, 0.78f);
            colors[(int)ImGuiCol.TabActive] = new Vector4(0.59f, 0.54f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.24f, 0.27f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.35f, 0.42f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.59f, 0.54f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.DockingEmptyBg] = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.PlotLines] = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
            colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(0.59f, 0.54f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(1.00f, 0.78f, 0.28f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.59f, 0.54f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.DragDropTarget] = new Vector4(0.73f, 0.67f, 0.24f, 1.00f);
            colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.59f, 0.54f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
            colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
            colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);

            ImGuiStylePtr style = ImGui.GetStyle();
            style.FrameBorderSize = 1.0f;
            style.WindowRounding = 0.0f;
            style.ChildRounding = 0.0f;
            style.FrameRounding = 0.0f;
            style.PopupRounding = 0.0f;
            style.ScrollbarRounding = 0.0f;
            style.GrabRounding = 0.0f;
            style.TabRounding = 0.0f;
        }

        public static void HumanRevolution()
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            RangeAccessor<Vector4> colors = style.Colors;

            colors[(int)ImGuiCol.Text] = new Vector4(0.92f, 0.92f, 0.92f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.44f, 0.44f, 0.44f, 1.00f);
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.06f, 0.06f, 0.06f, 1.00f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.51f, 0.36f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.11f, 0.11f, 0.11f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.51f, 0.36f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.78f, 0.55f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.51f, 0.36f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.91f, 0.64f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.00f, 0.00f, 0.00f, 0.51f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.11f, 0.11f, 0.11f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.06f, 0.06f, 0.06f, 0.53f);
            colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.21f, 0.21f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.47f, 0.47f, 0.47f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.81f, 0.83f, 0.81f, 1.00f);
            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.78f, 0.55f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.91f, 0.64f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.91f, 0.64f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.Button] = new Vector4(0.51f, 0.36f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.91f, 0.64f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.78f, 0.55f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.51f, 0.36f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.91f, 0.64f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.93f, 0.65f, 0.14f, 1.00f);
            colors[(int)ImGuiCol.Separator] = new Vector4(0.21f, 0.21f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.91f, 0.64f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.78f, 0.55f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.21f, 0.21f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.91f, 0.64f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.78f, 0.55f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.Tab] = new Vector4(0.51f, 0.36f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.91f, 0.64f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.TabActive] = new Vector4(0.78f, 0.55f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.07f, 0.10f, 0.15f, 0.97f);
            colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.14f, 0.26f, 0.42f, 1.00f);
            colors[(int)ImGuiCol.PlotLines] = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
            colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
            colors[(int)ImGuiCol.DragDropTarget] = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);
            colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
            colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
            colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);

            style.FramePadding = new Vector2(4, 2);
            style.ItemSpacing = new Vector2(10, 2);
            style.IndentSpacing = 12;
            style.ScrollbarSize = 10;

            style.WindowRounding = 4;
            style.FrameRounding = 4;
            style.PopupRounding = 4;
            style.ScrollbarRounding = 6;
            style.GrabRounding = 4;
            style.TabRounding = 4;

            style.WindowTitleAlign = new Vector2(1.0f, 0.5f);
            style.WindowMenuButtonPosition = ImGuiDir.Right;

            style.DisplaySafeAreaPadding = new Vector2(4, 4);
        }

        public static void DeepHell()
        {
            var style = ImGui.GetStyle();
            style.FrameRounding = 4.0f;
            style.WindowBorderSize = 0.0f;
            style.PopupBorderSize = 0.0f;
            style.GrabRounding = 4.0f;

            var colors = style.Colors;
            colors[(int)ImGuiCol.Text] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.73f, 0.75f, 0.74f, 1.00f);
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.09f, 0.09f, 0.09f, 0.94f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.20f, 0.20f, 0.20f, 0.50f);
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.71f, 0.39f, 0.39f, 0.54f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.84f, 0.66f, 0.66f, 0.40f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.84f, 0.66f, 0.66f, 0.67f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.47f, 0.22f, 0.22f, 0.67f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.47f, 0.22f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.47f, 0.22f, 0.22f, 0.67f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.34f, 0.16f, 0.16f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.02f, 0.02f, 0.02f, 0.53f);
            colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.31f, 0.31f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.51f, 0.51f, 0.51f, 1.00f);
            colors[(int)ImGuiCol.CheckMark] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.71f, 0.39f, 0.39f, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.84f, 0.66f, 0.66f, 1.00f);
            colors[(int)ImGuiCol.Button] = new Vector4(0.47f, 0.22f, 0.22f, 0.65f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.71f, 0.39f, 0.39f, 0.65f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.20f, 0.20f, 0.20f, 0.50f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.71f, 0.39f, 0.39f, 0.54f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.84f, 0.66f, 0.66f, 0.65f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.84f, 0.66f, 0.66f, 0.00f);
            colors[(int)ImGuiCol.Separator] = new Vector4(0.43f, 0.43f, 0.50f, 0.50f);
            colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.71f, 0.39f, 0.39f, 0.54f);
            colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.71f, 0.39f, 0.39f, 0.54f);
            colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.71f, 0.39f, 0.39f, 0.54f);
            colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.84f, 0.66f, 0.66f, 0.66f);
            colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.84f, 0.66f, 0.66f, 0.66f);
            colors[(int)ImGuiCol.Tab] = new Vector4(0.71f, 0.39f, 0.39f, 0.54f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.84f, 0.66f, 0.66f, 0.66f);
            colors[(int)ImGuiCol.TabActive] = new Vector4(0.84f, 0.66f, 0.66f, 0.66f);
            colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.07f, 0.10f, 0.15f, 0.97f);
            colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.14f, 0.26f, 0.42f, 1.00f);
            colors[(int)ImGuiCol.PlotLines] = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
            colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
            colors[(int)ImGuiCol.DragDropTarget] = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);
            colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);
            colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
            colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
            colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);
        }

        public static void VisualStudio()
        {
            static Vector4 ColorFromBytes(byte r, byte g, byte b) => new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1.0f);

            ImGuiStylePtr style = ImGui.GetStyle();
            RangeAccessor<Vector4> colors = style.Colors;

            Vector4 bgColor = ColorFromBytes(37, 37, 38);
            Vector4 lightBgColor = ColorFromBytes(82, 82, 85);
            Vector4 veryLightBgColor = ColorFromBytes(90, 90, 95);

            Vector4 panelColor = ColorFromBytes(51, 51, 55);
            Vector4 panelHoverColor = ColorFromBytes(29, 151, 236);
            Vector4 panelActiveColor = ColorFromBytes(0, 119, 200);

            Vector4 textColor = ColorFromBytes(255, 255, 255);
            Vector4 textDisabledColor = ColorFromBytes(151, 151, 151);
            Vector4 borderColor = ColorFromBytes(78, 78, 78);

            colors[(int)ImGuiCol.Text] = textColor;
            colors[(int)ImGuiCol.TextDisabled] = textDisabledColor;
            colors[(int)ImGuiCol.TextSelectedBg] = panelActiveColor;
            colors[(int)ImGuiCol.WindowBg] = bgColor;
            colors[(int)ImGuiCol.ChildBg] = bgColor;
            colors[(int)ImGuiCol.PopupBg] = bgColor;
            colors[(int)ImGuiCol.Border] = borderColor;
            colors[(int)ImGuiCol.BorderShadow] = borderColor;
            colors[(int)ImGuiCol.FrameBg] = panelColor;
            colors[(int)ImGuiCol.FrameBgHovered] = panelHoverColor;
            colors[(int)ImGuiCol.FrameBgActive] = panelActiveColor;
            colors[(int)ImGuiCol.TitleBg] = bgColor;
            colors[(int)ImGuiCol.TitleBgActive] = bgColor;
            colors[(int)ImGuiCol.TitleBgCollapsed] = bgColor;
            colors[(int)ImGuiCol.MenuBarBg] = panelColor;
            colors[(int)ImGuiCol.ScrollbarBg] = panelColor;
            colors[(int)ImGuiCol.ScrollbarGrab] = lightBgColor;
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = veryLightBgColor;
            colors[(int)ImGuiCol.ScrollbarGrabActive] = veryLightBgColor;
            colors[(int)ImGuiCol.CheckMark] = panelActiveColor;
            colors[(int)ImGuiCol.SliderGrab] = panelHoverColor;
            colors[(int)ImGuiCol.SliderGrabActive] = panelActiveColor;
            colors[(int)ImGuiCol.Button] = panelColor;
            colors[(int)ImGuiCol.ButtonHovered] = panelHoverColor;
            colors[(int)ImGuiCol.ButtonActive] = panelHoverColor;
            colors[(int)ImGuiCol.Header] = panelColor;
            colors[(int)ImGuiCol.HeaderHovered] = panelHoverColor;
            colors[(int)ImGuiCol.HeaderActive] = panelActiveColor;
            colors[(int)ImGuiCol.Separator] = borderColor;
            colors[(int)ImGuiCol.SeparatorHovered] = borderColor;
            colors[(int)ImGuiCol.SeparatorActive] = borderColor;
            colors[(int)ImGuiCol.ResizeGrip] = bgColor;
            colors[(int)ImGuiCol.ResizeGripHovered] = panelColor;
            colors[(int)ImGuiCol.ResizeGripActive] = lightBgColor;
            colors[(int)ImGuiCol.PlotLines] = panelActiveColor;
            colors[(int)ImGuiCol.PlotLinesHovered] = panelHoverColor;
            colors[(int)ImGuiCol.PlotHistogram] = panelActiveColor;
            colors[(int)ImGuiCol.PlotHistogramHovered] = panelHoverColor;
            colors[(int)ImGuiCol.DragDropTarget] = bgColor;
            colors[(int)ImGuiCol.NavHighlight] = bgColor;
            colors[(int)ImGuiCol.DockingPreview] = panelActiveColor;
            colors[(int)ImGuiCol.Tab] = bgColor;
            colors[(int)ImGuiCol.TabActive] = panelActiveColor;
            colors[(int)ImGuiCol.TabUnfocused] = bgColor;
            colors[(int)ImGuiCol.TabUnfocusedActive] = panelActiveColor;
            colors[(int)ImGuiCol.TabHovered] = panelHoverColor;

            style.WindowRounding = 0.0f;
            style.ChildRounding = 0.0f;
            style.FrameRounding = 0.0f;
            style.GrabRounding = 0.0f;
            style.PopupRounding = 0.0f;
            style.ScrollbarRounding = 0.0f;
            style.TabRounding = 0.0f;
        }

        public static void UnityDark()
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            style.Colors[(int)ImGuiCol.Text] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
            style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.13f, 0.14f, 0.15f, 1.00f);
            style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.13f, 0.14f, 0.15f, 1.00f);
            style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.13f, 0.14f, 0.15f, 1.00f);
            style.Colors[(int)ImGuiCol.Border] = new Vector4(0.43f, 0.43f, 0.50f, 0.50f);
            style.Colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.38f, 0.38f, 0.38f, 1.00f);
            style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.67f, 0.67f, 0.67f, 0.39f);
            style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.08f, 0.08f, 0.09f, 1.00f);
            style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.08f, 0.08f, 0.09f, 1.00f);
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.00f, 0.00f, 0.00f, 0.51f);
            style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
            style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.02f, 0.02f, 0.02f, 0.53f);
            style.Colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.31f, 0.31f, 0.31f, 1.00f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.51f, 0.51f, 0.51f, 1.00f);
            style.Colors[(int)ImGuiCol.CheckMark] = new Vector4(0.11f, 0.64f, 0.92f, 1.00f);
            style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.11f, 0.64f, 0.92f, 1.00f);
            style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.08f, 0.50f, 0.72f, 1.00f);
            style.Colors[(int)ImGuiCol.Button] = new Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.38f, 0.38f, 0.38f, 1.00f);
            style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.67f, 0.67f, 0.67f, 0.39f);
            style.Colors[(int)ImGuiCol.Header] = new Vector4(0.22f, 0.22f, 0.22f, 1.00f);
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.67f, 0.67f, 0.67f, 0.39f);
            style.Colors[(int)ImGuiCol.Separator] = style.Colors[(int)ImGuiCol.Border];
            style.Colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.41f, 0.42f, 0.44f, 1.00f);
            style.Colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.26f, 0.59f, 0.98f, 0.95f);
            style.Colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            style.Colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.29f, 0.30f, 0.31f, 0.67f);
            style.Colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.26f, 0.59f, 0.98f, 0.95f);
            style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.08f, 0.08f, 0.09f, 0.83f);
            style.Colors[(int)ImGuiCol.TabHovered] = new Vector4(0.33f, 0.34f, 0.36f, 0.83f);
            style.Colors[(int)ImGuiCol.TabActive] = new Vector4(0.23f, 0.23f, 0.24f, 1.00f);
            style.Colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.08f, 0.08f, 0.09f, 1.00f);
            style.Colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.13f, 0.14f, 0.15f, 1.00f);
            style.Colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.26f, 0.59f, 0.98f, 0.70f);
            style.Colors[(int)ImGuiCol.DockingEmptyBg] = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotLines] = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
            style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
            style.Colors[(int)ImGuiCol.DragDropTarget] = new Vector4(0.11f, 0.64f, 0.92f, 1.00f);
            style.Colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
            style.Colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
            style.Colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
            style.Colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);
            style.GrabRounding = style.FrameRounding = 2.3f;
        }

        public static void MSLight()
        {
            // Microsoft style by usernameiwantedwasalreadytaken from ImThemes
            ImGuiStylePtr style = ImGui.GetStyle();

            style.Alpha = 1.0f;
            style.DisabledAlpha = 0.6000000238418579f;
            style.WindowPadding = new Vector2(4.0f, 6.0f);
            style.WindowRounding = 0.0f;
            style.WindowBorderSize = 0.0f;
            style.WindowMinSize = new Vector2(32.0f, 32.0f);
            style.WindowTitleAlign = new Vector2(0.0f, 0.5f);
            style.WindowMenuButtonPosition = ImGuiDir.Left;
            style.ChildRounding = 0.0f;
            style.ChildBorderSize = 1.0f;
            style.PopupRounding = 0.0f;
            style.PopupBorderSize = 1.0f;
            style.FramePadding = new Vector2(8.0f, 6.0f);
            style.FrameRounding = 0.0f;
            style.FrameBorderSize = 1.0f;
            style.ItemSpacing = new Vector2(8.0f, 6.0f);
            style.ItemInnerSpacing = new Vector2(8.0f, 6.0f);
            style.CellPadding = new Vector2(4.0f, 2.0f);
            style.IndentSpacing = 20.0f;
            style.ColumnsMinSpacing = 6.0f;
            style.ScrollbarSize = 20.0f;
            style.ScrollbarRounding = 0.0f;
            style.GrabMinSize = 5.0f;
            style.GrabRounding = 0.0f;
            style.TabRounding = 4.0f;
            style.TabBorderSize = 0.0f;
            style.TabMinWidthForCloseButton = 0.0f;
            style.ColorButtonPosition = ImGuiDir.Right;
            style.ButtonTextAlign = new Vector2(0.5f, 0.5f);
            style.SelectableTextAlign = new Vector2(0.0f, 0.0f);

            style.Colors[(int)ImGuiCol.Text] = ImVec4(0.09803921729326248, 0.09803921729326248, 0.09803921729326248, 1.0);
            style.Colors[(int)ImGuiCol.TextDisabled] = ImVec4(0.4980392158031464, 0.4980392158031464, 0.4980392158031464, 1.0);
            style.Colors[(int)ImGuiCol.WindowBg] = ImVec4(0.9490196108818054, 0.9490196108818054, 0.9490196108818054, 1.0);
            style.Colors[(int)ImGuiCol.ChildBg] = ImVec4(0.9490196108818054, 0.9490196108818054, 0.9490196108818054, 1.0);
            style.Colors[(int)ImGuiCol.PopupBg] = ImVec4(1.0, 1.0, 1.0, 1.0);
            style.Colors[(int)ImGuiCol.Border] = ImVec4(0.6000000238418579, 0.6000000238418579, 0.6000000238418579, 1.0);
            style.Colors[(int)ImGuiCol.BorderShadow] = ImVec4(0.0, 0.0, 0.0, 0.0);
            style.Colors[(int)ImGuiCol.FrameBg] = ImVec4(1.0, 1.0, 1.0, 1.0);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = ImVec4(0.0, 0.4666666686534882, 0.8392156958580017, 0.2000000029802322);
            style.Colors[(int)ImGuiCol.FrameBgActive] = ImVec4(0.0, 0.4666666686534882, 0.8392156958580017, 1.0);
            style.Colors[(int)ImGuiCol.TitleBg] = ImVec4(0.03921568766236305, 0.03921568766236305, 0.03921568766236305, 1.0);
            style.Colors[(int)ImGuiCol.TitleBgActive] = ImVec4(0.1568627506494522, 0.2862745225429535, 0.47843137383461, 1.0);
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = ImVec4(0.0, 0.0, 0.0, 0.5099999904632568);
            style.Colors[(int)ImGuiCol.MenuBarBg] = ImVec4(0.8588235378265381, 0.8588235378265381, 0.8588235378265381, 1.0);
            style.Colors[(int)ImGuiCol.ScrollbarBg] = ImVec4(0.8588235378265381, 0.8588235378265381, 0.8588235378265381, 1.0);
            style.Colors[(int)ImGuiCol.ScrollbarGrab] = ImVec4(0.686274528503418, 0.686274528503418, 0.686274528503418, 1.0);
            style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = ImVec4(0.0, 0.0, 0.0, 0.2000000029802322);
            style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = ImVec4(0.0, 0.0, 0.0, 0.5);
            style.Colors[(int)ImGuiCol.CheckMark] = ImVec4(0.09803921729326248, 0.09803921729326248, 0.09803921729326248, 1.0);
            style.Colors[(int)ImGuiCol.SliderGrab] = ImVec4(0.686274528503418, 0.686274528503418, 0.686274528503418, 1.0);
            style.Colors[(int)ImGuiCol.SliderGrabActive] = ImVec4(0.0, 0.0, 0.0, 0.5);
            style.Colors[(int)ImGuiCol.Button] = ImVec4(0.8588235378265381, 0.8588235378265381, 0.8588235378265381, 1.0);
            style.Colors[(int)ImGuiCol.ButtonHovered] = ImVec4(0.0, 0.4666666686534882, 0.8392156958580017, 0.2000000029802322);
            style.Colors[(int)ImGuiCol.ButtonActive] = ImVec4(0.0, 0.4666666686534882, 0.8392156958580017, 1.0);
            style.Colors[(int)ImGuiCol.Header] = ImVec4(0.8588235378265381, 0.8588235378265381, 0.8588235378265381, 1.0);
            style.Colors[(int)ImGuiCol.HeaderHovered] = ImVec4(0.0, 0.4666666686534882, 0.8392156958580017, 0.2000000029802322);
            style.Colors[(int)ImGuiCol.HeaderActive] = ImVec4(0.0, 0.4666666686534882, 0.8392156958580017, 1.0);
            style.Colors[(int)ImGuiCol.Separator] = ImVec4(0.4274509847164154, 0.4274509847164154, 0.4980392158031464, 0.5);
            style.Colors[(int)ImGuiCol.SeparatorHovered] = ImVec4(0.09803921729326248, 0.4000000059604645, 0.7490196228027344, 0.7799999713897705);
            style.Colors[(int)ImGuiCol.SeparatorActive] = ImVec4(0.09803921729326248, 0.4000000059604645, 0.7490196228027344, 1.0);
            style.Colors[(int)ImGuiCol.ResizeGrip] = ImVec4(0.2588235437870026, 0.5882353186607361, 0.9764705896377563, 0.2000000029802322);
            style.Colors[(int)ImGuiCol.ResizeGripHovered] = ImVec4(0.2588235437870026, 0.5882353186607361, 0.9764705896377563, 0.6700000166893005);
            style.Colors[(int)ImGuiCol.ResizeGripActive] = ImVec4(0.2588235437870026, 0.5882353186607361, 0.9764705896377563, 0.949999988079071);
            style.Colors[(int)ImGuiCol.Tab] = ImVec4(0.1764705926179886, 0.3490196168422699, 0.5764706134796143, 0.8619999885559082);
            style.Colors[(int)ImGuiCol.TabHovered] = ImVec4(0.2588235437870026, 0.5882353186607361, 0.9764705896377563, 0.800000011920929);
            style.Colors[(int)ImGuiCol.TabActive] = ImVec4(0.196078434586525, 0.407843142747879, 0.6784313917160034, 1.0);
            style.Colors[(int)ImGuiCol.TabUnfocused] = ImVec4(0.06666667014360428, 0.1019607856869698, 0.1450980454683304, 0.9724000096321106);
            style.Colors[(int)ImGuiCol.TabUnfocusedActive] = ImVec4(0.1333333402872086, 0.2588235437870026, 0.4235294163227081, 1.0);
            style.Colors[(int)ImGuiCol.PlotLines] = ImVec4(0.6078431606292725, 0.6078431606292725, 0.6078431606292725, 1.0);
            style.Colors[(int)ImGuiCol.PlotLinesHovered] = ImVec4(1.0, 0.4274509847164154, 0.3490196168422699, 1.0);
            style.Colors[(int)ImGuiCol.PlotHistogram] = ImVec4(0.8980392217636108, 0.6980392336845398, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.PlotHistogramHovered] = ImVec4(1.0, 0.6000000238418579, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.TableHeaderBg] = ImVec4(0.1882352977991104, 0.1882352977991104, 0.2000000029802322, 1.0);
            style.Colors[(int)ImGuiCol.TableBorderStrong] = ImVec4(0.3098039329051971, 0.3098039329051971, 0.3490196168422699, 1.0);
            style.Colors[(int)ImGuiCol.TableBorderLight] = ImVec4(0.2274509817361832, 0.2274509817361832, 0.2470588237047195, 1.0);
            style.Colors[(int)ImGuiCol.TableRowBg] = ImVec4(0.0, 0.0, 0.0, 0.0);
            style.Colors[(int)ImGuiCol.TableRowBgAlt] = ImVec4(1.0, 1.0, 1.0, 0.05999999865889549);
            style.Colors[(int)ImGuiCol.TextSelectedBg] = ImVec4(0.2588235437870026, 0.5882353186607361, 0.9764705896377563, 0.3499999940395355);
            style.Colors[(int)ImGuiCol.DragDropTarget] = ImVec4(1.0, 1.0, 0.0, 0.8999999761581421);
            style.Colors[(int)ImGuiCol.NavHighlight] = ImVec4(0.2588235437870026, 0.5882353186607361, 0.9764705896377563, 1.0);
            style.Colors[(int)ImGuiCol.NavWindowingHighlight] = ImVec4(1.0, 1.0, 1.0, 0.699999988079071);
            style.Colors[(int)ImGuiCol.NavWindowingDimBg] = ImVec4(0.800000011920929, 0.800000011920929, 0.800000011920929, 0.2000000029802322);
            style.Colors[(int)ImGuiCol.ModalWindowDimBg] = ImVec4(0.800000011920929, 0.800000011920929, 0.800000011920929, 0.3499999940395355);
        }

        public static void Cherry()
        {
            ImGuiStylePtr style = ImGui.GetStyle();

            style.Alpha = 1.0f;
            style.DisabledAlpha = 0.6000000238418579f;
            style.WindowPadding = ImVec2(6.0, 3.0);
            style.WindowRounding = 0.0f;
            style.WindowBorderSize = 1.0f;
            style.WindowMinSize = ImVec2(32.0, 32.0);
            style.WindowTitleAlign = ImVec2(0.5, 0.5);
            style.WindowMenuButtonPosition = ImGuiDir.Left;
            style.ChildRounding = 0.0f;
            style.ChildBorderSize = 1.0f;
            style.PopupRounding = 0.0f;
            style.PopupBorderSize = 1.0f;
            style.FramePadding = ImVec2(5.0, 1.0);
            style.FrameRounding = 3.0f;
            style.FrameBorderSize = 1.0f;
            style.ItemSpacing = ImVec2(7.0, 1.0);
            style.ItemInnerSpacing = ImVec2(1.0, 1.0);
            style.CellPadding = ImVec2(4.0, 2.0);
            style.IndentSpacing = 6.0f;
            style.ColumnsMinSpacing = 6.0f;
            style.ScrollbarSize = 13.0f;
            style.ScrollbarRounding = 16.0f;
            style.GrabMinSize = 20.0f;
            style.GrabRounding = 2.0f;
            style.TabRounding = 4.0f;
            style.TabBorderSize = 1.0f;
            style.TabMinWidthForCloseButton = 0.0f;
            style.ColorButtonPosition = ImGuiDir.Right;
            style.ButtonTextAlign = ImVec2(0.5, 0.5);
            style.SelectableTextAlign = ImVec2(0.0, 0.0);

            style.Colors[(int)ImGuiCol.Text] = ImVec4(0.8588235378265381, 0.929411768913269, 0.886274516582489, 0.8799999952316284);
            style.Colors[(int)ImGuiCol.TextDisabled] = ImVec4(0.8588235378265381, 0.929411768913269, 0.886274516582489, 0.2800000011920929);
            style.Colors[(int)ImGuiCol.WindowBg] = ImVec4(0.1294117718935013, 0.1372549086809158, 0.168627455830574, 1.0);
            style.Colors[(int)ImGuiCol.ChildBg] = ImVec4(0.0, 0.0, 0.0, 0.0);
            style.Colors[(int)ImGuiCol.PopupBg] = ImVec4(0.2000000029802322, 0.2196078449487686, 0.2666666805744171, 0.8999999761581421);
            style.Colors[(int)ImGuiCol.Border] = ImVec4(0.5372549295425415, 0.47843137383461, 0.2549019753932953, 0.1620000004768372);
            style.Colors[(int)ImGuiCol.BorderShadow] = ImVec4(0.0, 0.0, 0.0, 0.0);
            style.Colors[(int)ImGuiCol.FrameBg] = ImVec4(0.2000000029802322, 0.2196078449487686, 0.2666666805744171, 1.0);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 0.7799999713897705);
            style.Colors[(int)ImGuiCol.FrameBgActive] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 1.0);
            style.Colors[(int)ImGuiCol.TitleBg] = ImVec4(0.2313725501298904, 0.2000000029802322, 0.2705882489681244, 1.0);
            style.Colors[(int)ImGuiCol.TitleBgActive] = ImVec4(0.501960813999176, 0.07450980693101883, 0.2549019753932953, 1.0);
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = ImVec4(0.2000000029802322, 0.2196078449487686, 0.2666666805744171, 0.75);
            style.Colors[(int)ImGuiCol.MenuBarBg] = ImVec4(0.2000000029802322, 0.2196078449487686, 0.2666666805744171, 0.4699999988079071);
            style.Colors[(int)ImGuiCol.ScrollbarBg] = ImVec4(0.2000000029802322, 0.2196078449487686, 0.2666666805744171, 1.0);
            style.Colors[(int)ImGuiCol.ScrollbarGrab] = ImVec4(0.08627451211214066, 0.1490196138620377, 0.1568627506494522, 1.0);
            style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 0.7799999713897705);
            style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 1.0);
            style.Colors[(int)ImGuiCol.CheckMark] = ImVec4(0.7098039388656616, 0.2196078449487686, 0.2666666805744171, 1.0);
            style.Colors[(int)ImGuiCol.SliderGrab] = ImVec4(0.4666666686534882, 0.7686274647712708, 0.8274509906768799, 0.1400000005960464);
            style.Colors[(int)ImGuiCol.SliderGrabActive] = ImVec4(0.7098039388656616, 0.2196078449487686, 0.2666666805744171, 1.0);
            style.Colors[(int)ImGuiCol.Button] = ImVec4(0.4666666686534882, 0.7686274647712708, 0.8274509906768799, 0.1400000005960464);
            style.Colors[(int)ImGuiCol.ButtonHovered] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 0.8600000143051147);
            style.Colors[(int)ImGuiCol.ButtonActive] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 1.0);
            style.Colors[(int)ImGuiCol.Header] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 0.7599999904632568);
            style.Colors[(int)ImGuiCol.HeaderHovered] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 0.8600000143051147);
            style.Colors[(int)ImGuiCol.HeaderActive] = ImVec4(0.501960813999176, 0.07450980693101883, 0.2549019753932953, 1.0);
            style.Colors[(int)ImGuiCol.Separator] = ImVec4(0.4274509847164154, 0.4274509847164154, 0.4980392158031464, 0.5);
            style.Colors[(int)ImGuiCol.SeparatorHovered] = ImVec4(0.09803921729326248, 0.4000000059604645, 0.7490196228027344, 0.7799999713897705);
            style.Colors[(int)ImGuiCol.SeparatorActive] = ImVec4(0.09803921729326248, 0.4000000059604645, 0.7490196228027344, 1.0);
            style.Colors[(int)ImGuiCol.ResizeGrip] = ImVec4(0.4666666686534882, 0.7686274647712708, 0.8274509906768799, 0.03999999910593033);
            style.Colors[(int)ImGuiCol.ResizeGripHovered] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 0.7799999713897705);
            style.Colors[(int)ImGuiCol.ResizeGripActive] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 1.0);
            style.Colors[(int)ImGuiCol.Tab] = ImVec4(0.1764705926179886, 0.3490196168422699, 0.5764706134796143, 0.8619999885559082);
            style.Colors[(int)ImGuiCol.TabHovered] = ImVec4(0.2588235437870026, 0.5882353186607361, 0.9764705896377563, 0.800000011920929);
            style.Colors[(int)ImGuiCol.TabActive] = ImVec4(0.196078434586525, 0.407843142747879, 0.6784313917160034, 1.0);
            style.Colors[(int)ImGuiCol.TabUnfocused] = ImVec4(0.06666667014360428, 0.1019607856869698, 0.1450980454683304, 0.9724000096321106);
            style.Colors[(int)ImGuiCol.TabUnfocusedActive] = ImVec4(0.1333333402872086, 0.2588235437870026, 0.4235294163227081, 1.0);
            style.Colors[(int)ImGuiCol.PlotLines] = ImVec4(0.8588235378265381, 0.929411768913269, 0.886274516582489, 0.6299999952316284);
            style.Colors[(int)ImGuiCol.PlotLinesHovered] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 1.0);
            style.Colors[(int)ImGuiCol.PlotHistogram] = ImVec4(0.8588235378265381, 0.929411768913269, 0.886274516582489, 0.6299999952316284);
            style.Colors[(int)ImGuiCol.PlotHistogramHovered] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 1.0);
            style.Colors[(int)ImGuiCol.TableHeaderBg] = ImVec4(0.1882352977991104, 0.1882352977991104, 0.2000000029802322, 1.0);
            style.Colors[(int)ImGuiCol.TableBorderStrong] = ImVec4(0.3098039329051971, 0.3098039329051971, 0.3490196168422699, 1.0);
            style.Colors[(int)ImGuiCol.TableBorderLight] = ImVec4(0.2274509817361832, 0.2274509817361832, 0.2470588237047195, 1.0);
            style.Colors[(int)ImGuiCol.TableRowBg] = ImVec4(0.0, 0.0, 0.0, 0.0);
            style.Colors[(int)ImGuiCol.TableRowBgAlt] = ImVec4(1.0, 1.0, 1.0, 0.05999999865889549);
            style.Colors[(int)ImGuiCol.TextSelectedBg] = ImVec4(0.4549019634723663, 0.196078434586525, 0.2980392277240753, 0.4300000071525574);
            style.Colors[(int)ImGuiCol.DragDropTarget] = ImVec4(1.0, 1.0, 0.0, 0.8999999761581421);
            style.Colors[(int)ImGuiCol.NavHighlight] = ImVec4(0.2588235437870026, 0.5882353186607361, 0.9764705896377563, 1.0);
            style.Colors[(int)ImGuiCol.NavWindowingHighlight] = ImVec4(1.0, 1.0, 1.0, 0.699999988079071);
            style.Colors[(int)ImGuiCol.NavWindowingDimBg] = ImVec4(0.800000011920929, 0.800000011920929, 0.800000011920929, 0.2000000029802322);
            style.Colors[(int)ImGuiCol.ModalWindowDimBg] = ImVec4(0.800000011920929, 0.800000011920929, 0.800000011920929, 0.3499999940395355);
        }

        public static void Photoshop()
        {
            ImGuiStylePtr style = ImGui.GetStyle();

            style.Alpha = 1.0f;
            style.DisabledAlpha = 0.6000000238418579f;
            style.WindowPadding = ImVec2(8.0, 8.0);
            style.WindowRounding = 4.0f;
            style.WindowBorderSize = 1.0f;
            style.WindowMinSize = ImVec2(32.0, 32.0);
            style.WindowTitleAlign = ImVec2(0.0, 0.5);
            style.WindowMenuButtonPosition = ImGuiDir.Left;
            style.ChildRounding = 4.0f;
            style.ChildBorderSize = 1.0f;
            style.PopupRounding = 2.0f;
            style.PopupBorderSize = 1.0f;
            style.FramePadding = ImVec2(4.0, 3.0);
            style.FrameRounding = 2.0f;
            style.FrameBorderSize = 1.0f;
            style.ItemSpacing = ImVec2(8.0, 4.0);
            style.ItemInnerSpacing = ImVec2(4.0, 4.0);
            style.CellPadding = ImVec2(4.0, 2.0);
            style.IndentSpacing = 21.0f;
            style.ColumnsMinSpacing = 6.0f;
            style.ScrollbarSize = 13.0f;
            style.ScrollbarRounding = 12.0f;
            style.GrabMinSize = 7.0f;
            style.GrabRounding = 0.0f;
            style.TabRounding = 0.0f;
            style.TabBorderSize = 1.0f;
            style.TabMinWidthForCloseButton = 0.0f;
            style.ColorButtonPosition = ImGuiDir.Right;
            style.ButtonTextAlign = ImVec2(0.5, 0.5);
            style.SelectableTextAlign = ImVec2(0.0, 0.0);

            style.Colors[(int)ImGuiCol.Text] = ImVec4(1.0, 1.0, 1.0, 1.0);
            style.Colors[(int)ImGuiCol.TextDisabled] = ImVec4(0.4980392158031464, 0.4980392158031464, 0.4980392158031464, 1.0);
            style.Colors[(int)ImGuiCol.WindowBg] = ImVec4(0.1764705926179886, 0.1764705926179886, 0.1764705926179886, 1.0);
            style.Colors[(int)ImGuiCol.ChildBg] = ImVec4(0.2784313857555389, 0.2784313857555389, 0.2784313857555389, 0.0);
            style.Colors[(int)ImGuiCol.PopupBg] = ImVec4(0.3098039329051971, 0.3098039329051971, 0.3098039329051971, 1.0);
            style.Colors[(int)ImGuiCol.Border] = ImVec4(0.2627451121807098, 0.2627451121807098, 0.2627451121807098, 1.0);
            style.Colors[(int)ImGuiCol.BorderShadow] = ImVec4(0.0, 0.0, 0.0, 0.0);
            style.Colors[(int)ImGuiCol.FrameBg] = ImVec4(0.1568627506494522, 0.1568627506494522, 0.1568627506494522, 1.0);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = ImVec4(0.2000000029802322, 0.2000000029802322, 0.2000000029802322, 1.0);
            style.Colors[(int)ImGuiCol.FrameBgActive] = ImVec4(0.2784313857555389, 0.2784313857555389, 0.2784313857555389, 1.0);
            style.Colors[(int)ImGuiCol.TitleBg] = ImVec4(0.1450980454683304, 0.1450980454683304, 0.1450980454683304, 1.0);
            style.Colors[(int)ImGuiCol.TitleBgActive] = ImVec4(0.1450980454683304, 0.1450980454683304, 0.1450980454683304, 1.0);
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = ImVec4(0.1450980454683304, 0.1450980454683304, 0.1450980454683304, 1.0);
            style.Colors[(int)ImGuiCol.MenuBarBg] = ImVec4(0.1921568661928177, 0.1921568661928177, 0.1921568661928177, 1.0);
            style.Colors[(int)ImGuiCol.ScrollbarBg] = ImVec4(0.1568627506494522, 0.1568627506494522, 0.1568627506494522, 1.0);
            style.Colors[(int)ImGuiCol.ScrollbarGrab] = ImVec4(0.2745098173618317, 0.2745098173618317, 0.2745098173618317, 1.0);
            style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = ImVec4(0.2980392277240753, 0.2980392277240753, 0.2980392277240753, 1.0);
            style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = ImVec4(1.0, 0.3882353007793427, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.CheckMark] = ImVec4(1.0, 1.0, 1.0, 1.0);
            style.Colors[(int)ImGuiCol.SliderGrab] = ImVec4(0.3882353007793427, 0.3882353007793427, 0.3882353007793427, 1.0);
            style.Colors[(int)ImGuiCol.SliderGrabActive] = ImVec4(1.0, 0.3882353007793427, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.Button] = ImVec4(1.0, 1.0, 1.0, 0.0);
            style.Colors[(int)ImGuiCol.ButtonHovered] = ImVec4(1.0, 1.0, 1.0, 0.1560000032186508);
            style.Colors[(int)ImGuiCol.ButtonActive] = ImVec4(1.0, 1.0, 1.0, 0.3910000026226044);
            style.Colors[(int)ImGuiCol.Header] = ImVec4(0.3098039329051971, 0.3098039329051971, 0.3098039329051971, 1.0);
            style.Colors[(int)ImGuiCol.HeaderHovered] = ImVec4(0.4666666686534882, 0.4666666686534882, 0.4666666686534882, 1.0);
            style.Colors[(int)ImGuiCol.HeaderActive] = ImVec4(0.4666666686534882, 0.4666666686534882, 0.4666666686534882, 1.0);
            style.Colors[(int)ImGuiCol.Separator] = ImVec4(0.2627451121807098, 0.2627451121807098, 0.2627451121807098, 1.0);
            style.Colors[(int)ImGuiCol.SeparatorHovered] = ImVec4(0.3882353007793427, 0.3882353007793427, 0.3882353007793427, 1.0);
            style.Colors[(int)ImGuiCol.SeparatorActive] = ImVec4(1.0, 0.3882353007793427, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.ResizeGrip] = ImVec4(1.0, 1.0, 1.0, 0.25);
            style.Colors[(int)ImGuiCol.ResizeGripHovered] = ImVec4(1.0, 1.0, 1.0, 0.6700000166893005);
            style.Colors[(int)ImGuiCol.ResizeGripActive] = ImVec4(1.0, 0.3882353007793427, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.Tab] = ImVec4(0.09411764889955521, 0.09411764889955521, 0.09411764889955521, 1.0);
            style.Colors[(int)ImGuiCol.TabHovered] = ImVec4(0.3490196168422699, 0.3490196168422699, 0.3490196168422699, 1.0);
            style.Colors[(int)ImGuiCol.TabActive] = ImVec4(0.1921568661928177, 0.1921568661928177, 0.1921568661928177, 1.0);
            style.Colors[(int)ImGuiCol.TabUnfocused] = ImVec4(0.09411764889955521, 0.09411764889955521, 0.09411764889955521, 1.0);
            style.Colors[(int)ImGuiCol.TabUnfocusedActive] = ImVec4(0.1921568661928177, 0.1921568661928177, 0.1921568661928177, 1.0);
            style.Colors[(int)ImGuiCol.PlotLines] = ImVec4(0.4666666686534882, 0.4666666686534882, 0.4666666686534882, 1.0);
            style.Colors[(int)ImGuiCol.PlotLinesHovered] = ImVec4(1.0, 0.3882353007793427, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.PlotHistogram] = ImVec4(0.5843137502670288, 0.5843137502670288, 0.5843137502670288, 1.0);
            style.Colors[(int)ImGuiCol.PlotHistogramHovered] = ImVec4(1.0, 0.3882353007793427, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.TableHeaderBg] = ImVec4(0.1882352977991104, 0.1882352977991104, 0.2000000029802322, 1.0);
            style.Colors[(int)ImGuiCol.TableBorderStrong] = ImVec4(0.3098039329051971, 0.3098039329051971, 0.3490196168422699, 1.0);
            style.Colors[(int)ImGuiCol.TableBorderLight] = ImVec4(0.2274509817361832, 0.2274509817361832, 0.2470588237047195, 1.0);
            style.Colors[(int)ImGuiCol.TableRowBg] = ImVec4(0.0, 0.0, 0.0, 0.0);
            style.Colors[(int)ImGuiCol.TableRowBgAlt] = ImVec4(1.0, 1.0, 1.0, 0.05999999865889549);
            style.Colors[(int)ImGuiCol.TextSelectedBg] = ImVec4(1.0, 1.0, 1.0, 0.1560000032186508);
            style.Colors[(int)ImGuiCol.DragDropTarget] = ImVec4(1.0, 0.3882353007793427, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.NavHighlight] = ImVec4(1.0, 0.3882353007793427, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.NavWindowingHighlight] = ImVec4(1.0, 0.3882353007793427, 0.0, 1.0);
            style.Colors[(int)ImGuiCol.NavWindowingDimBg] = ImVec4(0.0, 0.0, 0.0, 0.5860000252723694);
            style.Colors[(int)ImGuiCol.ModalWindowDimBg] = ImVec4(0.0, 0.0, 0.0, 0.5860000252723694);
        }

        private static Vector4 ImVec4(double x, double y, double z, double w) => new Vector4((float)x, (float)y, (float)z, (float)w);
        private static Vector2 ImVec2(double x, double y) => new Vector2((float)x, (float)y);
    }

    public readonly struct ImGuiStyle
    {
        public readonly float Alpha;
        public readonly float DisabledAlpha;
        public readonly Vector2 WindowPadding;
        public readonly float WindowRounding;
        public readonly float WindowBorderSize;
        public readonly Vector2 WindowMinSize;
        public readonly Vector2 WindowTitleAlign;
        public readonly ImGuiDir WindowMenuButtonPosition;
        public readonly float ChildRounding;
        public readonly float ChildBorderSize;
        public readonly float PopupRounding;
        public readonly float PopupBorderSize;
        public readonly Vector2 FramePadding;
        public readonly float FrameRounding;
        public readonly float FrameBorderSize;
        public readonly Vector2 ItemSpacing;
        public readonly Vector2 ItemInnerSpacing;
        public readonly Vector2 CellPadding;
        public readonly Vector2 TouchExtraPadding;
        public readonly float IndentSpacing;
        public readonly float ColumnsMinSpacing;
        public readonly float ScrollbarSize;
        public readonly float ScrollbarRounding;
        public readonly float GrabMinSize;
        public readonly float GrabRounding;
        public readonly float LogSliderDeadzone;
        public readonly float TabRounding;
        public readonly float TabBorderSize;
        public readonly float TabMinWidthForCloseButton;
        public readonly ImGuiDir ColorButtonPosition;
        public readonly Vector2 ButtonTextAlign;
        public readonly Vector2 SelectableTextAlign;
        public readonly Vector2 DisplayWindowPadding;
        public readonly Vector2 DisplaySafeAreaPadding;
        public readonly float MouseCursorScale;
        public readonly bool AntiAliasedLines;
        public readonly bool AntiAliasedLinesUseTex;
        public readonly bool AntiAliasedFill;
        public readonly float CurveTessellationTol;
        public readonly float CircleTessellationMaxError;

        public ImGuiStyle(ImGuiStylePtr ptr)
        {
            this.WindowBorderSize = ptr.WindowBorderSize;
            this.WindowTitleAlign = CopyVec(ptr.WindowTitleAlign);
            this.WindowRounding = ptr.WindowRounding;
            this.WindowPadding = CopyVec(ptr.WindowPadding);
            this.WindowMinSize = CopyVec(ptr.WindowMinSize);
            this.WindowMenuButtonPosition = ptr.WindowMenuButtonPosition;
            this.TouchExtraPadding = CopyVec(ptr.TouchExtraPadding);
            this.TabRounding = ptr.TabRounding;
            this.TabMinWidthForCloseButton = ptr.TabMinWidthForCloseButton;
            this.TabBorderSize = ptr.TabBorderSize;
            this.SelectableTextAlign = CopyVec(ptr.SelectableTextAlign);
            this.ScrollbarSize = ptr.ScrollbarSize;
            this.ScrollbarRounding = ptr.ScrollbarRounding;
            this.PopupRounding = ptr.PopupRounding;
            this.PopupBorderSize = ptr.PopupBorderSize;
            this.MouseCursorScale = ptr.MouseCursorScale;
            this.LogSliderDeadzone = ptr.LogSliderDeadzone;
            this.ItemSpacing = CopyVec(ptr.ItemSpacing);
            this.ItemInnerSpacing = CopyVec(ptr.ItemInnerSpacing);
            this.IndentSpacing = ptr.IndentSpacing;
            this.GrabRounding = ptr.GrabRounding;
            this.GrabMinSize = ptr.GrabMinSize;
            this.FrameRounding = ptr.FrameRounding;
            this.FramePadding = CopyVec(ptr.FramePadding);
            this.FrameBorderSize = ptr.FrameBorderSize;
            this.DisplayWindowPadding = CopyVec(ptr.DisplayWindowPadding);
            this.DisplaySafeAreaPadding = CopyVec(ptr.DisplaySafeAreaPadding);
            this.DisabledAlpha = ptr.DisabledAlpha;
            this.CurveTessellationTol = ptr.CurveTessellationTol;
            this.ColumnsMinSpacing = ptr.ColumnsMinSpacing;
            this.ColorButtonPosition = ptr.ColorButtonPosition;
            this.CircleTessellationMaxError = ptr.CircleTessellationMaxError;
            this.ChildRounding = ptr.ChildRounding;
            this.CellPadding = CopyVec(ptr.CellPadding);
            this.ChildBorderSize = ptr.ChildBorderSize;
            this.ButtonTextAlign = CopyVec(ptr.ButtonTextAlign);
            this.AntiAliasedLinesUseTex = ptr.AntiAliasedLinesUseTex;
            this.AntiAliasedLines = ptr.AntiAliasedLines;
            this.AntiAliasedFill = ptr.AntiAliasedFill;
            this.Alpha = ptr.Alpha;
        }

        public void Apply(ImGuiStylePtr to)
        {
            to.Alpha = this.Alpha;
            to.AntiAliasedFill = this.AntiAliasedFill;
            to.AntiAliasedLines = this.AntiAliasedLines;
            to.AntiAliasedLinesUseTex = this.AntiAliasedLinesUseTex;
            to.ButtonTextAlign = CopyVec(this.ButtonTextAlign);
            to.ChildBorderSize = this.ChildBorderSize;
            to.CellPadding = CopyVec(this.CellPadding);
            to.ChildRounding = this.ChildRounding;
            to.CircleTessellationMaxError = this.CircleTessellationMaxError;
            to.ColorButtonPosition = this.ColorButtonPosition;
            to.ColumnsMinSpacing = this.ColumnsMinSpacing;
            to.CurveTessellationTol = this.CurveTessellationTol;
            to.DisabledAlpha = this.DisabledAlpha;
            to.DisplaySafeAreaPadding = CopyVec(this.DisplaySafeAreaPadding);
            to.DisplayWindowPadding = CopyVec(this.DisplayWindowPadding);
            to.FrameBorderSize = this.FrameBorderSize;
            to.FramePadding = CopyVec(this.FramePadding);
            to.FrameRounding = this.FrameRounding;
            to.GrabMinSize = this.GrabMinSize;
            to.GrabRounding = this.GrabRounding;
            to.IndentSpacing = this.IndentSpacing;
            to.ItemInnerSpacing = CopyVec(this.ItemInnerSpacing);
            to.ItemSpacing = CopyVec(this.ItemSpacing);
            to.LogSliderDeadzone = this.LogSliderDeadzone;
            to.MouseCursorScale = this.MouseCursorScale;
            to.PopupBorderSize = this.PopupBorderSize;
            to.PopupRounding = this.PopupRounding;
            to.ScrollbarRounding = this.ScrollbarRounding;
            to.ScrollbarSize = this.ScrollbarSize;
            to.SelectableTextAlign = CopyVec(this.SelectableTextAlign);
            to.TabBorderSize = this.TabBorderSize;
            to.TabMinWidthForCloseButton = this.TabMinWidthForCloseButton;
            to.TabRounding = this.TabRounding;
            to.TouchExtraPadding = CopyVec(this.TouchExtraPadding);
            to.WindowMenuButtonPosition = this.WindowMenuButtonPosition;
            to.WindowMinSize = CopyVec(this.WindowMinSize);
            to.WindowPadding = CopyVec(this.WindowPadding);
            to.WindowRounding = this.WindowRounding;
            to.WindowTitleAlign = CopyVec(this.WindowTitleAlign);
            to.WindowBorderSize = this.WindowBorderSize;
        }

        private static Vector2 CopyVec(Vector2 vIn) => new Vector2(vIn.X, vIn.Y);
    }
}
