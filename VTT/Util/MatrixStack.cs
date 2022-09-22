namespace VTT.Util
{
    using OpenTK.Mathematics;
    using System.Collections.Generic;

    public class MatrixStack
    {
        private List<Matrix4> _matStack = new List<Matrix4>();
        private Matrix4 _currentMat = Matrix4.Identity;

        public bool Reversed { get; set; }

        public void Push(Matrix4 newMat)
        {
            if (this.Reversed)
            {
                this._matStack.Add(newMat);
                this._currentMat = this.IterativelyMulMat();
            }
            else
            {
                this._matStack.Add(this._currentMat);
                this._currentMat = Matrix4.Mult(this._currentMat, newMat);
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
                this._currentMat = this._matStack[this._matStack.Count - 1];
                this._matStack.RemoveAt(this._matStack.Count - 1);
            }
        }

        public void Clear()
        {
            this._currentMat = Matrix4.Identity;
            this._matStack.Clear();
        }

        private Matrix4 IterativelyMulMat()
        {
            Matrix4 ret = Matrix4.Identity;
            for (int i = this._matStack.Count - 1; i >= 0; i--)
            {
                Matrix4 m = this._matStack[i];
                ret = Matrix4.Mult(ret, m);
            }

            return ret;
        }

        public Matrix4 Current => this._currentMat;
    }
}
