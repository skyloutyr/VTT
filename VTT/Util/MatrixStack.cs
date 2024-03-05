namespace VTT.Util
{
    using System.Collections.Generic;
    using System.Numerics;

    public class MatrixStack
    {
        private readonly List<Matrix4x4> _matStack = new List<Matrix4x4>();
        private Matrix4x4 _currentMat = Matrix4x4.Identity;

        public bool Reversed { get; set; }

        public void Push(Matrix4x4 newMat)
        {
            if (this.Reversed)
            {
                this._matStack.Add(newMat);
                this._currentMat = this.IterativelyMulMat();
            }
            else
            {
                this._matStack.Add(this._currentMat);
                this._currentMat = Matrix4x4.Multiply(this._currentMat, newMat);
            }
        }

        public void Pop()
        {
            if (this.Reversed)
            {
                this._matStack.RemoveAt(this._matStack.Count - 1);
                this._currentMat = this.IterativelyMulMat();
            }
            else
            {
                this._currentMat = this._matStack[^1];
                this._matStack.RemoveAt(this._matStack.Count - 1);
            }
        }

        public void Clear()
        {
            this._currentMat = Matrix4x4.Identity;
            this._matStack.Clear();
        }

        private Matrix4x4 IterativelyMulMat()
        {
            Matrix4x4 ret = Matrix4x4.Identity;
            for (int i = this._matStack.Count - 1; i >= 0; i--)
            {
                Matrix4x4 m = this._matStack[i];
                ret = Matrix4x4.Multiply(ret, m);
            }

            return ret;
        }

        public Matrix4x4 Current => this._currentMat;
    }
}
