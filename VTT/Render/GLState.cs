namespace VTT.Render
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using VTT.GL.Bindings;

    public static class GLState
    {
        private static List<IStateParameter> commitalState = new List<IStateParameter>();
        private static StateEntry<T> RegisterState<T>(StateEntry<T> state)
        {
            if (!state._instant)
            {
                commitalState.Add(state);
            }

            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SwitchCapability(bool context, Capability cap)
        {
            if (context)
            {
                GL.Enable(cap);
            }
            else
            {
                GL.Disable(cap);
            }
        }

        public static StateEntry<bool> DepthTest { get; } = RegisterState(new StateEntry<bool>(false, x => SwitchCapability(x, Capability.DepthTest), (l, r) => l == r, false));
        public static StateEntry<ComparisonMode> DepthFunc { get; } = RegisterState(new StateEntry<ComparisonMode>(ComparisonMode.Less, x => GL.DepthFunction(x), (l, r) => l == r, false));
        public static StateEntry<bool> CullFace { get; } = RegisterState(new StateEntry<bool>(false, x => SwitchCapability(x, Capability.CullFace), (l, r) => l == r, false));
        public static StateEntry<PolygonFaceMode> CullFaceMode { get; } = RegisterState(new StateEntry<PolygonFaceMode>(PolygonFaceMode.Back, x => GL.CullFace(x), (l, r) => l == r, false));
        public static StateEntry<bool> Blend { get; } = RegisterState(new StateEntry<bool>(false, x => SwitchCapability(x, Capability.Blend), (l, r) => l == r, false));
        public static StateEntry<(BlendingFactor, BlendingFactor)> BlendFunc { get; } = RegisterState(new StateEntry<(BlendingFactor, BlendingFactor)>((BlendingFactor.One, BlendingFactor.Zero), x => GL.BlendFunc(x.Item1, x.Item2), (l, r) => l == r, false));
        public static StateEntry<bool> Scissor { get; } = RegisterState(new StateEntry<bool>(false, x => SwitchCapability(x, Capability.ScissorTest), (l, r) => l == r, false));
        public static StateEntry<bool> Multisample { get; } = RegisterState(new StateEntry<bool>(true, x => SwitchCapability(x, Capability.Multisample), (l, r) => l == r, false));
        public static StateEntry<bool> SampleAlphaToCoverage { get; } = RegisterState(new StateEntry<bool>(false, x => SwitchCapability(x, Capability.SampleAlphaToCoverage), (l, r) => l == r, false));
        public static StateEntry<bool> DepthMask { get; } = RegisterState(new StateEntry<bool>(true, x => GL.DepthMask(x), (l, r) => l == r, false));
        public static StateEntry<uint> ActiveTexture { get; } = RegisterState(new StateEntry<uint>(0, x => GL.ActiveTexture(x), (l, r) => l == r, true));

        public static void DrawArrays(PrimitiveType type, int first, int count)
        {
            foreach (IStateParameter state in commitalState)
            {
                state.Commit();
            }

            GL.DrawArrays(type, first, count);
        }

        public static void DrawElements(PrimitiveType type, int amt, ElementsType elements, nint offset)
        {
            foreach (IStateParameter state in commitalState)
            {
                state.Commit();
            }

            GL.DrawElements(type, amt, elements, offset);
        }

        public static void DrawElementsBaseVertex(PrimitiveType mode, int count, ElementsType type, nint indicesOffset, int baseVertex)
        {
            foreach (IStateParameter state in commitalState)
            {
                state.Commit();
            }

            GL.DrawElementsBaseVertex(mode, count, type, indicesOffset, baseVertex);
        }

        public static void DrawElementsInstanced(PrimitiveType mode, int count, ElementsType type, nint indicesOffset, int numInstances)
        {
            foreach (IStateParameter state in commitalState)
            {
                state.Commit();
            }

            GL.DrawElementsInstanced(mode, count, type, indicesOffset, numInstances);
        }

        public static void Clear(ClearBufferMask mask)
        {
            foreach (IStateParameter state in commitalState)
            {
                state.Commit();
            }

            GL.Clear(mask);
        }

        public interface IStateParameter
        {
            void Commit();
        }

        public class StateEntry<T> : IStateParameter
        {
            private T _valueNow;
            private T _valueGLState;
            private bool _valueChanged;
            private Action<T> _committer;
            private Func<T, T, bool> _comparer;
            internal bool _instant;

            public StateEntry(T valueDefault, Action<T> committer, Func<T, T, bool> comparer, bool instant)
            {
                this._valueNow = valueDefault;
                this._valueGLState = valueDefault;
                this._committer = committer;
                this._comparer = comparer;
                this._instant = instant;
            }

            public void Set(T state)
            {
                this._valueNow = state;
                this._valueChanged = true;
                if (this._instant)
                {
                    this.Commit();
                }
            }

            public T Current => this._valueNow;

            public void Commit()
            {
                if (this._valueChanged)
                {
                    if (!this._comparer(this._valueNow, this._valueGLState))
                    {
                        this._valueGLState = this._valueNow;
                        this._committer(this._valueGLState);
                    }

                    this._valueChanged = false;
                }
            }
        }
    }
}
