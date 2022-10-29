/*
Copyright (C) 2017 thundersoft. All rights reserved.
OpenGL ES 2.0
线程不安全，所有接口同步在gl上下文调用
使用流程是   init-》update_data-》draw->read_pixel use render_texture
定义 #define BLIT_TO_SCREEN 可以将离屏渲染的画面同步到屏幕依赖 blit_pass
*/
#pragma once

#ifdef _WIN32
#define GLEW_STATIC
#include "GL/glew.h"
#define log(...) printf(__VA_ARGS__)
#elif __ANDROID__
#include "GLES2/gl2.h"
#include <android/log.h>
#define log(...)  __android_log_print(ANDROID_LOG_DEBUG, "KEL_TAG", __VA_ARGS__)
#else
#include <GLES2/gl2.h>
#define log(...) printf(__VA_ARGS__)
#endif


#include <iostream>
#include <string>


#ifdef BLIT_TO_SCREEN
#include "blit_pass.h"
#endif
class NV12ToRGB
{

public:
    NV12ToRGB(int video_width, int vieo_height):width(video_width),height(vieo_height)
#ifdef BLIT_TO_SCREEN
,blit_pass(1600,900)
#endif
    {

    }

    ~NV12ToRGB()
    {
        glDeleteTextures(1, &texture_y);
        glDeleteTextures(1, &texture_uv);
        glDeleteTextures(1, &render_texture);
        glDeleteProgram(shader);
        glDeleteFramebuffers(1,&fbo);
    }

    /// @brief 获取 离屏渲染的贴图
    /// @return OpenGL的GPU对象id
    GLuint get_render_texture(){
        return render_texture;
    }

    /// @brief 将GPU纹理拷贝到内存，填充输入参数
    /// @param data
    void read_pixel(std::vector<unsigned  char>& data){
        data.resize(width*height*3);
        glBindFramebuffer(GL_FRAMEBUFFER, fbo);
        glReadPixels(0, 0, width,height, GL_RGB, GL_UNSIGNED_BYTE, data.data());
        glBindFramebuffer(GL_FRAMEBUFFER, 0);
    }

    /// @brief 组件要在OpenGL es2.0上下文里面初始化
    void init() {
        glClearColor(0.5,0.2,0.9,1.0);
        init_shader();
        init_texture(width,height);
        init_fbo();
#ifdef BLIT_TO_SCREEN
        blit_pass.init();
#endif
    }

    /// @brief 更新一帧nv12的数据
    /// @param data
    void update_data(const unsigned  char * data) {
        glBindTexture(GL_TEXTURE_2D, texture_y);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_LUMINANCE, width, height, 0, GL_LUMINANCE, GL_UNSIGNED_BYTE, data);

        glBindTexture(GL_TEXTURE_2D, texture_uv);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_LUMINANCE_ALPHA, width/2, height/2, 0, GL_LUMINANCE_ALPHA, GL_UNSIGNED_BYTE, &data[width*height]);
    }

    /// @brief 将nv12数据离屏渲染到render texture
    void draw() {
        static float vVertices[] = {
                -1.0f,  1.0f, 0.0f,  // Position 0
                0.0f,  1.0f,        // TexCoord 0
                -1.0f, -1.0f, 0.0f,  // Position 1
                0.0f,  0.0f,        // TexCoord 1
                1.0f, -1.0f, 0.0f,  // Position 2
                1.0f,  0.0f,        // TexCoord 2
                1.0f,  1.0f, 0.0f,  // Position 3
                1.0f,  1.0f         // TexCoord 3
        };
        static GLushort indices[] ={ 0, 1, 2, 0, 2, 3 };

        glBindFramebuffer(GL_FRAMEBUFFER, fbo);
        // Set the viewport
        glViewport(0, 0, width, height);

        glClearColor(0.0,1.0,0.0,1.0);
        // Clear the color buffer
        glClear(GL_COLOR_BUFFER_BIT);

        // Use the program object
        glUseProgram(shader);

        glEnableVertexAttribArray(position_loc);
        glVertexAttribPointer(position_loc, 3, GL_FLOAT, GL_FALSE, 5 * sizeof(GLfloat), vVertices);

        glEnableVertexAttribArray(coord_loc);
        glVertexAttribPointer(coord_loc, 2, GL_FLOAT, GL_FALSE, 5 * sizeof(GLfloat), &vVertices[3]);

        // Bind the texture
        glActiveTexture(GL_TEXTURE0);
        glBindTexture(GL_TEXTURE_2D,texture_y);
        glUniform1i(y_loc, 0);

        glActiveTexture(GL_TEXTURE1);
        glBindTexture(GL_TEXTURE_2D,texture_uv);
        glUniform1i(uv_loc, 1);

        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_SHORT, indices);

        glBindFramebuffer(GL_FRAMEBUFFER,0);
#ifdef BLIT_TO_SCREEN
        blit_pass.draw(render_texture);
#endif
    }

