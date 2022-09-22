namespace VTT.GL
{
    using OpenTK.Mathematics;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public enum VertexElementType
    {
        Int,
        UInt,
        Float,
        Double,
        Vec2,
        Vec2d,
        Vec3,
        Vec3d,
        Vec4,
        Vec4d,
        Mat2,
        Mat2d,
        Mat3,
        Mat3d,
        Mat4,
        Mat4d
    }

    public struct VertexFormatElement
    {
        public delegate float[] FormatConverter(object raw);
        public delegate void FormatAppender(object raw, float[] array, ref int arrayIndex);
        public delegate void VAOFunc(VertexArray vao);

        public VertexElementType Type { get; set; }
        public FormatConverter Converter { get; set; }
        public VAOFunc VAOAdapter { get; set; }
        public FormatAppender Appender { get; set; }
        public int ElementSize { get; set; }

        public VertexFormatElement(VertexElementType type, FormatConverter converter, VAOFunc func, FormatAppender appender, int size)
        {
            this.Type = type;
            this.Converter = converter;
            this.VAOAdapter = func;
            this.ElementSize = size;
            this.Appender = appender;
        }

        public float[] ToArray(object data) => this.Converter(data);

        public void PushVAO(VertexArray vao) => this.VAOAdapter(vao);
    }

    public class VertexFormat
    {
        public static VertexFormatElement ElementInt { get; } = new VertexFormatElement(VertexElementType.Int, o => new float[] { (int)o }, v => v.PushElement(ElementType.Float), (object o, float[] a, ref int i) => a[i++] = (int)o, 1);
        public static VertexFormatElement ElementFloat { get; } = new VertexFormatElement(VertexElementType.Float, o => new float[] { (float)o }, v => v.PushElement(ElementType.Float), (object o, float[] a, ref int i) => a[i++] = (float)o, 1);
        public static VertexFormatElement ElementVec2 { get; } = new VertexFormatElement(VertexElementType.Vec2, o => new float[] { ((Vector2)o).X, ((Vector2)o).Y }, v => v.PushElement(ElementType.Vec2), AppenderVec2, 2);
        public static VertexFormatElement ElementVec3 { get; } = new VertexFormatElement(VertexElementType.Vec3, o => new float[] { ((Vector3)o).X, ((Vector3)o).Y, ((Vector3)o).Z }, v => v.PushElement(ElementType.Vec3), AppenderVec3, 3);
        public static VertexFormatElement ElementVec4 { get; } = new VertexFormatElement(VertexElementType.Vec4, o => new float[] { ((Vector4)o).X, ((Vector4)o).Y, ((Vector4)o).Z, ((Vector4)o).W }, v => v.PushElement(ElementType.Vec4), AppenderVec4, 4);
        public static VertexFormatElement ElementMat4 { get; } = new VertexFormatElement(VertexElementType.Mat4, o => new float[] { ((Matrix4)o).M11, ((Matrix4)o).M12, ((Matrix4)o).M13, ((Matrix4)o).M14, ((Matrix4)o).M21, ((Matrix4)o).M22, ((Matrix4)o).M23, ((Matrix4)o).M24, ((Matrix4)o).M31, ((Matrix4)o).M32, ((Matrix4)o).M33, ((Matrix4)o).M34, ((Matrix4)o).M41, ((Matrix4)o).M42, ((Matrix4)o).M43, ((Matrix4)o).M44 }, v => v.PushElement(ElementType.Mat4), AppenderMat4, 16);

        public static VertexFormat Pos { get; } = new VertexFormat(new []{ VertexData.Position }, ElementVec3);
        public static VertexFormat AnimatedPos { get; } = new VertexFormat(new[] { VertexData.Position, VertexData.BoneIndexMatrix, VertexData.BoneWeightMatrix }, ElementVec3, ElementMat4, ElementMat4);
        public static VertexFormat PosUV { get; } = new VertexFormat(new[] { VertexData.Position, VertexData.UV }, ElementVec3, ElementVec2);
        public static VertexFormat PosNormal { get; } = new VertexFormat(new[] { VertexData.Position, VertexData.Normal }, ElementVec3, ElementVec3);
        public static VertexFormat PosUVNormal { get; } = new VertexFormat(new[] { VertexData.Position, VertexData.UV, VertexData.Normal }, ElementVec3, ElementVec2, ElementVec3);
        public static VertexFormat PosUVColor { get; } = new VertexFormat(new[] { VertexData.Position, VertexData.UV, VertexData.Color }, ElementVec3, ElementVec2, ElementVec4);
        public static VertexFormat UI { get; } = new VertexFormat(new[] { VertexData.Position, VertexData.Color, VertexData.UV }, ElementVec3, ElementFloat, ElementVec2);
        public static VertexFormat PosUVColorNormal { get; } = new VertexFormat(new[] { VertexData.Position, VertexData.UV, VertexData.Color, VertexData.Normal }, ElementVec3, ElementVec2, ElementVec4, ElementVec3);
        public static VertexFormat StaticModel { get; } = new VertexFormat(new[] { VertexData.Position, VertexData.UV, VertexData.Normal, VertexData.Tangent, VertexData.Bitangent }, ElementVec3, ElementVec2, ElementVec3, ElementVec3, ElementVec3);
        public static VertexFormat AnimatedModel { get; } = new VertexFormat(new[] { VertexData.Position, VertexData.UV, VertexData.Normal, VertexData.Tangent, VertexData.Bitangent, VertexData.BoneIndexMatrix, VertexData.BoneWeightMatrix }, ElementVec3, ElementVec2, ElementVec3, ElementVec3, ElementVec3, ElementMat4, ElementMat4);

        public delegate float[] Converter(VertexFormat self, Vertex vertex);
        public delegate void Appender(VertexFormat self, Vertex vertex, float[] array, ref int index);

        public VertexFormatElement[] Elements { get; set; }
        public int Size => this.Elements.Select(e => e.ElementSize).Sum();

        public Converter Adaptor { get; set; }
        public Appender AppendAdaptor { get; set; }

        public VertexFormat(Converter adaptor, Appender appendAdaptor, params VertexFormatElement[] elements)
        {
            this.Adaptor = adaptor;
            this.AppendAdaptor = appendAdaptor;
            this.Elements = elements;
        }

        private VertexData[] _constrStoredAdaptorArray;
        public VertexFormat(VertexData[] orderedData, params VertexFormatElement[] elements)
        {
            this._constrStoredAdaptorArray = orderedData;
            this.Adaptor = (f, v) =>
            {
                object[] data = new object[this._constrStoredAdaptorArray.Length];
                for (int i = 0; i < data.Length; ++i)
                {
                    data[i] = v[this._constrStoredAdaptorArray[i]];
                }

                return f.ConvertElements(data);
            };

            this.AppendAdaptor = (VertexFormat self, Vertex vertex, float[] array, ref int index) =>
            {
                for (int i = 0; i < this._constrStoredAdaptorArray.Length; ++i)
                {
                    object data = vertex[this._constrStoredAdaptorArray[i]];
                    this.Elements[i].Appender(data, array, ref index);
                }
            };

            this.Elements = elements;
        }

        public virtual float[] ToArray(Vertex vertex) => this.Adaptor(this, vertex);

        public virtual void SetupVAO(VertexArray vao)
        {
            vao.SetVertexSize<float>(this.Size);
            foreach (VertexFormatElement element in this.Elements)
            {
                element.PushVAO(vao);
            }
        }

        public float[] MakeInitialArray() => new float[this.Size];

        public void PushAllData(float[] array, params object[] data)
        {
            int offset = 0;
            for (int i = 0; i < data.Length; ++i)
            {
                float[] temp = this.Elements[i].ToArray(data[i]);
                Array.Copy(temp, 0, array, offset, temp.Length);
                offset += temp.Length;
            }
        }

        public void Append(float[] array, ref int index, params Vertex[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                Vertex v = data[i];
                this.Append(array, ref index, v);
            }
        }

        public void Append(float[] array, ref int index, Vertex v)
        {
            this.AppendAdaptor(this, v, array, ref index);
        }

        public float[] ConvertElements(params object[] data)
        {
            float[] array = this.MakeInitialArray();
            this.PushAllData(array, data);
            return array;
        }

        private static void AppenderVec2(object data, float[] array, ref int index)
        {
            Vector2 vec = (Vector2)data;
            array[index++] = vec.X;
            array[index++] = vec.Y;
        }

        private static void AppenderVec3(object data, float[] array, ref int index)
        {
            Vector3 vec = (Vector3)data;
            array[index++] = vec.X;
            array[index++] = vec.Y;
            array[index++] = vec.Z;
        }

        private static void AppenderVec4(object data, float[] array, ref int index)
        {
            Vector4 vec = (Vector4)data;
            array[index++] = vec.X;
            array[index++] = vec.Y;
            array[index++] = vec.Z;
            array[index++] = vec.W;
        }

        private static void AppenderMat4(object data, float[] array, ref int index)
        {
            Matrix4 mat = (Matrix4)data;
            array[index++] = mat.M11;
            array[index++] = mat.M12;
            array[index++] = mat.M13;
            array[index++] = mat.M14;
            array[index++] = mat.M21;
            array[index++] = mat.M22;
            array[index++] = mat.M23;
            array[index++] = mat.M24;
            array[index++] = mat.M31;
            array[index++] = mat.M32;
            array[index++] = mat.M33;
            array[index++] = mat.M34;
            array[index++] = mat.M41;
            array[index++] = mat.M42;
            array[index++] = mat.M43;
            array[index++] = mat.M44;
        }
    }

    public class Vertex : ICloneable, IEnumerable<KeyValuePair<VertexData, object>>
    {
        private readonly Dictionary<VertexData, object> vertexData = new Dictionary<VertexData, object>();

        public virtual object this[VertexData s]
        {
            get => this.vertexData[s];
            set => this.vertexData[s] = value;
        }

        public virtual void PushData(VertexData name, object thing) => this.vertexData[name] = thing;
        public virtual bool HasData(VertexData name) => this.vertexData.ContainsKey(name);

        public Vertex()
        {
        }

        public Vertex(IEnumerable<KeyValuePair<VertexData, object>> data)
        {
            foreach (KeyValuePair<VertexData, object> d in data)
            {
                this.vertexData[d.Key] = d.Value;
            }
        }

        object ICloneable.Clone() => this.Clone();

        public virtual Vertex Clone()
        {
            Vertex ret = new Vertex();
            foreach (KeyValuePair<VertexData, object> data in this.vertexData)
            {
                ret.vertexData[data.Key] = data.Value;
            }

            return ret;
        }

        public virtual IEnumerator<KeyValuePair<VertexData, object>> GetEnumerator() => this.vertexData.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.vertexData.GetEnumerator();

        public virtual void Add(VertexData name, object value) => this.vertexData.Add(name, value);
        public virtual void ClearData(params VertexData[] data) => data.Select(s => this.vertexData.Remove(s)).Sum(b => 0);
        public virtual void Clear() => this.vertexData.Clear();
    }

    public class ArrayVertex : Vertex
    {
        const int VertexDataLength = 8;
        private readonly object[] _backingArray;

        public ArrayVertex() : base()
        {
            this._backingArray = new object[VertexDataLength];
        }

        public ArrayVertex(IEnumerable<KeyValuePair<VertexData, object>> data) : this()
        {
            foreach (KeyValuePair<VertexData, object> d in data)
            {
                this._backingArray[(byte)d.Key] = d.Value;
            }
        }

        public override object this[VertexData s]
        {
            get => this._backingArray[(byte)s];
            set => this._backingArray[(byte)s] = value;
        }

        public override void PushData(VertexData name, object thing) => this._backingArray[(byte)name] = thing;
        public override bool HasData(VertexData name) => this._backingArray[(byte)name] != null;

        public override Vertex Clone()
        {
            ArrayVertex ret = new ArrayVertex();
            Array.Copy(this._backingArray, ret._backingArray, VertexDataLength);
            return ret;
        }

        public override IEnumerator<KeyValuePair<VertexData, object>> GetEnumerator()
        {
            for (int i = 0; i < VertexDataLength; ++i)
            {
                object o = this._backingArray[i];
                if (o == null)
                {
                    continue;
                }

                yield return new KeyValuePair<VertexData, object>((VertexData)i, o);
            }

            yield break;
        }

        public override void Add(VertexData name, object value) => this[name] = value;
        public override void ClearData(params VertexData[] data)
        {
            foreach (VertexData dat in data)
            {
                this[dat] = null;
            }
        }

        public override void Clear()
        {
            for (int i = 0; i < VertexDataLength; ++i)
            {
                this._backingArray[i] = null;
            }
        }
    }

    public enum VertexData : byte
    {
        Position,
        Normal,
        UV,
        Color,
        Tangent,
        Bitangent,
        BoneIndexMatrix,
        BoneWeightMatrix
    }
}