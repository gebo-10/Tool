<script setup>
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import { NScrollbar } from 'naive-ui'  // 如果按需引入

const logs = ref([])
const scrollRef = ref(null)      // 用于操作滚动条
let eventSource = null

// 自动滚动到底部
const scrollToBottom = async () => {
  await nextTick()
  if (scrollRef.value) {
    const el = scrollRef.value.$el?.querySelector('.n-scrollbar-content')
    if (el) el.scrollTop = el.scrollHeight
  }
}

onMounted(() => {
  eventSource = new EventSource('/api/logs')

  eventSource.onmessage = (event) => {
    const log = JSON.parse(event.data)
    logs.value.push(log)
    // 保留最近 500 条，防止数组过大
    if (logs.value.length > 500) logs.value.shift()
    scrollToBottom()
  }

  eventSource.onerror = () => {
    // 可处理重连或提示
  }
})

onUnmounted(() => {
  eventSource?.close()
})
</script>

<template>
  <div class="log-viewer">
    <h2>实时日志</h2>

    <!-- 固定高度区域，用 naive-ui Scrollbar -->
    <n-scrollbar ref="scrollRef" style="max-height: 400px; border: 1px solid #ddd; border-radius: 4px;">
      <div class="log-content">
        <div
          v-for="(log, idx) in logs"
          :key="idx"
          class="log-line"
          :class="log.level"
        >
          <span class="time">{{ log.time }}</span>
          <span class="level">[{{ log.level }}]</span>
          <span class="msg">{{ log.message }}</span>
        </div>
      </div>
    </n-scrollbar>
  </div>
</template>

<style scoped>
.log-viewer {
  max-width: 800px;
  margin: 20px auto;
}

.log-content {
  padding: 10px;
  font-family: 'Courier New', monospace;
  font-size: 14px;
}

.log-line {
  padding: 2px 0;
  border-bottom: 1px solid #eee;
}

.log-line.ERROR {
  color: #d32f2f;
  font-weight: bold;
}

.time { color: #666; margin-right: 8px; }
.level { margin-right: 8px; }
.msg { color: #333; }
</style>