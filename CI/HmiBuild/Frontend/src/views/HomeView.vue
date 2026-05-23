<script setup>
import WeatherForecast from '../components/WeatherForecast.vue';
import LogViewer from '../components/LogViewer.vue';
</script>

<template>
  <div class="home">
    <h1>HMI Build 控制面板</h1>
    <WeatherForecast />
    <hr />
    <LogViewer />
  </div>
</template>

<style scoped>
.home {
  max-width: 900px;
  margin: 30px auto;
}
</style>