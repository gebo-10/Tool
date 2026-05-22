<script setup>
import { ref, onMounted } from 'vue'

// 用来存储天气数据的数组
const weatherList = ref([])
// 加载状态
const loading = ref(true)
// 错误信息
const errorMsg = ref('')

// 页面挂载后自动请求数据
onMounted(async () => {
  try {
    const res = await fetch('/api/weatherforecast')
    if (!res.ok) {
      throw new Error('请求失败：' + res.status)
    }
    weatherList.value = await res.json()
  } catch (err) {
    errorMsg.value = err.message
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="app">
    <h1>HMI Build - 天气数据</h1>

    <!-- 加载中 -->
    <p v-if="loading">加载中...</p>

    <!-- 错误提示 -->
    <p v-else-if="errorMsg" class="error">出错了：{{ errorMsg }}</p>

    <!-- 数据表格 -->
    <table v-else>
      <thead>
        <tr>
          <th>日期</th>
          <th>温度(°C)</th>
          <th>温度(°F)</th>
          <th>摘要</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in weatherList" :key="item.date">
          <td>{{ item.date }}</td>
          <td>{{ item.temperatureC }}</td>
          <td>{{ item.temperatureF }}</td>
          <td>{{ item.summary }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.app {
  max-width: 700px;
  margin: 30px auto;
  font-family: Arial, sans-serif;
}

table {
  width: 100%;
  border-collapse: collapse;
  margin-top: 20px;
}

th, td {
  border: 1px solid #ddd;
  padding: 10px;
  text-align: center;
}

th {
  background-color: #f2f2f2;
}

.error {
  color: red;
  font-weight: bold;
}
</style>