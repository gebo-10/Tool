<template>
  <n-layout-header bordered style="padding: 0 24px; height: 64px; display: flex; align-items: center; justify-content: space-between; background: #fff">
    <!-- 左侧：品牌 / 标题 -->
    <div class="left">
      <h2 style="margin: 0;">HMI CI</h2>
    </div>

    <!-- 右侧：用户信息与操作 -->
    <div class="right">
      <n-dropdown trigger="click" :options="dropdownOptions" @select="handleSelect">
        <n-button text type="primary">
          <span style="margin-right: 8px;">{{ username }}</span>
          <n-icon :component="ChevronDownIcon" />
        </n-button>
      </n-dropdown>
    </div>
  </n-layout-header>
</template>

<script setup>
import { computed } from 'vue';
import { useRouter } from 'vue-router';
import { NLayoutHeader, NDropdown, NButton, NIcon } from 'naive-ui';
import { ChevronDown } from '@vicons/ionicons5'; // 需要安装 @vicons/ionicons5 或换成其他图标

const router = useRouter();

// 从 localStorage 或 store 中获取用户名
const username = computed(() => {
  // 简单方案：登录时将用户名存入 localStorage
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
    // 跳转到设置页面（可后续实现）
    // router.push('/settings');
    console.log('设置');
  }
};
</script>

<style scoped>
.left, .right {
  display: flex;
  align-items: center;
}
</style>