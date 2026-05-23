<script setup>
import { ref, onMounted } from 'vue'

const weatherList = ref([])
const loading = ref(true)
const errorMsg = ref('')

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
  <div class="weather">
    <h2>天气数据</h2>

    <p v-if="loading">加载中...</p>
    <p v-else-if="errorMsg" class="error">出错了：{{ errorMsg }}</p>

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
.weather {
  max-width: 700px;
  margin: 20px auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  margin-top: 10px;
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