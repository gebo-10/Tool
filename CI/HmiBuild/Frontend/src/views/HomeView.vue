<script setup>
import WeatherForecast from '../components/WeatherForecast.vue';
import LogViewer from '../components/LogViewer.vue';
import HeaderBar from '../components/HeaderBar.vue';
//import PipelineDetail from '../components/PipelineDetail.vue';
import G6Test from '../components/G6Test.vue';
import G6VueNode from '../components/G6VueNode.vue';
</script>

<template>
  <div class="home">
    <HeaderBar />
    <div class="content">
      <!-- <G6VueNode /> -->
      <hr />
      <WeatherForecast />
      <hr />
      <!-- <PipelineDetail /> -->
      <hr />
      <!-- <LogViewer /> -->
      <hr />
      <G6Test />
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