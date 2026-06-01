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

      <div class="logs-section">
        <h4>实时日志</h4>
        <div class="log-container" ref="logContainer">
          <div v-for="(log, idx) in logs" :key="idx" :class="['log-line', log.level]">
            <!-- <span class="log-time">{{ log.time }}</span> -->
            <!-- ⭐ 使用 v-html 渲染富文本 -->
            <span class="log-msg" v-html="renderLogMessage(log.message)"></span>
          </div>
          <n-empty v-if="!logs.length && !logLoading" description="暂无日志" />
        </div>
      </div>
    </n-spin>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, nextTick, computed ,watch } from 'vue';
import { useRoute } from 'vue-router';                         // 新增
import { NSpin, NDescriptions, NDescriptionsItem, NTag, NEmpty } from 'naive-ui';
const route = useRoute();   
// 从路由参数获取 pipelineId（假设路由定义为 /pipelines/:id/tasks/:taskId）
const pipelineId = computed(() => Number(route.params.id));

const props = defineProps({
  //pipelineId: { type: Number, required: true },  // 新增
  taskId: { type: String, required: true }
});

watch(() => props.taskId, (newId, oldId) => {
  if (newId && newId !== oldId) {
    // taskId 发生了变化，重新加载
    //loadTaskDetail();
    startLogStream();
  }
});

const emit = defineEmits(['close']);

// 任务数据
const task = ref(null);
const loading = ref(false);
const logs = ref([]);
const logLoading = ref(false);
const logContainer = ref(null);
let abortController = null;   // 用于取消 fetch 流

// ---------- 工具函数 ----------
const getAuthHeaders = () => {
  const token = localStorage.getItem('token');  // 根据实际存储方式调整
  return token ? { 'Authorization': `Bearer ${token}` } : {};
};

const statusType = (status) => {
  const map = { pending: 'default', running: 'info', completed: 'success', failed: 'error' };
  return map[status] || 'default';
};

// ---------- 加载任务详情 ----------
const loadTaskDetail = async () => {
  loading.value = true;
  try {
    const res = await fetch(`/api/pipelines/${pipelineId.value}/tasks/${props.taskId}`, {
      headers: getAuthHeaders()
    });
    if (res.ok) {
      const data = await res.json();
      task.value = {
        id: data.id,
        label: data.label || data.name,
        status: data.status,
        progress: data.progress ?? 0,
        startTime: data.startTime ? new Date(data.startTime).toLocaleString() : '-',
        endTime: data.endTime ? new Date(data.endTime).toLocaleString() : null,
        parameters: data.parameters ? JSON.stringify(data.parameters) : '-'
      };
    } else {
      console.error('获取任务详情失败');
    }
  } catch (err) {
    console.error('获取任务详情出错', err);
  } finally {
    loading.value = false;
  }
};

// ---------- 启动 SSE 日志流 ----------
const startLogStream = () => {
  // 取消上一次连接
  if (abortController) {
    abortController.abort();
  }
  logs.value = [];
  logLoading.value = true;
  abortController = new AbortController();

  fetch(`/api/pipelines/${pipelineId.value}/tasks/${props.taskId}/logs`, {
    headers: {
      ...getAuthHeaders(),
      'Accept': 'text/event-stream'
    },
    signal: abortController.signal
  }).then(async (response) => {
    if (!response.ok) {
      throw new Error('SSE 连接失败');
    }
    logLoading.value = false;

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      // 按 \n\n 分割 SSE 事件
      const parts = buffer.split('\n\n');
      buffer = parts.pop() || '';  // 最后一部分可能不完整，保留下次拼接

      for (const part of parts) {
        if (!part.trim()) continue;
        // 解析 "data: ..."
        const lines = part.split('\n');
        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const jsonStr = line.substring(6);
            try {
              const data = JSON.parse(jsonStr);
              if (data.lines) {
                data.lines.forEach(msg => {
                  const level = parseLogLevel(msg) || 'INFO';
                  logs.value.push({
                    time: new Date().toLocaleTimeString(),
                    level: level,
                    message: msg
                  });
                });
              } else if (data.line !== undefined) {
                const level = parseLogLevel(data.line) || 'INFO';
                logs.value.push({
                  time: new Date().toLocaleTimeString(),
                  level: level,
                  message: data.line
                });
              } else if (data.end) {
                console.log('日志流结束');
                abortController.abort(); // 触发 cleanup
                return;
              }
            } catch (e) {
              console.warn('解析日志数据失败', e);
            }
          }
        }
      }

      // 自动滚动到底部
      await nextTick();
      if (logContainer.value) {
        logContainer.value.scrollTop = logContainer.value.scrollHeight;
      }
    }
  }).catch(err => {
    if (err.name !== 'AbortError') {
      console.error('日志流错误', err);
    }
    logLoading.value = false;
  });
};

// 辅助函数：从日志行中提取级别（如 [WRN]）
const parseLogLevel = (line) => {
  const match = line.match(/^.*?\[(\w{3})\]/); // 匹配开头的 [WRN], [ERR], [INF] 等
  return match ? match[1].toUpperCase() : null;
};

// ---------- 新增：日志消息中的链接渲染 ----------
const renderLogMessage = (msg) => {
  if (!msg) return '';

  // 定义匹配和替换规则
  const patterns = [
    {
      regex: /\b(Artifact\/[^\s<>]+)/g,
      process: (match) => {
        const fullUrl = window.location.origin + '/' + match;
        const fileName = match.split('/').pop();
        return `<a href="${fullUrl}" target="_blank" rel="noopener noreferrer" class="log-link">${fileName}</a>`;
      }
    },
    {
      regex: /(https?:\/\/[^\s<>]+)/g,
      process: (match) => `<a href="${match}" target="_blank" rel="noopener noreferrer" class="log-link">${match}</a>`
    }
  ];

  let result = msg;
  const replacements = [];

  // 1. 用特殊占位符替换所有匹配的链接
  patterns.forEach(({ regex, process }) => {
    result = result.replace(regex, (match) => {
      const placeholder = `\x00LINK${replacements.length}\x00`;
      replacements.push(process(match));
      return placeholder;
    });
  });

  // 2. 对剩余纯文本进行 HTML 转义（跳过占位符）
  result = result.replace(/[&<>"']/g, (char) => {
    const map = {
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#039;'
    };
    return map[char];
  });

  // 3. 将占位符替换回真正的 <a> 标签（不会被转义）
  result = result.replace(/\x00LINK(\d+)\x00/g, (_, index) => {
    return replacements[Number(index)];
  });

  return result;
};


// ---------- 生命周期 ----------
onMounted(() => {
  //loadTaskDetail();
  startLogStream();
});

onUnmounted(() => {
  if (abortController) abortController.abort();
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
  /* max-height: 300px; */
  /* overflow-y: auto; */
  background: #f9f9f9;
  padding: 8px;
  font-family: monospace;
  font-size: 13px;
  border-radius: 4px;
  border: 1px solid #eee;
}



</style>

<style>
/* 全局样式，影响所有 .log-link */
.log-link {
  color: #19974d;
  text-decoration: none;
  font-size: 22px;
  font-style: italic;
  font-weight: bold;
}
.log-link:hover {
  color: #284233;
}

.log-line {
  border-bottom: 1px solid #f0f0f0;
  padding: 2px 0;
}
.log-line.WRN { color: #b47e13; }   /* 黄色 */
.log-line.ERR { color: #d32f2f; }   /* 红色 */
.log-line.INF { color: inherit; }   /* 默认色 */
</style>