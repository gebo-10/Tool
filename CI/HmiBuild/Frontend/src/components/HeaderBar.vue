<template>
  <n-layout-header bordered
    style="padding: 0 24px; height: 38px; display: flex; align-items: center; justify-content: space-between; background: #fff">
    <!-- 左侧：品牌 / 标题（可点击返回首页，文字渐变） -->
    <div class="left" @click="goHome" style="cursor: pointer;">
      <h2 class="brand-title">HMI CI</h2>
    </div>

    <!-- 右侧：用户信息与操作 -->
    <div class="right">
      <n-dropdown trigger="click" :options="dropdownOptions" @select="handleSelect">
        <n-button text type="primary">
          <span style="margin-right: 8px;">{{ username }}</span>
          <n-icon :component="ChevronDown" />
        </n-button>
      </n-dropdown>
    </div>
  </n-layout-header>
</template>

<script setup>
import { computed } from 'vue';
import { useRouter } from 'vue-router';
import { NLayoutHeader, NDropdown, NButton, NIcon } from 'naive-ui';
import { ChevronDown } from '@vicons/ionicons5';

const router = useRouter();

const username = computed(() => {
  return localStorage.getItem('username') || '未知用户';
});

const dropdownOptions = [
  { label: '设置', key: 'settings' },
  { label: '退出登录', key: 'logout' },
];

const handleSelect = (key) => {
  if (key === 'logout') {
    localStorage.removeItem('token');
    localStorage.removeItem('username');
    router.push('/login');
  } else if (key === 'settings') {
    console.log('设置');
  }
};

// 点击标题返回首页
const goHome = () => {
  router.push('/');
};
</script>

<style scoped>
.left,
.right {
  display: flex;
  align-items: center;
}

/* 品牌文字渐变 */
.brand-title {
  margin: 0;
  font-size: 20px;
  font-weight: bold;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  -webkit-background-clip: text;
  background-clip: text;
  -webkit-text-fill-color: transparent;
  text-fill-color: transparent;
  /* 可选：增加一点投影 */
  filter: drop-shadow(0 1px 2px rgba(102, 126, 234, 0.3));
}
</style>