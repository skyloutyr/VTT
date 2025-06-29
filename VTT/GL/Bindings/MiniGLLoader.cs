﻿namespace VTT.GL.Bindings
{
    using System;
    using System.Runtime.InteropServices;

    public static unsafe class MiniGLLoader
    {
        public delegate IntPtr ProcAddressDelegate(string name);

        public static void Load(ProcAddressDelegate addressGetter)
        {
            clearColorO = ToDelegate<clearColorD>(addressGetter("glClearColor"));
            viewportO = ToDelegate<viewportD>(addressGetter("glViewport"));
            clearO = ToDelegate<clearD>(addressGetter("glClear"));
            genBuffersO = ToDelegate<genBuffersD>(addressGetter("glGenBuffers"));
            deleteBuffersO = ToDelegate<deleteBuffersD>(addressGetter("glDeleteBuffers"));
            bindBufferO = ToDelegate<bindBufferD>(addressGetter("glBindBuffer"));
            bufferDataO = ToDelegate<bufferDataD>(addressGetter("glBufferData"));
            createShaderO = ToDelegate<createShaderD>(addressGetter("glCreateShader"));
            deleteShaderO = ToDelegate<deleteShaderD>(addressGetter("glDeleteShader"));
            attachShaderO = ToDelegate<attachShaderD>(addressGetter("glAttachShader"));
            compileShaderO = ToDelegate<compileShaderD>(addressGetter("glCompileShader"));
            detachShaderO = ToDelegate<detachShaderD>(addressGetter("glDetachShader"));
            shaderSourceO = ToDelegate<shaderSourceD>(addressGetter("glShaderSource"));
            getShaderivO = ToDelegate<getShaderivD>(addressGetter("glGetShaderiv"));
            getShaderInfoLogO = ToDelegate<getShaderInfoLogD>(addressGetter("glGetShaderInfoLog"));
            createProgramO = ToDelegate<createProgramD>(addressGetter("glCreateProgram"));
            deleteProgramO = ToDelegate<deleteProgramD>(addressGetter("glDeleteProgram"));
            linkProgramO = ToDelegate<linkProgramD>(addressGetter("glLinkProgram"));
            useProgramO = ToDelegate<useProgramD>(addressGetter("glUseProgram"));
            getProgramivO = ToDelegate<getProgramivD>(addressGetter("glGetProgramiv"));
            getProgramInfoLogO = ToDelegate<getProgramInfoLogD>(addressGetter("glGetProgramInfoLog"));
            genVertexArraysO = ToDelegate<genVertexArraysD>(addressGetter("glGenVertexArrays"));
            bindVertexArrayO = ToDelegate<bindVertexArrayD>(addressGetter("glBindVertexArray"));
            enableVertexAttribArrayO = ToDelegate<enableVertexAttribArrayD>(addressGetter("glEnableVertexAttribArray"));
            disableVertexAttribArrayO = ToDelegate<disableVertexAttribArrayD>(addressGetter("glDisableVertexAttribArray"));
            vertexAttribPointerO = ToDelegate<vertexAttribPointerD>(addressGetter("glVertexAttribPointer"));
            vertexAttribIPointerO = ToDelegate<vertexAttribIPointerD>(addressGetter("glVertexAttribIPointer"));
            drawArraysO = ToDelegate<drawArraysD>(addressGetter("glDrawArrays"));
            drawArraysInstancedO = ToDelegate<drawArraysInstancedD>(addressGetter("glDrawArraysInstanced"));
            drawElementsO = ToDelegate<drawElementsD>(addressGetter("glDrawElements"));
            drawElementsInstancedO = ToDelegate<drawElementsInstancedD>(addressGetter("glDrawElementsInstanced"));
            drawElementsBaseVertexO = ToDelegate<drawElementsBaseVertexD>(addressGetter("glDrawElementsBaseVertex"));
            enableO = ToDelegate<enableD>(addressGetter("glEnable"));
            enableiO = ToDelegate<enableiD>(addressGetter("glEnablei"));
            disableO = ToDelegate<disableD>(addressGetter("glDisable"));
            disableiO = ToDelegate<disableiD>(addressGetter("glDisablei"));
            getUniformLocationO = ToDelegate<getUniformLocationD>(addressGetter("glGetUniformLocation"));
            uniform1fO = ToDelegate<uniform1fD>(addressGetter("glUniform1f"));
            uniform2fO = ToDelegate<uniform2fD>(addressGetter("glUniform2f"));
            uniform3fO = ToDelegate<uniform3fD>(addressGetter("glUniform3f"));
            uniform4fO = ToDelegate<uniform4fD>(addressGetter("glUniform4f"));
            uniform1iO = ToDelegate<uniform1iD>(addressGetter("glUniform1i"));
            uniform2iO = ToDelegate<uniform2iD>(addressGetter("glUniform2i"));
            uniform3iO = ToDelegate<uniform3iD>(addressGetter("glUniform3i"));
            uniform4iO = ToDelegate<uniform4iD>(addressGetter("glUniform4i"));
            uniform1uiO = ToDelegate<uniform1uiD>(addressGetter("glUniform1ui"));
            uniform2uiO = ToDelegate<uniform2uiD>(addressGetter("glUniform2ui"));
            uniform3uiO = ToDelegate<uniform3uiD>(addressGetter("glUniform3ui"));
            uniform4uiO = ToDelegate<uniform4uiD>(addressGetter("glUniform4ui"));
            uniform1fvO = ToDelegate<uniformxfvD>(addressGetter("glUniform1fv"));
            uniform2fvO = ToDelegate<uniformxfvD>(addressGetter("glUniform2fv"));
            uniform3fvO = ToDelegate<uniformxfvD>(addressGetter("glUniform3fv"));
            uniform4fvO = ToDelegate<uniformxfvD>(addressGetter("glUniform4fv"));
            uniform1ivO = ToDelegate<uniformxivD>(addressGetter("glUniform1iv"));
            uniform2ivO = ToDelegate<uniformxivD>(addressGetter("glUniform2iv"));
            uniform3ivO = ToDelegate<uniformxivD>(addressGetter("glUniform3iv"));
            uniform4ivO = ToDelegate<uniformxivD>(addressGetter("glUniform4iv"));
            uniform1uivO = ToDelegate<uniformxuivD>(addressGetter("glUniform1uiv"));
            uniform2uivO = ToDelegate<uniformxuivD>(addressGetter("glUniform2uiv"));
            uniform3uivO = ToDelegate<uniformxuivD>(addressGetter("glUniform3uiv"));
            uniform4uivO = ToDelegate<uniformxuivD>(addressGetter("glUniform4uiv"));
            uniformMatrix2fvO = ToDelegate<uniformMatrixfvD>(addressGetter("glUniformMatrix2fv"));
            uniformMatrix3fvO = ToDelegate<uniformMatrixfvD>(addressGetter("glUniformMatrix3fv"));
            uniformMatrix4fvO = ToDelegate<uniformMatrixfvD>(addressGetter("glUniformMatrix4fv"));
            uniformMatrix2x3fvO = ToDelegate<uniformMatrixfvD>(addressGetter("glUniformMatrix2x3fv"));
            uniformMatrix3x2fvO = ToDelegate<uniformMatrixfvD>(addressGetter("glUniformMatrix3x2fv"));
            uniformMatrix2x4fvO = ToDelegate<uniformMatrixfvD>(addressGetter("glUniformMatrix2x4fv"));
            uniformMatrix4x2fvO = ToDelegate<uniformMatrixfvD>(addressGetter("glUniformMatrix4x2fv"));
            uniformMatrix3x4fvO = ToDelegate<uniformMatrixfvD>(addressGetter("glUniformMatrix3x4fv"));
            uniformMatrix4x3fvO = ToDelegate<uniformMatrixfvD>(addressGetter("glUniformMatrix4x3fv"));
            cullFaceO = ToDelegate<cullFaceD>(addressGetter("glCullFace"));
            polygonModeO = ToDelegate<polygonModeD>(addressGetter("glPolygonMode"));
            genTexturesO = ToDelegate<genTexturesD>(addressGetter("glGenTextures"));
            bindTextureO = ToDelegate<bindTextureD>(addressGetter("glBindTexture"));
            deleteTexturesO = ToDelegate<deleteTexturesD>(addressGetter("glDeleteTextures"));
            texImage1DO = ToDelegate<texImage1DD>(addressGetter("glTexImage1D"));
            texImage2DO = ToDelegate<texImage2DD>(addressGetter("glTexImage2D"));
            texImage3DO = ToDelegate<texImage3DD>(addressGetter("glTexImage3D"));
            activeTextureO = ToDelegate<activeTextureD>(addressGetter("glActiveTexture"));
            texParameterfO = ToDelegate<texParameterfD>(addressGetter("glTexParameterf"));
            texParameteriO = ToDelegate<texParameteriD>(addressGetter("glTexParameteri"));
            texParameterfvO = ToDelegate<texParameterfvD>(addressGetter("glTexParameterfv"));
            texParameterivO = ToDelegate<texParameterivD>(addressGetter("glTexParameteriv"));
            generateMipmapO = ToDelegate<generateMipmapD>(addressGetter("glGenerateMipmap"));
            depthFuncO = ToDelegate<depthFuncD>(addressGetter("glDepthFunc"));
            bufferSubDataO = ToDelegate<bufferSubDataD>(addressGetter("glBufferSubData"));
            blendFuncO = ToDelegate<blendFuncD>(addressGetter("glBlendFunc"));
            deleteVertexArraysO = ToDelegate<deleteVertexArraysD>(addressGetter("glDeleteVertexArrays"));
            texSubImage1DO = ToDelegate<texSubImage1DD>(addressGetter("glTexSubImage1D"));
            texSubImage2DO = ToDelegate<texSubImage2DD>(addressGetter("glTexSubImage2D"));
            texSubImage3DO = ToDelegate<texSubImage3DD>(addressGetter("glTexSubImage3D"));
            mapBufferO = ToDelegate<mapBufferD>(addressGetter("glMapBuffer"));
            unmapBufferO = ToDelegate<unmapBufferD>(addressGetter("glUnmapBuffer"));
            compressedTexImage2DO = ToDelegate<compressedTexImage2DD>(addressGetter("glCompressedTexImage2D"));
            fenceSyncO = ToDelegate<fenceSyncD>(addressGetter("glFenceSync"));
            isTextureO = ToDelegate<isTextureD>(addressGetter("glIsTexture"));
            getIntegervO = ToDelegate<getIntegervD>(addressGetter("glGetIntegerv"));
            getSyncivO = ToDelegate<getSyncivD>(addressGetter("glGetSynciv"));
            deleteSyncO = ToDelegate<deleteSyncD>(addressGetter("glDeleteSync"));
            getTexLevelParameterivO = ToDelegate<getTexLevelParameterivD>(addressGetter("glGetTexLevelParameteriv"));
            getTexImageO = ToDelegate<getTexImageD>(addressGetter("glGetTexImage"));
            genQueriesO = ToDelegate<genQueriesD>(addressGetter("glGenQueries"));
            getQueryObjectui64vO = ToDelegate<getQueryObjectui64vD>(addressGetter("glGetQueryObjectui64v"));
            getQueryObjecti64vO = ToDelegate<getQueryObjecti64vD>(addressGetter("glGetQueryObjecti64v"));
            beginQueryO = ToDelegate<beginQueryD>(addressGetter("glBeginQuery"));
            endQueryO = ToDelegate<endQueryD>(addressGetter("glEndQuery"));
            deleteQueriesO = ToDelegate<deleteQueriesD>(addressGetter("glDeleteQueries"));
            getUniformBlockIndexO = ToDelegate<getUniformBlockIndexD>(addressGetter("glGetUniformBlockIndex"));
            uniformBlockBindingO = ToDelegate<uniformBlockBindingD>(addressGetter("glUniformBlockBinding"));
            getActiveUniformO = ToDelegate<getActiveUniformD>(addressGetter("glGetActiveUniform"));
            bindBufferBaseO = ToDelegate<bindBufferBaseD>(addressGetter("glBindBufferBase"));
            getStringiO = ToDelegate<getStringiD>(addressGetter("glGetStringi"));
            scissorO = ToDelegate<scissorD>(addressGetter("glScissor"));
            bindFramebufferO = ToDelegate<bindFramebufferD>(addressGetter("glBindFramebuffer"));
            depthMaskO = ToDelegate<depthMaskD>(addressGetter("glDepthMask"));
            drawBufferO = ToDelegate<drawBufferD>(addressGetter("glDrawBuffer"));
            checkFramebufferStatusO = ToDelegate<checkFramebufferStatusD>(addressGetter("glCheckFramebufferStatus"));
            genFramebuffersO = ToDelegate<genFramebuffersD>(addressGetter("glGenFramebuffers"));
            framebufferTexture2DO = ToDelegate<framebufferTexture2DD>(addressGetter("glFramebufferTexture2D"));
            drawBuffersO = ToDelegate<drawBuffersD>(addressGetter("glDrawBuffers"));
            framebufferTextureO = ToDelegate<framebufferTextureD>(addressGetter("glFramebufferTexture"));
            readBufferO = ToDelegate<drawBufferD>(addressGetter("glReadBuffer"));
            deleteFramebuffersO = ToDelegate<deleteFramebuffersD>(addressGetter("glDeleteFramebuffers"));
            colorMaskO = ToDelegate<colorMaskD>(addressGetter("glColorMask"));
            debugMessageCallbackO = ToDelegate<debugMessageCallbackD>(addressGetter("glDebugMessageCallback"));
            texBufferO = ToDelegate<texBufferD>(addressGetter("glTexBuffer"));
            vertexAttribDivisorO = ToDelegate<vertexAttribDivisorD>(addressGetter("glVertexAttribDivisor"));
            genRenderbuffersO = ToDelegate<genRenderbuffersD>(addressGetter("glGenRenderbuffers"));
            bindRenderbufferO = ToDelegate<bindRenderbufferD>(addressGetter("glBindRenderbuffer"));
            renderbufferStorageO = ToDelegate<renderbufferStorageD>(addressGetter("glRenderbufferStorage"));
            framebufferRenderbufferO = ToDelegate<framebufferRenderbufferD>(addressGetter("glFramebufferRenderbuffer"));
            waitSyncO = ToDelegate<waitSyncD>(addressGetter("glWaitSync"));
            clientWaitSyncO = ToDelegate<clientWaitSyncD>(addressGetter("glClientWaitSync"));
            mapBufferRangeO = ToDelegate<mapBufferRangeD>(addressGetter("glMapBufferRange"));
            compressedTexSubImage2DO = ToDelegate<compressedTexSubImage2DD>(addressGetter("glCompressedTexSubImage2D"));
            finishO = ToDelegate<finishD>(addressGetter("glFinish"));
            readPixelsO = ToDelegate<readPixelsD>(addressGetter("glReadPixels"));
            pushDebugGroupO = ToDelegate<pushDebugGroupD>(addressGetter("glPushDebugGroup"));
            popDebugGroupO = ToDelegate<popDebugGroupD>(addressGetter("glPopDebugGroup"));
            objectLabelO = ToDelegate<objectLabelD>(addressGetter("glObjectLabel"));
        }

        internal delegate void clearColorD(float r, float g, float b, float a);
        internal static clearColorD clearColorO;
        internal delegate void viewportD(int x, int y, int width, int height);
        internal static viewportD viewportO;
        internal delegate void clearD(uint mask);
        internal static clearD clearO;
        internal delegate void genBuffersD(int n, uint* buffers);
        internal static genBuffersD genBuffersO;
        internal delegate void deleteBuffersD(int n, uint* buffers);
        internal static deleteBuffersD deleteBuffersO;
        internal delegate void bindBufferD(uint target, uint buffer);
        internal static bindBufferD bindBufferO;
        internal delegate void bufferDataD(uint target, nint size, void* data, uint usage);
        internal static bufferDataD bufferDataO;
        internal delegate uint createShaderD(uint shaderType);
        internal static createShaderD createShaderO;
        internal delegate void deleteShaderD(uint shader);
        internal static deleteShaderD deleteShaderO;
        internal delegate void attachShaderD(uint program, uint shader);
        internal static attachShaderD attachShaderO;
        internal delegate void compileShaderD(uint shader);
        internal static compileShaderD compileShaderO;
        internal delegate void detachShaderD(uint program, uint shader);
        internal static detachShaderD detachShaderO;
        internal delegate void shaderSourceD(uint shader, int count, byte** strings, int* lengths);
        internal static shaderSourceD shaderSourceO;
        internal delegate void getShaderivD(uint shader, uint pname, int* iparams);
        internal static getShaderivD getShaderivO;
        internal delegate void getShaderInfoLogD(uint shader, int maxLength, int* length, byte* infoLog);
        internal static getShaderInfoLogD getShaderInfoLogO;
        internal delegate uint createProgramD();
        internal static createProgramD createProgramO;
        internal delegate void deleteProgramD(uint program);
        internal static deleteProgramD deleteProgramO;
        internal delegate void linkProgramD(uint program);
        internal static linkProgramD linkProgramO;
        internal delegate void useProgramD(uint program);
        internal static useProgramD useProgramO;
        internal delegate void getProgramivD(uint program, uint pname, int* param);
        internal static getProgramivD getProgramivO;
        internal delegate void getProgramInfoLogD(uint program, int maxLength, int* length, byte* infoLog);
        internal static getProgramInfoLogD getProgramInfoLogO;
        internal delegate void genVertexArraysD(int n, uint* arrays);
        internal static genVertexArraysD genVertexArraysO;
        internal delegate void bindVertexArrayD(uint array);
        internal static bindVertexArrayD bindVertexArrayO;
        internal delegate void enableVertexAttribArrayD(uint index);
        internal static enableVertexAttribArrayD enableVertexAttribArrayO;
        internal delegate void disableVertexAttribArrayD(uint index);
        internal static disableVertexAttribArrayD disableVertexAttribArrayO;
        internal delegate void vertexAttribPointerD(uint index, int size, uint type, bool normalized, int stride, void* ptr);
        internal static vertexAttribPointerD vertexAttribPointerO;
        internal delegate void vertexAttribIPointerD(uint index, int size, uint type, int stride, void* ptr);
        internal static vertexAttribIPointerD vertexAttribIPointerO;
        internal delegate void drawArraysD(uint mode, int first, int count);
        internal static drawArraysD drawArraysO;
        internal delegate void drawArraysInstancedD(uint mode, int first, int count, int instancecount);
        internal static drawArraysInstancedD drawArraysInstancedO;
        internal delegate void drawElementsD(uint mode, int count, uint type, void* indices);
        internal static drawElementsD drawElementsO;
        internal delegate void drawElementsInstancedD(uint mode, int count, uint type, void* indices, int instancecount);
        internal static drawElementsInstancedD drawElementsInstancedO;
        internal delegate void drawElementsBaseVertexD(uint mode, int count, uint type, void* indices, int baseVertex);
        internal static drawElementsBaseVertexD drawElementsBaseVertexO;
        internal delegate void enableD(uint cap);
        internal static enableD enableO;
        internal delegate void disableD(uint cap);
        internal static disableD disableO;
        internal delegate void enableiD(uint cap, uint index);
        internal static enableiD enableiO;
        internal delegate void disableiD(uint cap, uint index);
        internal static disableiD disableiO;
        internal delegate int getUniformLocationD(uint program, byte* name);
        internal static getUniformLocationD getUniformLocationO;
        internal delegate void uniform1fD(int location, float v0);
        internal delegate void uniform2fD(int location, float v0, float v1);
        internal delegate void uniform3fD(int location, float v0, float v1, float v2);
        internal delegate void uniform4fD(int location, float v0, float v1, float v2, float v3);
        internal delegate void uniform1iD(int location, int v0);
        internal delegate void uniform2iD(int location, int v0, int v1);
        internal delegate void uniform3iD(int location, int v0, int v1, int v2);
        internal delegate void uniform4iD(int location, int v0, int v1, int v2, int v3);
        internal delegate void uniform1uiD(int location, uint v0);
        internal delegate void uniform2uiD(int location, uint v0, uint v1);
        internal delegate void uniform3uiD(int location, uint v0, uint v1, uint v2);
        internal delegate void uniform4uiD(int location, uint v0, uint v1, uint v2, uint v3);
        internal delegate void uniformxfvD(int location, int count, float* values);
        internal delegate void uniformxivD(int location, int count, int* values);
        internal delegate void uniformxuivD(int location, int count, uint* values);
        internal static uniform1fD uniform1fO;
        internal static uniform2fD uniform2fO;
        internal static uniform3fD uniform3fO;
        internal static uniform4fD uniform4fO;
        internal static uniform1iD uniform1iO;
        internal static uniform2iD uniform2iO;
        internal static uniform3iD uniform3iO;
        internal static uniform4iD uniform4iO;
        internal static uniform1uiD uniform1uiO;
        internal static uniform2uiD uniform2uiO;
        internal static uniform3uiD uniform3uiO;
        internal static uniform4uiD uniform4uiO;
        internal static uniformxfvD uniform1fvO;
        internal static uniformxfvD uniform2fvO;
        internal static uniformxfvD uniform3fvO;
        internal static uniformxfvD uniform4fvO;
        internal static uniformxivD uniform1ivO;
        internal static uniformxivD uniform2ivO;
        internal static uniformxivD uniform3ivO;
        internal static uniformxivD uniform4ivO;
        internal static uniformxuivD uniform1uivO;
        internal static uniformxuivD uniform2uivO;
        internal static uniformxuivD uniform3uivO;
        internal static uniformxuivD uniform4uivO;
        internal delegate void uniformMatrixfvD(int location, int count, bool transpose, float* value);
        internal static uniformMatrixfvD uniformMatrix2fvO;
        internal static uniformMatrixfvD uniformMatrix3fvO;
        internal static uniformMatrixfvD uniformMatrix4fvO;
        internal static uniformMatrixfvD uniformMatrix2x3fvO;
        internal static uniformMatrixfvD uniformMatrix3x2fvO;
        internal static uniformMatrixfvD uniformMatrix2x4fvO;
        internal static uniformMatrixfvD uniformMatrix4x2fvO;
        internal static uniformMatrixfvD uniformMatrix3x4fvO;
        internal static uniformMatrixfvD uniformMatrix4x3fvO;
        internal delegate void cullFaceD(uint mode);
        internal static cullFaceD cullFaceO;
        internal delegate void polygonModeD(uint face, uint mode);
        internal static polygonModeD polygonModeO;
        internal delegate void genTexturesD(int n, uint* textures);
        internal static genTexturesD genTexturesO;
        internal delegate void bindTextureD(uint target, uint texture);
        internal static bindTextureD bindTextureO;
        internal delegate void deleteTexturesD(int n, uint* textures);
        internal static deleteTexturesD deleteTexturesO;
        internal delegate void texImage1DD(uint target, int level, int internalformat, int width, int border, uint format, uint type, void* data);
        internal static texImage1DD texImage1DO;
        internal delegate void texImage2DD(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, void* data);
        internal static texImage2DD texImage2DO;
        internal delegate void texImage3DD(uint target, int level, int internalformat, int width, int height, int depth, int border, uint format, uint type, void* data);
        internal static texImage3DD texImage3DO;
        internal delegate void activeTextureD(uint texture);
        internal static activeTextureD activeTextureO;
        internal delegate void texParameterfD(uint target, uint pname, float param);
        internal delegate void texParameteriD(uint target, uint pname, int param);
        internal delegate void texParameterfvD(uint target, uint pname, float* param);
        internal delegate void texParameterivD(uint target, uint pname, int* param);
        internal static texParameterfD texParameterfO;
        internal static texParameteriD texParameteriO;
        internal static texParameterfvD texParameterfvO;
        internal static texParameterivD texParameterivO;
        internal delegate void generateMipmapD(uint target);
        internal static generateMipmapD generateMipmapO;
        internal delegate void depthFuncD(uint func);
        internal static depthFuncD depthFuncO;
        internal delegate void bufferSubDataD(uint target, nint offset, nint size, void* data);
        internal static bufferSubDataD bufferSubDataO;
        internal delegate void blendFuncD(uint sfactor, uint dfactor);
        internal static blendFuncD blendFuncO;
        internal delegate void deleteVertexArraysD(int n, uint* arrays);
        internal static deleteVertexArraysD deleteVertexArraysO;
        internal delegate void texSubImage1DD(uint target, int level, int xoffset, int width, uint format, uint type, void* pixels);
        internal static texSubImage1DD texSubImage1DO;
        internal delegate void texSubImage2DD(uint target, int level, int xoffset, int yoffset, int width, int height, uint format, uint type, void* pixels);
        internal static texSubImage2DD texSubImage2DO;
        internal delegate void texSubImage3DD(uint target, int level, int xoffset, int yoffset, int zoffset, int width, int height, int depth, uint format, uint type, void* pixels);
        internal static texSubImage3DD texSubImage3DO;
        internal delegate void* mapBufferD(uint target, uint access);
        internal static mapBufferD mapBufferO;
        internal delegate bool unmapBufferD(uint target);
        internal static unmapBufferD unmapBufferO;
        internal delegate void compressedTexImage2DD(uint target, int level, uint internalformat, int width, int height, int border, int imageSize, void* data);
        internal static compressedTexImage2DD compressedTexImage2DO;
        internal delegate void* fenceSyncD(uint condition, uint flags);
        internal static fenceSyncD fenceSyncO;
        internal delegate bool isTextureD(uint tex);
        internal static isTextureD isTextureO;
        internal delegate void getIntegervD(uint pname, int* data);
        internal static getIntegervD getIntegervO;
        internal delegate void getSyncivD(void* sync, uint pname, int bufSize, int* length, int* values);
        internal static getSyncivD getSyncivO;
        internal delegate void deleteSyncD(void* sync);
        internal static deleteSyncD deleteSyncO;
        internal delegate void getTexLevelParameterivD(uint target, int level, uint pname, int* param);
        internal static getTexLevelParameterivD getTexLevelParameterivO;
        internal delegate void getTexImageD(uint target, int level, uint format, uint type, void* pixels);
        internal static getTexImageD getTexImageO;
        internal delegate void genQueriesD(int n, uint* ids);
        internal static genQueriesD genQueriesO;
        internal delegate void getQueryObjectui64vD(uint id, uint pname, ulong* param);
        internal static getQueryObjectui64vD getQueryObjectui64vO;
        internal delegate void getQueryObjecti64vD(uint id, uint pname, long* param);
        internal static getQueryObjecti64vD getQueryObjecti64vO;
        internal delegate void beginQueryD(uint target, uint id);
        internal static beginQueryD beginQueryO;
        internal delegate void endQueryD(uint target);
        internal static endQueryD endQueryO;
        internal delegate void deleteQueriesD(int n, uint* ids);
        internal static deleteQueriesD deleteQueriesO;
        internal delegate uint getUniformBlockIndexD(uint program, byte* uniformBlockName);
        internal static getUniformBlockIndexD getUniformBlockIndexO;
        internal delegate void uniformBlockBindingD(uint program, uint uniformBlockIndex, uint uniformBlockBinding);
        internal static uniformBlockBindingD uniformBlockBindingO;
        internal delegate void getActiveUniformD(uint program, uint index, int bufSize, int* length, int* size, uint* type, byte* name);
        internal static getActiveUniformD getActiveUniformO;
        internal delegate void bindBufferBaseD(uint target, uint index, uint buffer);
        internal static bindBufferBaseD bindBufferBaseO;
        internal delegate byte* getStringiD(uint name, uint index);
        internal static getStringiD getStringiO;
        internal delegate void scissorD(int x, int y, int width, int height);
        internal static scissorD scissorO;
        internal delegate void bindFramebufferD(uint target, uint fbo);
        internal static bindFramebufferD bindFramebufferO;
        internal delegate void depthMaskD(bool b);
        internal static depthMaskD depthMaskO;
        internal delegate void drawBufferD(uint buf);
        internal static drawBufferD drawBufferO;
        internal static drawBufferD readBufferO;
        internal delegate uint checkFramebufferStatusD(uint target);
        internal static checkFramebufferStatusD checkFramebufferStatusO;
        internal delegate void genFramebuffersD(int n, uint* ids);
        internal static genFramebuffersD genFramebuffersO;
        internal delegate void framebufferTexture2DD(uint target, uint attachment, uint textureTarget, uint texture, int level);
        internal static framebufferTexture2DD framebufferTexture2DO;
        internal delegate void drawBuffersD(int n, uint* bufs);
        internal static drawBuffersD drawBuffersO;
        internal delegate void framebufferTextureD(uint target, uint attachment, uint texture, int level);
        internal static framebufferTextureD framebufferTextureO;
        internal delegate void deleteFramebuffersD(int n, uint* buffers);
        internal static deleteFramebuffersD deleteFramebuffersO;
        internal delegate void colorMaskD(bool r, bool g, bool b, bool a);
        internal static colorMaskD colorMaskO;
        internal delegate void debugMessageCallbackD(void* callback, void* userParam);
        internal static debugMessageCallbackD debugMessageCallbackO;
        internal delegate void texBufferD(uint target, uint internalformat, uint buffer);
        internal static texBufferD texBufferO;
        internal delegate void vertexAttribDivisorD(uint index, uint divisor);
        internal static vertexAttribDivisorD vertexAttribDivisorO;
        internal delegate void genRenderbuffersD(int n, uint* bufs);
        internal static genRenderbuffersD genRenderbuffersO;
        internal delegate void bindRenderbufferD(uint target, uint rbo);
        internal static bindRenderbufferD bindRenderbufferO;
        internal delegate void renderbufferStorageD(uint target, uint internalformat, int w, int h);
        internal static renderbufferStorageD renderbufferStorageO;
        internal delegate void framebufferRenderbufferD(uint target, uint attachment, uint renderbuffertarget, uint renderbuffer);
        internal static framebufferRenderbufferD framebufferRenderbufferO;
        internal delegate void waitSyncD(void* sync, uint flags, ulong timeout);
        internal static waitSyncD waitSyncO;
        internal delegate uint clientWaitSyncD(void* sync, uint flags, ulong timeout);
        internal static clientWaitSyncD clientWaitSyncO;
        internal delegate void* mapBufferRangeD(uint target, nint offset, nint length, uint access);
        internal static mapBufferRangeD mapBufferRangeO;
        internal delegate void compressedTexSubImage2DD(uint target, int level, int xoffset, int yoffset, int width, int height, uint format, int imageSize, void* pixels);
        internal static compressedTexSubImage2DD compressedTexSubImage2DO;
        internal delegate void finishD();
        internal static finishD finishO;
        internal delegate void readPixelsD(int x, int y, int width, int height, uint format, uint type, void* data);
        internal static readPixelsD readPixelsO;
        internal delegate void pushDebugGroupD(uint source, uint id, int length, byte* message);
        internal static pushDebugGroupD pushDebugGroupO;
        internal delegate void popDebugGroupD();
        internal static popDebugGroupD popDebugGroupO;
        internal delegate void objectLabelD(uint identifier, uint name, int length, byte* label);
        internal static objectLabelD objectLabelO;

        private static T ToDelegate<T>(IntPtr ptr) where T : Delegate => IntPtr.Zero.Equals(ptr) ? null : Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }
}