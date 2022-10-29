#include <jni.h>
#include <android/log.h>

#include <GLES2/gl2.h>
#include <GLES2/gl2ext.h>

#include <stdio.h>
#include <stdlib.h>
#include <math.h>
#include <fstream>
#include <vector>

#include <dirent.h>
#include <unistd.h>

#include "svpng.inc.h"

#define  LOG_TAG    "libgl2jni"
#define  LOGI(...)  __android_log_print(ANDROID_LOG_INFO,LOG_TAG,__VA_ARGS__)
#define  LOGE(...)  __android_log_print(ANDROID_LOG_ERROR,LOG_TAG,__VA_ARGS__)

void get_file(const std::string&  filename, std::vector<unsigned char >&  data) {
    std::ifstream filestr(filename, std::ios::binary);

    data.clear();
    // 要读入整个文件，必须采用二进制打开
    //filestr.open (dir+"/"+filename, std::ios::binary);
    if (!filestr)
    {
        LOGE("cant open file %s");
    }
    // 获取filestr对应buffer对象的指针
    auto pbuf=filestr.rdbuf();

    // 调用buffer对象方法获取文件大小
    auto size=pbuf->pubseekoff (0,std::ios::end,std::ios::in);
    pbuf->pubseekpos (0,std::ios::in);

    // 分配内存空间
    data.resize(size);
    // 获取文件内容
    pbuf->sgetn ((char *)data.data(),size);
    //buffer[size-1]='\0';
    filestr.close();
}

#include "nv12_to_rgb.h""
NV12ToRGB * render;

extern "C" {
    JNIEXPORT void JNICALL Java_com_android_gl2jni_GL2JNILib_init(JNIEnv * env, jobject obj,  jint width, jint height);
    JNIEXPORT void JNICALL Java_com_android_gl2jni_GL2JNILib_step(JNIEnv * env, jobject obj);
};

JNIEXPORT void JNICALL Java_com_android_gl2jni_GL2JNILib_init(JNIEnv * env, jobject obj,  jint width, jint height)
{
    if(render==nullptr){
        render=new NV12ToRGB(960,544);
        render->init();
    }

    std::vector<unsigned char> nv12_data;
    get_file("/data/data/com.android.gl2jni/files/b960x544.yuv",nv12_data);

    render->update_data(nv12_data.data());


}

JNIEXPORT void JNICALL Java_com_android_gl2jni_GL2JNILib_step(JNIEnv * env, jobject obj)
{
    render->draw();

    std::vector<unsigned  char> data;
    render->read_pixel(data);
    FILE *fp = fopen("/data/data/com.android.gl2jni/files/rgb.png", "wb");

    svpng(fp, 960, 544, data.data(), 0);
    fclose(fp);
}