private:
    void init_shader() {
        static const GLchar * vs_source  =
                "attribute vec3 vPosition;\n"
                "attribute vec2 vCoord;\n"
                "varying vec2 v_texCoord;\n"
                "void main() {\n"
                "  gl_Position = vec4(vPosition,1.0);\n"
                "  v_texCoord = vCoord;  \n"
                "}\n";

        static const GLchar * fs_source =
                "precision mediump float;\n"
                "varying vec2 v_texCoord;\n"
                "uniform sampler2D y_texture;                       \n"
                "uniform sampler2D uv_texture;                      \n"

                "void main (void){                                  \n"
                "   float r, g, b, y, u, v;                         \n"
                "   vec4 color=vec4(0.0,0.0,0.0,1.0);                         \n"
                "   y = texture2D(y_texture, v_texCoord).r;         \n"
                "   u = texture2D(uv_texture, v_texCoord).r -0.5;  \n"
                "   v = texture2D(uv_texture, v_texCoord).a -0.5;  \n"

                "   color.r = y + 1.13983*v;                              \n"
                "   color.g = y - 0.39465*u - 0.58060*v;                  \n"
                "   color.b = y + 2.03211*u;                              \n"

                "   gl_FragColor = color;                           \n"
                "}                                                  \n";
        // Load the shaders and get a linked program object
        GLuint vs = build_shader(GL_VERTEX_SHADER, vs_source);
        GLuint fs = build_shader(GL_FRAGMENT_SHADER, fs_source);
        build_program(vs, fs);

        position_loc = glGetAttribLocation(shader, "vPosition");
        coord_loc = glGetAttribLocation(shader, "vCoord");

        // Get the sampler location
        y_loc = glGetUniformLocation(shader, "y_texture");
        uv_loc = glGetUniformLocation(shader, "uv_texture");
    }

    void init_texture(int width,int height)
    {
        glGenTextures(1, &texture_y);
        glBindTexture(GL_TEXTURE_2D, texture_y);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_LUMINANCE, width, height, 0, GL_LUMINANCE, GL_UNSIGNED_BYTE, nullptr);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
        glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

        glGenTextures(1, &texture_uv);
        glBindTexture(GL_TEXTURE_2D, texture_uv);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_LUMINANCE_ALPHA, width/2, height/2, 0, GL_LUMINANCE_ALPHA, GL_UNSIGNED_BYTE, nullptr);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
        glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    }

    void init_fbo(){
        glGenFramebuffers(1, &fbo);
        glBindFramebuffer(GL_FRAMEBUFFER, fbo);

        glGenTextures(1, &render_texture);
        glBindTexture(GL_TEXTURE_2D, render_texture);
        glTexImage2D(GL_TEXTURE_2D, 0, GL_RGB, width, height, 0, GL_RGB, GL_UNSIGNED_BYTE, nullptr);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
        glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
        glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

        glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0 , GL_TEXTURE_2D, render_texture, 0);

        // unbind
        glBindTexture(GL_TEXTURE_2D, 0);
        //glBindRenderbuffer(GL_RENDERBUFFER, 0);
        glBindFramebuffer(GL_FRAMEBUFFER, 0);

        if (glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) {
            log("create framebuffer failed");
        }
    }

    GLuint build_shader(int shader_type, const char * source) {
        if (source==nullptr)
        {
            log("shader source is empty");
            return 0;
        }
        GLuint shader = glCreateShader(shader_type);

        if (0 == shader)
        {
            log("Create vertex shader failed ");
            return 0;
        }

        const GLchar* vCodeArray[1] = { source };
        glShaderSource(shader, 1, vCodeArray, NULL);
        glCompileShader(shader);

        GLint compileResult;
        glGetShaderiv(shader, GL_COMPILE_STATUS, &compileResult);
        if (GL_FALSE == compileResult)
        {
            GLint logLen;

            glGetShaderiv(shader, GL_INFO_LOG_LENGTH, &logLen);
            if (logLen > 0)
            {
                char* info = (char*)malloc(logLen);
                GLsizei written;
                glGetShaderInfoLog(shader, logLen, &written, info);
                //log("vertex shader compile log : ");
                log("vertex shader compile log : %s",info);
                free(info);
            }
            return 0;
        }
        return shader;
    }

    bool build_program(GLuint fs, GLuint vs) {
        GLuint program = glCreateProgram();
        if (!program)
        {
            log("create program failed");
            return false;
        }

        if (fs != 0 && vs != 0) {
            glAttachShader(program, fs);
            glAttachShader(program, vs);
        }

        glLinkProgram(program);

        GLint link_status;
        glGetProgramiv(program, GL_LINK_STATUS, &link_status);
        if (GL_FALSE == link_status)
        {
            log("ERROR : link shader program failed");
            GLint logLen;
            glGetProgramiv(program, GL_INFO_LOG_LENGTH, &logLen);
            if (logLen > 0)
            {
                char* info = (char*)malloc(logLen);
                GLsizei written;
                glGetProgramInfoLog(program, logLen,
                                    &written, info);
                log("%s",info);
            }
            glDeleteProgram(program);
            return false;
        }

        shader = program;
        return true;
    }

    static void printGLString(const char *name, GLenum s) {
        const char *v = (const char *) glGetString(s);
        LOGI("GL %s = %s\n", name, v);
    }

    static void checkGlError(const char* op) {
        for (GLint error = glGetError(); error; error = glGetError()) {
            LOGI("after %s() glError (0x%x)\n", op, error);
        }
    }
private:
    int width=0;
    int height = 0;
    GLuint shader=0;

    GLuint texture_y=-1;
    GLuint texture_uv=-1;

    GLuint position_loc = -1;
    GLuint coord_loc = -1;

    int y_loc = -1;
    int uv_loc = -1;

    GLuint render_texture=-1;
    GLuint fbo=-1;
#ifdef BLIT_TO_SCREEN
    BlitPass blit_pass;
#endif
};
