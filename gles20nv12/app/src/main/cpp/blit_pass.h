#pragma once

#include "GLES2/gl2.h"
#include <android/log.h>
#define LOG_TAG  "KEL_TAG"
#define LOGD(...)  __android_log_print(ANDROID_LOG_DEBUG, LOG_TAG, __VA_ARGS__)
#define log LOGD

//#define GLEW_STATIC
//#include "GL/glew.h"
//void log(const char * info) {
//   // std::cout << info << std::endl;
//    LOGD(info);
//}

#include <iostream>
#include <string>


class BlitPass
{
public:
    int width=0;
    int height = 0;
    GLuint shader=0;

    GLuint position_loc = -1;
    GLuint coord_loc = -1;

    int texture_loc = -1;


    BlitPass(int video_width, int vieo_height):width(video_width),height(vieo_height)
    {

    }

    ~BlitPass()
    {
        glDeleteProgram(shader);
    }

    void init() {
        //glClearColor(0.5,0.2,0.9,1.0);
        init_shader();
    }

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
                "uniform sampler2D texture;                       \n"
                "void main (void){                                  \n"
                "   gl_FragColor  = texture2D(texture, v_texCoord);         \n"
                "}                                                  \n";
        // Load the shaders and get a linked program object
        GLuint vs = build_shader(GL_VERTEX_SHADER, vs_source);
        GLuint fs = build_shader(GL_FRAGMENT_SHADER, fs_source);
        build_program(vs, fs);

        position_loc = glGetAttribLocation(shader, "vPosition");
        coord_loc = glGetAttribLocation(shader, "vCoord");

        // Get the sampler location
        texture_loc = glGetUniformLocation(shader, "texture");
    }


    void draw(GLuint texture) {
        static float vVertices[] = {
                -1.0f,  1.0f, 0.0f,  // Position 0
                0.0f,  0.0f,        // TexCoord 0
                -1.0f, -1.0f, 0.0f,  // Position 1
                0.0f,  1.0f,        // TexCoord 1
                1.0f, -1.0f, 0.0f,  // Position 2
                1.0f,  1.0f,        // TexCoord 2
                1.0f,  1.0f, 0.0f,  // Position 3
                1.0f,  0.0f         // TexCoord 3
        };
        static GLushort indices[] ={ 0, 1, 2, 0, 2, 3 };

        // Set the viewport
        glViewport(0, 0, width, height);

        glClearColor(1.0,0.0,0.0,1.0);
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
        glBindTexture(GL_TEXTURE_2D,texture);
        glUniform1i(texture_loc, 0);

        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_SHORT, indices);
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
};
