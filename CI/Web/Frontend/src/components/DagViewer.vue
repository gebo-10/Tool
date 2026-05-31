<template>
  <div class="dag-page">
    <!-- 左侧：DAG 图 -->
    <div class="dag-left" id="graphContainer"></div>

    <!-- 右侧：任务详情面板 -->
    <div class="dag-right">
      <TaskDetail
        v-if="selectedTaskId"
        :task-id="selectedTaskId"
        @close="selectedTaskId = null"
      />
      <n-empty v-else description="点击左侧节点查看任务详情" style="margin-top: 40px;" />
    </div>
  </div>
</template>

<script setup>
import { onMounted, onUnmounted, ref, watch, computed } from 'vue';
import { Rect, register, Graph, ExtensionCategory } from '@antv/g6';
import { NEmpty } from 'naive-ui';
import TaskDetail from './TaskDetail.vue';
import { useRoute } from 'vue-router';
import request from '../utils/request'; 

const route = useRoute();
const pipelineId = computed(() => Number(route.params.id)); // 根据实际路由参数名调整

const props = defineProps({
  graphData: { type: Object, default: () => ({ data: { nodes: [], edges: [] } }) },
});

let graph = null;
const selectedTaskId = ref(null);
let eventSource = null; // SSE 连接引用

// ---------- 自定义节点类（保持不变）----------
class DagTaskNode extends Rect {
  render(attributes = this.parsedAttributes, container) {
    super.render(attributes, container);
    const { label = '', status = 'pending', progress = 0 } = attributes;
    const [width, height] = this.getSize(attributes);
    const startX = -width / 2, startY = -height / 2;
    const statusColors = {
      pending:  { bg: '#E5E7EB', text: '#6B7280', prog: '#9CA3AF' },
      running:  { bg: '#DBEAFE', text: '#1D4ED8', prog: '#3B82F6' },
      completed:{ bg: '#D1FAE5', text: '#047857', prog: '#10B981' },
      failed:   { bg: '#FEE2E2', text: '#B91C1C', prog: '#EF4444' },
      cancelled:{ bg: '#F3F4F6', text: '#e2b00d', prog: '#9CA3AF' },
    };
    const colors = statusColors[status] || statusColors.pending;
    const statusLabel = { pending:'等待中', running:'运行中', completed:'已完成', failed:'失败', cancelled:'已取消' }[status] || status;
    this.upsert('status-name', 'text', { x:startX+10, y:0, text:label, textAlign:'left', textBaseline:'middle', fontSize:14, fontWeight:600, fill:'#8F4997', fontFamily:'system-ui, sans-serif' }, container);
    this.upsert('status-text', 'text', { x:width/2-10, y:0, text:statusLabel, fontSize:12, fill:colors.text, textAlign:'right', textBaseline:'middle' }, container);
    const barY = height/2 - 10, barX = startX+10;
    this.upsert('progress-bg', 'rect', { x:barX, y:barY, width:width-20, height:4, radius:2, fill:'#F3F4F6', stroke:'#E5E7EB' }, container);
    this.upsert('progress-bar', 'rect', { x:barX, y:barY, width:(width-20)*(progress/100), height:4, radius:2, fill:colors.prog }, container);
  }
}
let registered = false;
if (!registered) { register(ExtensionCategory.NODE, 'dag-task', DagTaskNode); registered = true; }

// ---------- 更新节点数据（不触发布局）----------
const updateNode = (nodeId, newData) => {
  if (!graph) return;
  const node = graph.getNodeData(nodeId);
  if (!node) return;
  // 合并原有 data 与新数据
  //const merged = { ...node.data, ...newData };
  console.log('更新节点数据', nodeId, newData);
  graph.updateNodeData([newData]);
  graph.draw(); // 仅重绘节点样式，不重新布局
};

