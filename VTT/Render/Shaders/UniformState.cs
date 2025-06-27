namespace VTT.Render.Shaders
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using VTT.GL;
    using VTT.GL.Bindings;
    using VTT.Util;

    public class UniformState<T> where T : unmanaged, IEquatable<T>
    {
        public static UniformState<T> Invalid { get; } = new UniformState<T>();

        private readonly bool _isArray;
        private readonly bool _isValid;
        private PrimitiveDataUnion _state = new PrimitiveDataUnion();
        private Matrix4x4 _m4State = new Matrix4x4();
        private readonly PrimitiveDataUnion[] _stateArray;
        private readonly Matrix4x4[] _m4StateArray;
        private readonly Action<T> _setter;
        private readonly SetterArray _setterArray;
        private readonly bool _checkValue;
        private readonly UniformWrapper _uniform;
        private readonly UniformWrapper[] _uniformArray;

        public int MachineUnitsNominalUsage { get; }
        public int MachineUnitsWorstCaseUsage { get; }
        public int UniformSlotsTaken { get; }

        private delegate void SetterArray(Span<T> span, int offset);

        private UniformState()
        {
            this._isArray = false;
            this._isValid = false;
            this._checkValue = false;
            this._setter = x => { };
            this._setterArray = (x, o) => { };
        }

        public UniformState(ShaderProgram prog, string name, bool isArray, bool checkValue)
        {
            this._isArray = isArray;
            this._checkValue = checkValue;
            bool isMatrix = typeof(T) == typeof(Matrix4x4);
            int elementAmount = 0;
            if (isArray)
            {
                List<UniformWrapper> wrapperArray = new List<UniformWrapper>();
                int counter = 0;
                while (true)
                {
                    UniformWrapper uw = prog.UniformManager.GetUniform($"{name}[{counter++}]");
                    if (!uw.Valid)
                    {
                        break;
                    }
                    else
                    {
                        wrapperArray.Add(uw);
                    }
                }

                this._isValid = wrapperArray.Count > 0;
                if (this._checkValue)
                {
                    if (isMatrix)
                    {
                        this._m4StateArray = new Matrix4x4[counter];

                        // So here we have a problem - mat4 uniforms differ per vendor - amd seems to set the defaults to [0,0,0,0],[0,0,0,0],[0,0,0,0],[0,0,0,0], nvidia seems to set the defaults to [1,0,0,0],[0,1,0,0],[0,0,1,0],[0,0,0,1], intel differs per product
                        // Set it to empty explicitly, bc an empty (zeroed out) matrix is the least likely to be used in a shader legitimately
                        Array.Fill(this._m4StateArray, new Matrix4x4());
                    }
                    else
                    {
                        this._stateArray = new PrimitiveDataUnion[counter];
                        Array.Fill(this._stateArray, new PrimitiveDataUnion());
                    }
                }

                this._uniformArray = wrapperArray.ToArray();
                elementAmount = counter;
            }
            else
            {
                elementAmount = 1;
                this._uniform = prog.UniformManager.GetUniform(name);
                this._isValid = this._uniform.Valid;
                if (this._checkValue)
                {
                    if (isMatrix)
                    {
                        this._m4State = new Matrix4x4();
                    }
                    else
                    {
                        this._state = new PrimitiveDataUnion();
                    }
                }
            }

            if (this._isValid)
            {
                Type ttype = typeof(T);
                if (ttype == typeof(float))
                {
                    this._setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), this, nameof(SetFloat));
                    this._setterArray = (SetterArray)Delegate.CreateDelegate(typeof(SetterArray), this, nameof(SetFloatV));
                    this.MachineUnitsNominalUsage = sizeof(float) * elementAmount;
                    this.MachineUnitsWorstCaseUsage = (this._isArray ? (sizeof(float) * 4) : sizeof(float)) * elementAmount;
                    this.UniformSlotsTaken = elementAmount;
                    return;
                }

                if (ttype == typeof(uint))
                {
                    this._setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), this, nameof(SetUint));
                    this._setterArray = (SetterArray)Delegate.CreateDelegate(typeof(SetterArray), this, nameof(SetUintV));
                    this.MachineUnitsNominalUsage = sizeof(uint) * elementAmount;
                    this.MachineUnitsWorstCaseUsage = (this._isArray ? (sizeof(float) * 4) : sizeof(uint)) * elementAmount;
                    this.UniformSlotsTaken = elementAmount;
                    return;
                }

                if (ttype == typeof(int))
                {
                    this._setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), this, nameof(SetInt));
                    this._setterArray = (SetterArray)Delegate.CreateDelegate(typeof(SetterArray), this, nameof(SetIntV));
                    this.MachineUnitsNominalUsage = sizeof(int) * elementAmount;
                    this.MachineUnitsWorstCaseUsage = (this._isArray ? (sizeof(float) * 4) : sizeof(int)) * elementAmount;
                    this.UniformSlotsTaken = elementAmount;
                    return;
                }

                if (ttype == typeof(bool))
                {
                    this._setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), this, nameof(SetBool));
                    this._setterArray = (SetterArray)Delegate.CreateDelegate(typeof(SetterArray), this, nameof(SetBoolV));
                    this.MachineUnitsNominalUsage = sizeof(int) * elementAmount;
                    this.MachineUnitsWorstCaseUsage = (this._isArray ? (sizeof(float) * 4) : sizeof(int)) * elementAmount;
                    this.UniformSlotsTaken = elementAmount;
                    return;
                }

                if (ttype == typeof(Vector2))
                {
                    this._setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), this, nameof(SetVec2));
                    this._setterArray = (SetterArray)Delegate.CreateDelegate(typeof(SetterArray), this, nameof(SetVec2V));
                    this.MachineUnitsNominalUsage = sizeof(float) * 2 * elementAmount;
                    this.MachineUnitsWorstCaseUsage = (this._isArray ? (sizeof(float) * 4) : (sizeof(float) * 2)) * elementAmount;
                    this.UniformSlotsTaken = elementAmount;
                    return;
                }

                if (ttype == typeof(Vector3))
                {
                    this._setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), this, nameof(SetVec3));
                    this._setterArray = (SetterArray)Delegate.CreateDelegate(typeof(SetterArray), this, nameof(SetVec3V));
                    this.MachineUnitsNominalUsage = sizeof(float) * 3 * elementAmount;
                    this.MachineUnitsWorstCaseUsage = sizeof(float) * 4 * elementAmount;
                    this.UniformSlotsTaken = elementAmount;
                    return;
                }

                if (ttype == typeof(Vector4))
                {
                    this._setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), this, nameof(SetVec4));
                    this._setterArray = (SetterArray)Delegate.CreateDelegate(typeof(SetterArray), this, nameof(SetVec4V));
                    this.MachineUnitsNominalUsage = sizeof(float) * 4 * elementAmount;
                    this.MachineUnitsWorstCaseUsage = sizeof(float) * 4 * elementAmount;
                    this.UniformSlotsTaken = elementAmount;
                    return;
                }

                if (ttype == typeof(Matrix4x4))
                {
                    this._setter = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), this, nameof(SetMat4));
                    this._setterArray = (SetterArray)Delegate.CreateDelegate(typeof(SetterArray), this, nameof(SetMat4V));
                    this.MachineUnitsNominalUsage = sizeof(float) * 16 * elementAmount;
                    this.MachineUnitsWorstCaseUsage = sizeof(float) * 16 * elementAmount;
                    this.UniformSlotsTaken = elementAmount * 4;
                    return;
                }

                throw new NotSupportedException($"Type {ttype} is not supported by opengl!");
            }
            else
            {
                this._setter = x => { };
                this._setterArray = (x, i) => { };
            }
        }

        public void Set(T t)
        {
            if (!this._isValid)
            {
                return;
            }

            if (this._isArray)
            {
                this._setterArray(MemoryMarshal.CreateSpan(ref t, 1), 0);
            }
            else
            {
                this._setter(t);
            }
        }

        public void Set(T t, int offset)
        {
            if (!this._isValid)
            {
                return;
            }

            if (this._isArray)
            {
                this._setterArray(MemoryMarshal.CreateSpan(ref t, 1), offset);
            }
            else
            {
                this._setter(t);
            }
        }

        public void Set(Span<T> span, int offset)
        {
            if (!this._isValid || span.Length == 0)
            {
                return;
            }

            if (this._isArray)
            {
                this._setterArray(span, offset);
            }
            else
            {
                this._setter(span[0]);
            }
        }

        private void SetFloat(float f)
        {
            if (this._checkValue)
            {
                if (this._state.fVal != f)
                {
                    this._state.fVal = f;
                    this._uniform.Set(f);
                }
            }
            else
            {
                this._uniform.Set(f);
            }
        }

        private void SetFloatV(Span<float> array, int offset)
        {
            if (this._checkValue)
            {
                bool anyDiff = false;
                for (int i = offset; i < Math.Min(array.Length, this._stateArray.Length); ++i)
                {
                    if (this._stateArray[i].fVal != array[i])
                    {
                        anyDiff = true;
                        this._stateArray[i].fVal = array[i];
                    }
                }

                if (anyDiff)
                {
                    GL.Uniform((int)this._uniformArray[offset], array);
                }
            }
        }

        private void SetUint(uint ui)
        {
            if (this._checkValue)
            {
                if (this._state.uiVal != ui)
                {
                    this._state.uiVal = ui;
                    this._uniform.Set(ui);
                }
            }
            else
            {
                this._uniform.Set(ui);
            }
        }

        private void SetUintV(Span<uint> array, int offset)
        {
            if (this._checkValue)
            {
                bool anyDiff = false;
                for (int i = offset; i < Math.Min(array.Length, this._stateArray.Length); ++i)
                {
                    if (this._stateArray[i].uiVal != array[i])
                    {
                        anyDiff = true;
                        this._stateArray[i].uiVal = array[i];
                    }
                }

                if (anyDiff)
                {
                    GL.Uniform((int)this._uniformArray[offset], array);
                }
            }
        }

        private void SetInt(int i)
        {
            if (this._checkValue)
            {
                if (this._state.iVal != i)
                {
                    this._state.iVal = i;
                    this._uniform.Set(i);
                }
            }
            else
            {
                this._uniform.Set(i);
            }
        }

        private void SetIntV(Span<int> array, int offset)
        {
            if (this._checkValue)
            {
                bool anyDiff = false;
                for (int i = offset; i < Math.Min(array.Length, this._stateArray.Length); ++i)
                {
                    if (this._stateArray[i].iVal != array[i])
                    {
                        anyDiff = true;
                        this._stateArray[i].iVal = array[i];
                    }
                }

                if (anyDiff)
                {
                    GL.Uniform((int)this._uniformArray[offset], array);
                }
            }
        }

        private void SetBool(bool b) => this.SetInt(b ? 1 : 0);

        private void SetBoolV(Span<bool> array, int offset)
        {
            if (this._checkValue)
            {
                bool anyDiff = false;
                for (int i = offset; i < Math.Min(array.Length, this._stateArray.Length); ++i)
                {
                    int ival = array[i] ? 1 : 0;
                    if (this._stateArray[i].iVal != ival)
                    {
                        anyDiff = true;
                        this._stateArray[i].iVal = ival;
                    }
                }

                if (anyDiff)
                {
                    GL.Uniform((int)this._uniformArray[offset], array);
                }
            }
        }

        private void SetVec2(Vector2 v2)
        {
            if (this._checkValue)
            {
                if (this._state.v2Val != v2)
                {
                    this._state.v2Val = v2;
                    this._uniform.Set(v2);
                }
            }
            else
            {
                this._uniform.Set(v2);
            }
        }

        private void SetVec2V(Span<Vector2> array, int offset)
        {
            if (this._checkValue)
            {
                bool anyDiff = false;
                for (int i = offset; i < Math.Min(array.Length, this._stateArray.Length); ++i)
                {
                    if (this._stateArray[i].v2Val != array[i])
                    {
                        anyDiff = true;
                        this._stateArray[i].v2Val = array[i];
                    }
                }

                if (anyDiff)
                {
                    GL.Uniform((int)this._uniformArray[offset], array);
                }
            }
        }

        private void SetVec3(Vector3 v3)
        {
            if (this._checkValue)
            {
                if (this._state.v3Val != v3)
                {
                    this._state.v3Val = v3;
                    this._uniform.Set(v3);
                }
            }
            else
            {
                this._uniform.Set(v3);
            }
        }

        private void SetVec3V(Span<Vector3> array, int offset)
        {
            if (this._checkValue)
            {
                bool anyDiff = false;
                for (int i = offset; i < Math.Min(array.Length, this._stateArray.Length); ++i)
                {
                    if (this._stateArray[i].v3Val != array[i])
                    {
                        anyDiff = true;
                        this._stateArray[i].v3Val = array[i];
                    }
                }

                if (anyDiff)
                {
                    GL.Uniform((int)this._uniformArray[offset], array);
                }
            }
        }

        private void SetVec4(Vector4 v4)
        {
            if (this._checkValue)
            {
                if (this._state.v4Val != v4)
                {
                    this._state.v4Val = v4;
                    this._uniform.Set(v4);
                }
            }
            else
            {
                this._uniform.Set(v4);
            }
        }

        private void SetVec4V(Span<Vector4> array, int offset)
        {
            if (this._checkValue)
            {
                bool anyDiff = false;
                for (int i = offset; i < Math.Min(array.Length, this._stateArray.Length); ++i)
                {
                    if (this._stateArray[i].v4Val != array[i])
                    {
                        anyDiff = true;
                        this._stateArray[i].v4Val = array[i];
                    }
                }

                if (anyDiff)
                {
                    GL.Uniform((int)this._uniformArray[offset], array);
                }
            }
        }

        private void SetMat4(Matrix4x4 m4)
        {
            if (this._checkValue)
            {
                if (this._m4State != m4)
                {
                    this._m4State = m4;
                    this._uniform.Set(m4);
                }
            }
            else
            {
                this._uniform.Set(m4);
            }
        }

        private void SetMat4V(Span<Matrix4x4> array, int offset)
        {
            if (this._checkValue)
            {
                bool anyDiff = false;
                for (int i = offset; i < Math.Min(array.Length, this._stateArray.Length); ++i)
                {
                    if (this._m4StateArray[i] != array[i])
                    {
                        anyDiff = true;
                        this._m4StateArray[i] = array[i];
                    }
                }

                if (anyDiff)
                {
                    GL.Uniform((int)this._uniformArray[offset], array, false);
                }
            }
            else
            {
                GL.Uniform((int)this._uniformArray[offset], array, false);
            }
        }
    }
}
