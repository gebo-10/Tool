<template>
  <div class="login-container">
    <n-card title="HMI Build 登录" style="width: 400px">
      <n-form ref="formRef" :model="form" :rules="rules">
        <n-form-item label="用户名" path="username">
          <n-input v-model:value="form.username" placeholder="admin" />
        </n-form-item>
        <n-form-item label="密码" path="password">
          <n-input
            v-model:value="form.password"
            type="password"
            placeholder="123456"
            @keyup.enter="handleLogin"
          />
        </n-form-item>
        <n-button type="primary" block :loading="loading" @click="handleLogin">
          登录
        </n-button>
      </n-form>
      <n-alert v-if="error" type="error" :title="error" style="margin-top: 16px" />
    </n-card>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import request from '../utils/request';

const router = useRouter();
const form = ref({ username: '', password: '' });
const loading = ref(false);
const error = ref('');


const token = localStorage.getItem('token');
const eventSource = new EventSource(`/api/logs?token=${encodeURIComponent(token)}`);

const rules = {
  username: { required: true, message: '请输入用户名', trigger: 'blur' },
  password: { required: true, message: '请输入密码', trigger: 'blur' },
};

const handleLogin = async () => {
  //error.value = '';
  loading.value = true;
  try {
    const res = await request.post('/auth/login', form.value);
    const { token } = res.data;
    localStorage.setItem('token', token);
    router.push('/');          // 跳转到首页
  } catch (err) {
    error.value = err.response?.data || '登录失败';
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.login-container {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100vh;
  background-color: #f5f5f5;
}
</style>