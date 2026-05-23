<script setup>
import WeatherForecast from './components/WeatherForecast.vue'
import LogViewer from './components/LogViewer.vue'
</script>

<template>
  <div class="app">
    <h1>HMI Build 控制面板</h1>
    <WeatherForecast />
    <hr />
    <LogViewer />
  </div>
</template>

<style scoped>
.app {
  max-width: 900px;
  margin: 30px auto;
  font-family: Arial, sans-serif;
}
</style>