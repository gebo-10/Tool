<script setup>
import WeatherForecast from '../components/WeatherForecast.vue';
import LogViewer from '../components/LogViewer.vue';
import HeaderBar from '../components/HeaderBar.vue';
import PipelineList from '../components/PipelineList.vue';
import CreatePipeline from '../components/CreatePipeline.vue';
import { ref } from 'vue';
import { NMessageProvider } from 'naive-ui';
const showCreateModal = ref(false);


// 列表组件引用，用于刷新
const pipelineListRef = ref(null);

const handleCreated = () => {
  //showCreateModal.value = false;
  // 触发列表刷新（如果 PipelineList 暴露了刷新方法）
  pipelineListRef.value?.fetchTasks();
};
</script>

<template>
  <n-message-provider>
    <div class="home">
      <HeaderBar />
      <div class="content">
        <div class="toolbar">
          <n-button type="primary" @click="showCreateModal = true">创建流水线</n-button>
        </div>
        <PipelineList ref="pipelineListRef" />
      </div>
      <CreatePipeline
        v-model:show="showCreateModal"
        @created="handleCreated"
      />
    </div>
  </n-message-provider>
</template>

<style scoped>
.home {
  min-height: 100vh;
  background: #f5f5f5;
}
.content {
  max-width: 90vw;
  margin: 20px auto;
  background: #fff;
  padding: 20px;
  border-radius: 6px;
}
.toolbar {
  margin-bottom: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>