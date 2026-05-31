<template>
  <DagViewer :graph-data="graphData" />
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue';
import DagViewer from '../components/DagViewer.vue';
import request from '../utils/request';
import { useRoute } from 'vue-router';
const route = useRoute();

const graphData = ref({ nodes: [], edges: [] });
let eventSource = null;

// 1. 首次获取 DAG 结构
const fetchDag = async (pipelineId) => {
  const res = await request.get(`/pipelines/${pipelineId}`);
  console.log('初始 DAG 数据', res);
  graphData.value = res.data;
};

// 2. 连接 SSE 接收节点实时状态更新
const connectSSE = (pipelineId) => {
  eventSource = new EventSource(`/api/pipeline/${pipelineId}/dag-updates?token=${localStorage.getItem('token')}`);
  eventSource.onmessage = (event) => {
    const update = JSON.parse(event.data); // 格式：{ nodeId, status, progress }
    const node = graphData.value.nodes.find(n => n.id === update.nodeId);
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