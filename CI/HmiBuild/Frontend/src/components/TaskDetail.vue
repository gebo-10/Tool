<template>
  <div class="task-detail">
    <n-spin :show="loading">
      <n-descriptions v-if="task" title="基本信息" :column="2" bordered>
        <n-descriptions-item label="任务ID">{{ task.id }}</n-descriptions-item>
        <n-descriptions-item label="名称">{{ task.label }}</n-descriptions-item>
        <n-descriptions-item label="状态">
          <n-tag :type="statusType(task.status)">{{ task.status }}</n-tag>
        </n-descriptions-item>
        <n-descriptions-item label="进度">{{ task.progress }}%</n-descriptions-item>
        <n-descriptions-item label="开始时间">{{ task.startTime || '-' }}</n-descriptions-item>
        <n-descriptions-item label="结束时间">{{ task.endTime || '-' }}</n-descriptions-item>
        <n-descriptions-item label="参数" :span="2">{{ task.parameters || '-' }}</n-descriptions-item>
      </n-descriptions>

      <!-- 实时日志展示 -->
      <div class="logs-section">
        <h4>实时日志</h4>
        <div class="log-container" ref="logContainer">
          <div v-for="(log, idx) in logs" :key="idx" class="log-line" :class="log.level">
            <span class="log-time">{{ log.time }}</span>
            <span class="log-msg">{{ log.message }}</span>
          </div>
          <n-empty v-if="!logs.length && !logLoading" description="暂无日志" />
          <Logviewer />
        </div>
      </div>
    </n-spin>
  </div>
</template>

<script setup>
import { ref, watch, onUnmounted, nextTick } from 'vue';
import { NSpin, NDescriptions, NDescriptionsItem, NTag, NEmpty } from 'naive-ui';

import Logviewer from './LogViewer.vue';  // 可选：如果需要更复杂的日志组件
const props = defineProps({
  taskId: { type: String, required: true }
});

const emit = defineEmits(['close']);

// 任务数据
const task = ref(null);
const loading = ref(false);
const logs = ref([]);
const logLoading = ref(false);
const logContainer = ref(null);
let logEventSource = null;

// 模拟数据（实际应从 API 获取）
const mockTaskData = (id) => ({
  id,
  label: `任务 ${id}`,
  status: 'running',
  progress: 45,
  startTime: new Date().toLocaleString(),
  endTime: null,
  parameters: '{"param1":"value1"}',
});

// 模拟日志生成
const mockLogs = () => {
  const messages = ['初始化完成', '正在处理...', '数据读取中...', '校验通过', '步骤1完成'];
  return {
    time: new Date().toLocaleTimeString(),
    level: 'INFO',
    message: messages[Math.floor(Math.random() * messages.length)]
  };
};

// 监听 taskId 变化，加载任务详情
// watch(() => props.taskId, (newId) => {
//   if (newId) {
//     loadTaskDetail(newId);
//     startLogStream(newId);
//   }
// }, { immediate: true });

const loadTaskDetail = async (id) => {
  loading.value = true;
  // TODO: 替换为真实 API 请求
  await new Promise(resolve => setTimeout(resolve, 300));
  task.value = mockTaskData(id);
  loading.value = false;
};

const startLogStream = (id) => {
  // 先关闭旧连接
  if (logEventSource) {
    logEventSource.close();
    logEventSource = null;
  }
  logs.value = [];
  logLoading.value = true;

  // 模拟 SSE 或轮询
  // 实际可替换为 EventSource('/api/tasks/' + id + '/logs?token=...')
  const interval = setInterval(() => {
    logs.value.push(mockLogs());
    // 滚动到底部
    nextTick(() => {
      if (logContainer.value) {
        logContainer.value.scrollTop = logContainer.value.scrollHeight;
      }
    });
  }, 1000);
  
  logEventSource = { close: () => clearInterval(interval) }; // 模拟关闭方法
  logLoading.value = false;
};

// 辅助函数：状态对应的 tag 类型
const statusType = (status) => {
  const map = { pending: 'default', running: 'info', completed: 'success', failed: 'error' };
  return map[status] || 'default';
};

onUnmounted(() => {
  if (logEventSource) logEventSource.close();
});
</script>

<style scoped>
.task-detail {
  padding: 10px 0;
}
.logs-section {
  margin-top: 20px;
}
.log-container {
  max-height: 300px;
  overflow-y: auto;
  background: #f9f9f9;
  padding: 8px;
  font-family: monospace;
  font-size: 13px;
  border-radius: 4px;
  border: 1px solid #eee;
}
.log-line {
  border-bottom: 1px solid #f0f0f0;
  padding: 2px 0;
}
.log-line.ERROR { color: #d32f2f; }
.log-time { color: #999; margin-right: 8px; }
.log-msg { color: #333; }
</style>