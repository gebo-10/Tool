<template>
  <div class="login-container">
    <n-card title="HMI CI" style="width: 420px">
      <n-tabs default-value="login" type="line" animated>
        <!-- 登录面板 -->
        <n-tab-pane name="login" tab="登录">
          <n-form ref="loginFormRef" :model="loginForm" :rules="loginRules">
            <n-form-item label="用户名" path="username">
              <n-input v-model:value="loginForm.username" placeholder="请输入用户名" />
            </n-form-item>
            <n-form-item label="密码" path="password">
              <n-input
                v-model:value="loginForm.password"
                type="password"
                placeholder="请输入密码"
                @keyup.enter="handleLogin"
              />
            </n-form-item>
            <n-button type="primary" block :loading="loginLoading" @click="handleLogin">
              登录
            </n-button>
          </n-form>
          <n-alert v-if="loginError" type="error" :title="loginError" style="margin-top: 16px" />
        </n-tab-pane>

        <!-- 注册面板 -->
        <n-tab-pane name="register" tab="注册">
          <n-form ref="regFormRef" :model="regForm" :rules="regRules">
            <n-form-item label="用户名" path="username">
              <n-input v-model:value="regForm.username" placeholder="请输入用户名" />
            </n-form-item>
            <n-form-item label="密码" path="password">
              <n-input
                v-model:value="regForm.password"
                type="password"
                placeholder="请输入密码"
              />
            </n-form-item>
            <n-form-item label="确认密码" path="confirmPassword">
              <n-input
                v-model:value="regForm.confirmPassword"
                type="password"
                placeholder="请再次输入密码"
              />
            </n-form-item>
            <n-button type="primary" block :loading="regLoading" @click="handleRegister">
              注册
            </n-button>
          </n-form>
          <n-alert v-if="regError" type="error" :title="regError" style="margin-top: 16px" />
          <n-alert v-if="regSuccess" type="success" title="注册成功，请登录" style="margin-top: 16px" />
        </n-tab-pane>
      </n-tabs>
    </n-card>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import request from '../utils/request';

const router = useRouter();

// ---------- 登录相关 ----------
const loginFormRef = ref(null);
const loginForm = ref({ username: '', password: '' });
const loginLoading = ref(false);
const loginError = ref('');

const loginRules = {
  username: { required: true, message: '请输入用户名', trigger: 'blur' },
  password: { required: true, message: '请输入密码', trigger: 'blur' },
};

const handleLogin = async () => {
  loginError.value = '';
  // 表单验证
  try {
    await loginFormRef.value?.validate();
  } catch {
    return;
  }

  loginLoading.value = true;
  try {
    const res = await request.post('/auth/login', loginForm.value);
    const { token, username } = res.data;
    localStorage.setItem('token', token);
    localStorage.setItem('username', username);
    router.push('/');
  } catch (err) {
    loginError.value = err.response?.data || '登录失败';
  } finally {
    loginLoading.value = false;
  }
};

// ---------- 注册相关 ----------
const regFormRef = ref(null);
const regForm = ref({ username: '', password: '', confirmPassword: '' });
const regLoading = ref(false);
const regError = ref('');
const regSuccess = ref(false);

// 自定义确认密码校验
const validateConfirmPassword = (rule, value) => {
  if (value !== regForm.value.password) {
    return new Error('两次密码输入不一致');
  }
  return true;
};

const regRules = {
  username: { required: true, message: '请输入用户名', trigger: 'blur' },
  password: { required: true, message: '请输入密码', trigger: 'blur' },
  confirmPassword: [
    { required: true, message: '请确认密码', trigger: 'blur' },
    { validator: validateConfirmPassword, trigger: ['blur', 'change'] }
  ],
};

const handleRegister = async () => {
  regError.value = '';
  regSuccess.value = false;
  try {
    await regFormRef.value?.validate();
  } catch {
    return;
  }

  regLoading.value = true;
  try {
    await request.post('/auth/register', {
      username: regForm.value.username,
      password: regForm.value.password,
    });
    regSuccess.value = true;
    regForm.value = { username: '', password: '', confirmPassword: '' }; // 清空表单
  } catch (err) {
    regError.value = err.response?.data || '注册失败';
  } finally {
    regLoading.value = false;
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