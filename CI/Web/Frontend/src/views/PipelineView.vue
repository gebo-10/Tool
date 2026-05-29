<template>
  <HeaderBar />
  <div class="pipeline-view">
    <!-- 可以显示任务基本信息头部 -->
    <div class="pipeline-header">
      <h2>任务详情 - {{ pipelineId }}</h2>
    </div>

    <!-- DAG 图区域 -->
    <div class="dag-area">
      <DagViewer :pipeline-id="pipelineId" :graph-data="graphData"/>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue';
import request from '../utils/request';
import { useRoute } from 'vue-router';

import HeaderBar from '../components/HeaderBar.vue';
import DagViewer from '../components/DagViewer.vue';

const route = useRoute();
const pipelineId = route.params.id;   // 对应路由 /tasks/:id
let eventSource = null;
const graphData = ref({ data: { nodes: [], edges: [] } });

const fetchDag = async (pipelineId) => {
  const res = await request.get(`/pipelines/${pipelineId}`);
  console.log('初始 DAG 数据', res);
  graphData.value.data = JSON.parse(res.data.dagJson);
};


// 2. 连接 SSE 接收节点实时状态更新
const connectSSE = (pipelineId) => {
  eventSource = new EventSource(`/api/pipelines/${pipelineId}/dag-updates?token=${localStorage.getItem('token')}`);
  eventSource.onmessage = (event) => {
    const update = JSON.parse(event.data); // 格式：{ nodeId, status, progress }
    const node = graphData.value.data.nodes.find(n => n.id === update.nodeId);
    if (node) {
      node.status = update.status;
      node.progress = update.progress;
    }
    // 触发 watch 更新图形
  };
};

onMounted(() => {
  const pipelineId = route.params.id; // 假设路由为 /pipeline/:id
  console.log('Pipeline ID from route:', pipelineId);
  fetchDag(pipelineId).then(() => connectSSE(pipelineId));
});

onUnmounted(() => {
  eventSource?.close();
});

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