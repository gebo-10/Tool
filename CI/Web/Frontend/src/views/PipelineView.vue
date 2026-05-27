<template>
  <HeaderBar />
  <div class="pipeline-view">
    <!-- 可以显示任务基本信息头部 -->
    <div class="pipeline-header">
      <h2>任务详情 - {{ pipelineId }}</h2>
    </div>

    <!-- DAG 图区域 -->
    <div class="dag-area">
      <G6Test :pipeline-id="pipelineId" />
    </div>
  </div>
</template>

<script setup>
import { useRoute } from 'vue-router';
import G6Test from '../components/G6Test.vue';   // 路径根据你的实际结构调整
import HeaderBar from '../components/HeaderBar.vue';

const route = useRoute();
const pipelineId = route.params.id;   // 对应路由 /tasks/:id
</script>

<style scoped>
.pipeline-view {
  padding: 16px;
  height: 100%;
}
.pipeline-header {
  margin-bottom: 16px;
}
.dag-area {
  height: calc(100vh - 120px);  /* 根据布局调整 */
}
</style>