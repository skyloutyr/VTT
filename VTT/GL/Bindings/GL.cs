﻿namespace VTT.GL.Bindings
{
    using System;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using static VTT.GL.Bindings.MiniGLLoader;

    public static unsafe class GL
    {
        public static void ClearColor(float r, float g, float b, float a) => clearColorO(r, g, b, a);
        public static void Viewport(int x, int y, int w, int h) => viewportO(x, y, w, h);
        public static void Clear(ClearBufferMask mask) => clearO((uint)mask);
        public static uint GenBuffer()
        {
            uint r = 0;
            genBuffersO(1, &r);
            return r;
        }

        public static ReadOnlySpan<uint> GenBuffers(int n)
        {
            uint[] uints = new uint[n];
            GCHandle gch = GCHandle.Alloc(uints, GCHandleType.Pinned);
            genBuffersO(n, (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(uints, 0));
            ReadOnlySpan<uint> buffers = new(uints);
            gch.Free();
            return buffers;
        }

        public static void DeleteBuffer(uint b) => deleteBuffersO(1, &b);
        public static void DeleteBuffers(Span<uint> buffers)
        {
            fixed (uint* ui = buffers)
            {
                deleteBuffersO(buffers.Length, ui);
            }
        }

        public static void BindBuffer(BufferTarget target, uint buffer) => bindBufferO((uint)target, buffer);
        public static void BufferData<T>(BufferTarget target, Span<T> data, BufferUsage usage) where T : unmanaged
        {
            fixed (T* ptr = data)
            {
                BufferData(target, sizeof(T) * data.Length, ptr, usage);
            }
        }

        public static void BufferData(BufferTarget target, nint size, void* data, BufferUsage usage) => bufferDataO((uint)target, size, data, (uint)usage);
        public static void BufferData(BufferTarget target, nint size, nint data, BufferUsage usage) => bufferDataO((uint)target, size, (void*)data, (uint)usage);
        public static void ShaderSource(uint shader, string src)
        {
            byte* stringPtr = (byte*)Marshal.StringToHGlobalAnsi(src); // Can treat as byte* (char* in c)
            int len = src.Length + 1; // Marshal.StringToHGlobalAnsi allocates (string.length + 1) * wchar_t size, include +1 (null terminator?)
            shaderSourceO(shader, 1, &stringPtr, &len);
            Marshal.FreeHGlobal((IntPtr)stringPtr);
        }

        public static uint CreateShader(ShaderType shaderType) => createShaderO((uint)shaderType);
        public static void CompileShader(uint shader) => compileShaderO(shader);
        public static int GetShaderProperty(uint shader, ShaderProperty property)
        {
            int r = 0;
            getShaderivO(shader, (uint)property, &r);
            return r;
        }

        public static string GetShaderInfoLog(uint shader)
        {
            int l = 0;
            int bLen = GetShaderProperty(shader, ShaderProperty.InfoLogLength);
            byte* stringBytes = (byte*)Marshal.AllocHGlobal(bLen);
            getShaderInfoLogO(shader, bLen, &l, stringBytes);
            string r = Marshal.PtrToStringAnsi((IntPtr)stringBytes, l);
            Marshal.FreeHGlobal((IntPtr)stringBytes);
            return r;
        }

        public static void DeleteShader(uint shader) => deleteShaderO(shader);
        public static uint CreateProgram() => createProgramO();
        public static void AttachShader(uint program, uint shader) => attachShaderO(program, shader);
        public static void DetachShader(uint program, uint shader) => detachShaderO(program, shader);
        public static void LinkProgram(uint program) => linkProgramO(program);
        public static ReadOnlySpan<int> GetProgramProperty(uint program, ProgramProperty pname)
        {
            int[] pars = new int[pname == ProgramProperty.ComputeWorkGroupSize ? 3 : 1];
            GCHandle gch = GCHandle.Alloc(pars, GCHandleType.Pinned);
            getProgramivO(program, (uint)pname, (int*)Marshal.UnsafeAddrOfPinnedArrayElement(pars, 0));
            gch.Free();
            return new ReadOnlySpan<int>(pars);
        }

        public static string GetProgramInfoLog(uint program)
        {
            int l = 0;
            int bLen = GetProgramProperty(program, ProgramProperty.InfoLogLength)[0];
            byte* stringBytes = (byte*)Marshal.AllocHGlobal(bLen);
            getProgramInfoLogO(program, bLen, &l, stringBytes);
            string r = Marshal.PtrToStringAnsi((IntPtr)stringBytes, l);
            Marshal.FreeHGlobal((IntPtr)stringBytes);
            return r;
        }

        public static void UseProgram(uint program) => useProgramO(program);
        public static void DeleteProgram(uint program) => deleteProgramO(program);
        public static uint GenVertexArray()
        {
            uint r = 0;
            genVertexArraysO(1, &r);
            return r;
        }

        public static ReadOnlySpan<uint> GenVertexArrays(int n)
        {
            uint[] uints = new uint[n];
            GCHandle gch = GCHandle.Alloc(uints, GCHandleType.Pinned);
            genVertexArraysO(n, (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(uints, 0));
            ReadOnlySpan<uint> buffers = new(uints);
            gch.Free();
            return buffers;
        }

        public static void BindVertexArray(uint array) => bindVertexArrayO(array);
        public static void EnableVertexAttribArray(uint index) => enableVertexAttribArrayO(index);
        public static void DisableVertexAttribArray(uint index) => disableVertexAttribArrayO(index);
        public static void VertexAttribPointer(uint index, int size, VertexAttributeType type, bool normalized, int stride, nint pointer) => vertexAttribPointerO(index, size, (uint)type, normalized, stride, (void*)pointer);
        public static void VertexAttribIPointer(uint index, int size, VertexAttributeIntegerType type, int stride, nint pointer) => vertexAttribIPointerO(index, size, (uint)type, stride, (void*)pointer);
        public static void DrawArrays(PrimitiveType mode, int first, int count) => drawArraysO((uint)mode, first, count);
        public static void DrawArraysInstanced(PrimitiveType mode, int first, int count, int numInstances) => drawArraysInstancedO((uint)mode, first, count, numInstances);
        public static void DrawElements(PrimitiveType mode, int count, ElementsType type, nint indicesOffset) => drawElementsO((uint)mode, count, (uint)type, (void*)indicesOffset);
        public static void DrawElementsInstanced(PrimitiveType mode, int count, ElementsType type, nint indicesOffset, int numInstances) => drawElementsInstancedO((uint)mode, count, (uint)type, (void*)indicesOffset, numInstances);
        public static void DrawElementsBaseVertex(PrimitiveType mode, int count, ElementsType type, nint indicesOffset, int baseVertex) => drawElementsBaseVertexO((uint)mode, count, (uint)type, (void*)indicesOffset, baseVertex);
        public static void Enable(Capability cap) => enableO((uint)cap);
        public static void Disable(Capability cap) => disableO((uint)cap);
        public static void EnableIndexed(IndexedCapability cap, uint index) => enableiO((uint)cap, index);
        public static void DisableIndexed(IndexedCapability cap, uint index) => disableiO((uint)cap, index);
        public static int GetUniformLocation(uint program, string uniformName)
        {
            byte* ansiStr = (byte*)Marshal.StringToHGlobalAnsi(uniformName);
            int uniformLoc = getUniformLocationO(program, ansiStr);
            Marshal.FreeHGlobal((IntPtr)ansiStr);
            return uniformLoc;
        }

        public static uint GetUniformBlockIndex(uint program, string uniormBlockName)
        {
            byte* ansiStr = (byte*)Marshal.StringToHGlobalAnsi(uniormBlockName);
            uint uniformLoc = getUniformBlockIndexO(program, ansiStr);
            Marshal.FreeHGlobal((IntPtr)ansiStr);
            return uniformLoc;
        }

        public static void UniformBlockBinding(uint program, uint blockIndex, uint blockBinding) => uniformBlockBindingO(program, blockIndex, blockBinding);

        public static void Uniform(int location, bool value) => uniform1iO(location, value ? 1 : 0);
        public static void Uniform(int location, int value) => uniform1iO(location, value);
        public static void Uniform(int location, uint value) => uniform1uiO(location, value);
        public static void Uniform(int location, float value) => uniform1fO(location, value);
        public static void Uniform(int location, Vector2 value) => uniform2fvO(location, 1, &value.X);
        public static void Uniform(int location, Vector3 value) => uniform3fvO(location, 1, &value.X);
        public static void Uniform(int location, Vector4 value) => uniform4fvO(location, 1, &value.X);
        public static void Uniform(int location, Matrix4x4 value) => uniformMatrix4fvO(location, 1, false, &value.M11);
        public static void Uniform(int location, Matrix3x2 value) => uniformMatrix3x2fvO(location, 1, false, &value.M11);
        public static void Uniform(int location, bool[] values)
        {
            int[] convVals = values.Select(x => x ? 1 : 0).ToArray(); // Terrible performance, but alas, opengl expects integer inputs for bools
            GCHandle hnd = GCHandle.Alloc(convVals, GCHandleType.Pinned);
            uniform1uivO(location, convVals.Length, (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(convVals, 0));
            hnd.Free();
        }

        public static void Uniform(int location, int[] values)
        {
            GCHandle hnd = GCHandle.Alloc(values, GCHandleType.Pinned);
            uniform1ivO(location, values.Length, (int*)Marshal.UnsafeAddrOfPinnedArrayElement(values, 0));
            hnd.Free();
        }

        public static void Uniform(int location, uint[] values)
        {
            GCHandle hnd = GCHandle.Alloc(values, GCHandleType.Pinned);
            uniform1uivO(location, values.Length, (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(values, 0));
            hnd.Free();
        }

        public static void Uniform(int location, float[] values)
        {
            GCHandle hnd = GCHandle.Alloc(values, GCHandleType.Pinned);
            uniform1fvO(location, values.Length, (float*)Marshal.UnsafeAddrOfPinnedArrayElement(values, 0));
            hnd.Free();
        }

        public static void Uniform(int location, Vector2[] values)
        {
            GCHandle hnd = GCHandle.Alloc(values, GCHandleType.Pinned);
            uniform2fvO(location, values.Length, (float*)Marshal.UnsafeAddrOfPinnedArrayElement(values, 0)); // Bad cast but works for opengl so w/e
            hnd.Free();
        }

        public static void Uniform(int location, Vector3[] values)
        {
            GCHandle hnd = GCHandle.Alloc(values, GCHandleType.Pinned);
            uniform3fvO(location, values.Length, (float*)Marshal.UnsafeAddrOfPinnedArrayElement(values, 0)); // Bad cast but works for opengl so w/e
            hnd.Free();
        }

        public static void Uniform(int location, Vector4[] values)
        {
            GCHandle hnd = GCHandle.Alloc(values, GCHandleType.Pinned);
            uniform4fvO(location, values.Length, (float*)Marshal.UnsafeAddrOfPinnedArrayElement(values, 0)); // Bad cast but works for opengl so w/e
            hnd.Free();
        }

        public static void CullFace(PolygonFaceMode faceMode) => cullFaceO((uint)faceMode);
        public static void PolygonMode(PolygonFaceMode face, PolygonRasterMode raster) => polygonModeO((uint)face, (uint)raster);
        public static uint GenTexture()
        {
            uint r = 0;
            genTexturesO(1, &r);
            return r;
        }

        public static ReadOnlySpan<uint> GenTextures(int n)
        {
            uint[] uints = new uint[n];
            GCHandle gch = GCHandle.Alloc(uints, GCHandleType.Pinned);
            genTexturesO(n, (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(uints, 0));
            ReadOnlySpan<uint> buffers = new(uints);
            gch.Free();
            return buffers;
        }

        public static void BindTexture(TextureTarget target, uint texture) => bindTextureO((uint)target, texture);
        public static void DeleteTexture(uint t) => deleteTexturesO(1, &t);
        public static void DeleteTextures(Span<uint> textures)
        {
            fixed (uint* ui = textures)
            {
                deleteTexturesO(textures.Length, ui);
            }
        }

        public static void TexImage1D(int level, SizedInternalFormat internalFormat, int width, PixelDataFormat dataFormat, PixelDataType dataElementType, nint data) => texImage1DO((uint)TextureTarget.Texture1D, level, (int)internalFormat, width, 0, (uint)dataFormat, (uint)dataElementType, (void*)data);
        public static void TexImage2D(TextureTarget target, int level, SizedInternalFormat internalFormat, int width, int height, PixelDataFormat dataFormat, PixelDataType dataElementType, nint data) => texImage2DO((uint)target, level, (int)internalFormat, width, height, 0, (uint)dataFormat, (uint)dataElementType, (void*)data);
        public static void TexImage3D(TextureTarget target, int level, SizedInternalFormat internalFormat, int width, int height, int depth, PixelDataFormat dataFormat, PixelDataType dataElementType, nint data) => texImage3DO((uint)target, level, (int)internalFormat, width, height, depth, 0, (uint)dataFormat, (uint)dataElementType, (void*)data);
        public static void TexSubImage1D(int level, int xoffset, int width, PixelDataFormat dataFormat, PixelDataType dataElementType, nint data) => texSubImage1DO((uint)TextureTarget.Texture1D, level, xoffset, width, (uint)dataFormat, (uint)dataElementType, (void*)data);
        public static void TexSubImage2D(TextureTarget target, int level, int xoffset, int yoffset, int width, int height, PixelDataFormat dataFormat, PixelDataType dataElementType, nint data) => texSubImage2DO((uint)target, level, xoffset, yoffset, width, height, (uint)dataFormat, (uint)dataElementType, (void*)data);
        public static void TexSubImage3D(TextureTarget target, int level, int xoffset, int yoffset, int zoffset, int width, int height, int depth, PixelDataFormat dataFormat, PixelDataType dataElementType, nint data) => texSubImage3DO((uint)target, level, xoffset, yoffset, zoffset, width, height, depth, (uint)dataFormat, (uint)dataElementType, (void*)data);
        public static void ActiveTexture(uint index) => activeTextureO(0x84C0 + index); // No enum here due to a remark by khronos that 'The number of texture units an implementation supports is implementation dependent, but must be at least 80.', yet the header for 4.6 only lists units 0-31.
        public static void TexParameter(TextureTarget target, TextureProperty prop, int value) => texParameteriO((uint)target, (uint)prop, value);
        public static void TexParameter(TextureTarget target, TextureProperty prop, float value) => texParameterfO((uint)target, (uint)prop, value);
        public static void TexParameter(TextureTarget target, TextureProperty prop, DepthStencilTextureMode depthStencilMode) => TexParameter(target, prop, (int)depthStencilMode);
        public static void TexParameter(TextureTarget target, TextureProperty prop, ComparisonMode textureCompareFunc) => TexParameter(target, prop, (int)textureCompareFunc);
        public static void TexParameter(TextureTarget target, TextureProperty prop, TextureCompareMode textureCompareMode) => TexParameter(target, prop, (int)textureCompareMode);
        public static void TexParameter(TextureTarget target, TextureProperty prop, TextureMinFilter textureMinFilter) => TexParameter(target, prop, (int)textureMinFilter);
        public static void TexParameter(TextureTarget target, TextureProperty prop, TextureMagFilter textureMagFilter) => TexParameter(target, prop, (int)textureMagFilter);
        public static void TexParameter(TextureTarget target, TextureProperty prop, TextureSwizzleMask swizzleMask) => TexParameter(target, prop, (int)swizzleMask);
        public static void TexParameter(TextureTarget target, TextureProperty prop, TextureWrapMode wrapMode) => TexParameter(target, prop, (int)wrapMode);
        public static void TexParameter(TextureTarget target, TextureProperty prop, TextureSwizzleMask[] swizzleMask)
        {
            GCHandle gch = GCHandle.Alloc(swizzleMask, GCHandleType.Pinned);
            texParameterivO((uint)target, (uint)prop, (int*)Marshal.UnsafeAddrOfPinnedArrayElement(swizzleMask, 0));
            gch.Free();
        }

        public static void TexParameter(TextureTarget target, TextureProperty prop, float[] values)
        {
            GCHandle gch = GCHandle.Alloc(values, GCHandleType.Pinned);
            texParameterfvO((uint)target, (uint)prop, (float*)Marshal.UnsafeAddrOfPinnedArrayElement(values, 0));
            gch.Free();
        }

        public static void GenerateMipmap(TextureTarget target) => generateMipmapO((uint)target);
        public static void DepthFunction(ComparisonMode depthMode) => depthFuncO((uint)depthMode);
        public static void BufferSubData(BufferTarget target, nint offset, nint size, nint data) => bufferSubDataO((uint)target, offset, size, (void*)data);
        public static void BufferSubData<T>(BufferTarget target, nint offset, ReadOnlySpan<T> data) where T : unmanaged
        {
            fixed (T* ptr = data)
            {
                BufferSubData(target, offset, sizeof(T) * data.Length, (nint)ptr);
            }
        }

        public static void BlendFunc(BlendingFactor src, BlendingFactor dst) => blendFuncO((uint)src, (uint)dst);

        public static void DeleteVertexArray(uint b) => deleteVertexArraysO(1, &b);
        public static void DeleteVertexArrays(Span<uint> buffers)
        {
            fixed (uint* ui = buffers)
            {
                deleteVertexArraysO(buffers.Length, ui);
            }
        }

        public static void* MapBuffer(BufferTarget target, BufferAccess access) => mapBufferO((uint)target, (uint)access);
        public static bool UnmapBuffer(BufferTarget target) => unmapBufferO((uint)target);
        public static void CompressedTexImage2D(TextureTarget target, int level, SizedInternalFormat internalFormat, int w, int h, int imageSize, void* data) => compressedTexImage2DO((uint)target, level, (uint)internalFormat, w, h, 0, imageSize, data);
        public static void* GenFenceSync() => fenceSyncO(0x9117, 0);
        public static bool IsTexture(uint tex) => isTextureO(tex);
        public static ReadOnlySpan<int> GetInteger(GLPropertyName prop)
        {
            int[] ints = new int[4];
            GCHandle gch = GCHandle.Alloc(ints, GCHandleType.Pinned);
            getIntegervO((uint)prop, (int*)Marshal.UnsafeAddrOfPinnedArrayElement(ints, 0));
            ReadOnlySpan<int> buffers = new(ints);
            gch.Free();
            return buffers;
        }

        public static int GetSync(void* sync, SyncProperty pname)
        {
            int i = 0;
            int j = 0;
            getSyncivO(sync, (uint)pname, sizeof(int), &j, &i);
            return i;
        }

        public static void DeleteSync(void* sync) => deleteSyncO(sync);
        public static int GetTexLevelParameter(TextureTarget target, int level, TextureLevelPropertyGetter prop)
        {
            int i = 0;
            getTexLevelParameterivO((uint)target, level, (uint)prop, &i);
            return i;
        }

        public static void GetTexImage(TextureTarget target, int level, PixelDataFormat format, PixelDataType type, void* pixels) => getTexImageO((uint)target, level, (uint)format, (uint)type, pixels);

        public static uint GenQuery()
        {
            uint r = 0;
            genQueriesO(1, &r);
            return r;
        }

        public static ReadOnlySpan<uint> GenQueries(int n)
        {
            uint[] uints = new uint[n];
            GCHandle gch = GCHandle.Alloc(uints, GCHandleType.Pinned);
            genQueriesO(n, (uint*)Marshal.UnsafeAddrOfPinnedArrayElement(uints, 0));
            ReadOnlySpan<uint> buffers = new(uints);
            gch.Free();
            return buffers;
        }

        public static ulong GetQueryObjectUnsignedLong(uint id, QueryProperty pname)
        {
            ulong ul = 0;
            getQueryObjectui64vO(id, (uint)pname, &ul);
            return ul;
        }

        public static long GetQueryObjectLong(uint id, QueryProperty pname)
        {
            long l = 0;
            getQueryObjecti64vO(id, (uint)pname, &l);
            return l;
        }

        public static void BeginQuery(QueryTarget target, uint id) => beginQueryO((uint)target, id);
        public static void EndQuery(QueryTarget target) => endQueryO((uint)target);

        public static void DeleteQuery(uint b) => deleteQueriesO(1, &b);
        public static void DeleteQueries(Span<uint> buffers)
        {
            fixed (uint* ui = buffers)
            {
                deleteQueriesO(buffers.Length, ui);
            }
        }

        public static void GetActiveUniform(uint program, uint index, int bufSize, out int length, out int size, out UniformDataType type, out string name)
        {
            byte* stringData = (byte*)Marshal.AllocHGlobal(bufSize);

            int l = 0;
            int s = 0;
            uint t = 0;

            getActiveUniformO(program, index, bufSize, &l, &s, &t, stringData);

            length = l;
            size = s;
            type = (UniformDataType)t;
            name = Marshal.PtrToStringAnsi((IntPtr)stringData, l);

            Marshal.FreeHGlobal((IntPtr)stringData);
        }

        public static void BindBufferBase(BaseBufferTarget target, uint index, uint buffer) => bindBufferBaseO((uint)target, index, buffer);

        public static string GetExtensionAt(int index)
        {

        }
    }

    public enum UniformDataType
    {
        Float = 0x1406,
        FloatVec2 = 0x8B50,
        FloatVec3 = 0x8B51,
        FloatVec4 = 0x8B52,
        Double = 0x140A,
        DoubleVec2 = 0x8FFC,
        DoubleVec3 = 0x8FFD,
        DoubleVec4 = 0x8FFE,
        Int = 0x1404,
        IntVec2 = 0x8B53,
        IntVec3 = 0x8B54,
        IntVec4 = 0x8B55,
        UnsignedInt = 0x1405,
        InsignedIntVec2 = 0x8DC6,
        InsignedIntVec3 = 0x8DC7,
        InsignedIntVec4 = 0x8DC8,
        Bool = 0x8B56,
        BoolVec2 = 0x8B57,
        BoolVec3 = 0x8B58,
        BoolVec4 = 0x8B59,
        FloatMat2 = 0x8B5A,
        FloatMat3 = 0x8B5B,
        FloatMat4 = 0x8B5C,
        FloatMat2x3 = 0x8B65,
        FloatMat2x4 = 0x8B66,
        FloatMat3x2 = 0x8B67,
        FloatMat3x4 = 0x8B68,
        FloatMat4x2 = 0x8B69,
        FloatMat4x3 = 0x8B6A,
        DoubleMat2 = 0x8F46,
        DoubleMat3 = 0x8F47,
        DoubleMat4 = 0x8F48,
        DoubleMat2x3 = 0x8F49,
        DoubleMat2x4 = 0x8F4A,
        DoubleMat3x2 = 0x8F4B,
        DoubleMat3x4 = 0x8F4C,
        DoubleMat4x2 = 0x8F4D,
        DoubleMat4x3 = 0x8F4E,
        Sampler1D = 0x8B5D,
        Sampler2D = 0x8B5E,
        Sampler3D = 0x8B5F,
        SamplerCube = 0x8B60,
        Sampler1DShadow = 0x8B61,
        Sampler2DShadow = 0x8B62,
        Sampler1DArray = 0x8DC0,
        Sampler2DArray = 0x8DC1,
        Sampler1DArrayShadow = 0x8DC3,
        Sampler2DArrayShadow = 0x8DC4,
        Sampler2DMultisample = 0x9108,
        Sampler2DMultisampleArray = 0x910B,
        SamplerCubeShadow = 0x8DC5,
        SamplerBuffer = 0x8DC2,
        Sampler2DRect = 0x8B63,
        Sampler2DRectShadow = 0x8B64,

        // TODO GL 4+ uniform types
    }

    public enum GLPropertyName
    {
        ActiveTexture = 0x84E0,
        AliasedLineWidthRange = 0x846E,
        ArrayBufferBinding = 0x8894,
        BlendEnabled = 0x0BE2,
        BlendColor = 0x8005,
        NumExtensions = 0x821D,

        // TODO properties starting from GL_BLEND_DST_ALPHA

        TextureBinding2D = 0x8069
    }
}