<script setup>
import WeatherForecast from '../components/WeatherForecast.vue';
import LogViewer from '../components/LogViewer.vue';
import HeaderBar from '../components/HeaderBar.vue';
</script>

<template>
  <div class="home">
    <HeaderBar />
    <div class="content">
      <WeatherForecast />
      <hr />
      <LogViewer />
    </div>
  </div>
</template>

<style scoped>
.home {
  min-height: 100vh;
  background: #f5f5f5;
}
.content {
  max-width: 900px;
  margin: 20px auto;
  background: #fff;
  padding: 20px;
  border-radius: 6px;
}
</style>