// ---------- 建立 SSE 连接 ----------
const connectSSE = () => {
  if (eventSource) eventSource.close();
  eventSource = new EventSource('/api/pipelines/status-stream');

  eventSource.onmessage = (event) => {
    try {
      const evt = JSON.parse(event.data); // BuildEvent: { eventType, pipelineId, info }
      console.log('SSE 收到事件:', evt);
      if(evt.eventType === 'NodeInfo' && evt.pipelineId === pipelineId.value) {
        var nodeInfo = evt.info; // 假设 info 包含 { id, status, progress, ... }，与节点 data 结构一致  
         if (nodeInfo.id) {
          console.log('更新节点', nodeInfo.id, nodeInfo);
          updateNode(nodeInfo.id, nodeInfo); // 直接用 info 更新节点数据，前提是后端发送的 info 格式正确
        }
      }else if(evt.eventType === 'DagInfo' && evt.pipelineId === pipelineId.value) {
        var dagInfo = evt.info; // 假设 info 包含完整的 DAG 数据 { nodes: [], edges: [] }
        if (dagInfo.nodes && dagInfo.edges) {
          graph.changeData(dagInfo); // 整体替换 DAG 数据，适用于大规模更新
        }
      }
    } catch (e) {
      console.error('SSE message parse error', e);
    }
  };

  eventSource.onerror = (err) => {
    console.error('SSE connection error', err);
    // 浏览器会自动重连，也可以手动处理
  };
};

// ---------- 原有渲染/监听逻辑 ----------
const renderGraph = (data) => {
  if (!graph) return;
  graph.setData(data);
  graph.render().then(() => graph.fitView());
};

// watch(() => props.graphData, (newData) => {
//   if (newData?.data && graph) {
//     graph.setData(props.graphData.data);
//     graph.render().then(() => graph.fitView());
//   }
// }, { deep: true, immediate: true });


// 请求 Pipeline 详情并渲染 DAG
const fetchAndRenderPipeline = async () => {
  try {
    const res = await request.get(`/pipelines/${pipelineId.value}`);
    console.log('获取 Pipeline 详情', res);
    const pipeline = res.data; // PipelineResponse 对象
    const dagData =JSON.parse(pipeline.dagJson); // 假设 dagJson 是字符串，需要 parse
    console.log('解析后的 DAG 数据', dagData);  
    if (graph) {
      graph.setData(dagData); // 直接设置 { nodes: [...], edges: [...] }
      await graph.render();
      graph.fitView();
    }
  } catch (error) {
    console.error('获取 Pipeline 失败', error);
  }
};

onMounted(() => {
  const container = document.getElementById('graphContainer');
  if (!container) return;

  graph = new Graph({
    container,
    width: container.clientWidth,
    height: 500,
    autoFit: 'center',
    animation: false,
    plugins: [{ type: 'grid-line', key: 'my-grid-line', size: 40, stroke: '#0001', follow: true }],
    node: {
      type: 'dag-task',
      style: {
        size: [130, 52],
        radius: 6,
        fill: '#DBEAFE',
        stroke: '#3B82F6',
        draggable: true,
        ports: [
          { key: 'left', placement: [0, 0.5] },
          { key: 'right', placement: [1, 0.5] },
        ],
      },
      state: { active: { fill: '#338833', stroke: 'transparent' } },
    },
    edge: { type: 'cubic-horizontal', style: { labelBackground: true, endArrow: true } },
    zoomRange: [0.5, 3],
    behaviors: ['zoom-canvas', 'drag-canvas', 'drag-element', 'click-select', {
      type: 'click-select', degree: 2, state: 'active', neighborState: 'neighborActive', unselectedState: 'inactive'
    }],
    layout: { type: 'dagre', rankdir: 'LR', nodesep: 40, ranksep: 80, animate: false },
  });

  graph.on('node:click', (evt) => {
    const nodeId = evt.target.id;
    if (nodeId) selectedTaskId.value = nodeId;
  });

  // 初始渲染
  //if (props.graphData?.data) renderGraph(props.graphData.data);

  // 1. 获取 Pipeline 数据并渲染
  fetchAndRenderPipeline();

  // 建立 SSE 连接
  connectSSE();
});

onUnmounted(() => {
  if (eventSource) eventSource.close();
  if (graph) { graph.destroy(); graph = null; }
});
</script>

<!-- <style scoped>
.dag-page {
  display: flex;
  width: 100%;
  height: 100%;
}
.dag-left {
  flex: 3;
  height: 600px;
  border-right: 1px solid #eee;
}
.dag-right {
  flex: 1;
  padding: 16px;
  overflow-y: auto;
}
</style> -->