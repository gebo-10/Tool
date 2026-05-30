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
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { Rect, register, Graph, ExtensionCategory } from '@antv/g6';
import { NEmpty } from 'naive-ui';
import TaskDetail from './TaskDetail.vue';

const props = defineProps({
  graphData: {
    type: Object,
    default: () => ({ data: { nodes: [], edges: [] } }),
  },
});

let graph = null; // G6 图实例
const selectedTaskId = ref(null);

// ---------- 自定义节点类 ----------
class DagTaskNode extends Rect {
  render(attributes = this.parsedAttributes, container) {
    // 1. 调用父类绘制基础矩形（确保 keyShape 存在）
    super.render(attributes, container);

    // 2. 获取业务数据
    const { label = '', status = 'pending', progress = 0 } = attributes;
    const [width, height] = this.getSize(attributes);
    const startX = -width / 2;
    const startY = -height / 2;

    // 状态配置
    const statusColors = {
      pending:  { bg: '#E5E7EB', text: '#6B7280', prog: '#9CA3AF' },
      running:  { bg: '#DBEAFE', text: '#1D4ED8', prog: '#3B82F6' },
      completed: { bg: '#D1FAE5', text: '#047857', prog: '#10B981' },
      failed:   { bg: '#FEE2E2', text: '#B91C1C', prog: '#EF4444' },
      cancelled: { bg: '#F3F4F6', text: '#e2b00d', prog: '#9CA3AF' },
    };
    const colors = statusColors[status] || statusColors.pending;
    const statusLabel = {
      pending: '等待中',
      running: '运行中',
      completed: '已完成',
      failed: '失败',
      cancelled: '已取消',
    }[status] || status;

    // 任务名称
    this.upsert('status-name', 'text', {
      x: startX + 10,
      y: 0,
      text: label,
      textAlign: 'left',
      textBaseline: 'middle',
      fontSize: 14,
      fontWeight: 600,
      fill: '#8F4997',
      fontFamily: 'system-ui, sans-serif',
    }, container);

    // 状态标签
    this.upsert('status-text', 'text', {
      x: width / 2 - 10,
      y: 0,
      text: statusLabel,
      fontSize: 12,
      fill: colors.text,
      textAlign: 'right',
      textBaseline: 'middle',
    }, container);

    // 进度条背景
    const barY = height / 2 - 10;
    const barX = startX + 10;
    this.upsert('progress-bg', 'rect', {
      x: barX,
      y: barY,
      width: width - 20,
      height: 4,
      radius: 2,
      fill: '#F3F4F6',
      stroke: '#E5E7EB',
    }, container);

    // 进度条填充
    this.upsert('progress-bar', 'rect', {
      x: barX,
      y: barY,
      width: (width - 20) * (progress / 100),
      height: 4,
      radius: 2,
      fill: colors.prog,
    }, container);
  }
}

// 注册自定义节点（仅一次）
let registered = false;
if (!registered) {
  register(ExtensionCategory.NODE, 'dag-task', DagTaskNode);
  registered = true;
}

// ---------- 辅助函数：渲染或更新图 ----------
const renderGraph = (data) => {
  if (!graph) return;
  // G6 v5 更新数据方法
  graph.setData(data);
  graph.render().then(() => {
    graph.fitView(); // 自动适应视图
  });
};

// ---------- 监听 props 变化 ----------
watch(
  () => props.graphData,
  (newData) => {
    if (newData && newData.data && graph) {
      //renderGraph(newData.data);
      graph.setData(props.graphData.data);
      graph.render().then(() => {
        graph.fitView();
      });
    }
  },
  { deep: true, immediate: true }
);

// ---------- 生命周期 ----------
onMounted(() => {
  const container = document.getElementById('graphContainer');
  if (!container) return;

  graph = new Graph({
    container,
    width: container.clientWidth,
    height: 500,
    autoFit: 'center',
    animation: false,
    plugins: [
      {
        type: 'grid-line',
        key: 'my-grid-line', // 指定唯一标识符，便于后续动态更新
        size: 40,
        stroke: '#0001',
        follow: true,
      },
    ],
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
      state: {
        active: { fill: '#338833', stroke: 'transparent' },
      },
    },
    edge: {
      type: 'cubic-horizontal',
      style: {
        labelBackground: true,
        endArrow: true,
      },
    },
    //background: '#ffffff',
    zoomRange: [0.5, 3],
    behaviors: [
      'zoom-canvas',
      'drag-canvas',
      'drag-element',
      'click-select',
      {
        type: 'click-select',
        degree: 2,
        state: 'active',
        neighborState: 'neighborActive',
        unselectedState: 'inactive',
      },
    ],
    layout: {
      type: 'dagre',
      rankdir: 'LR',
      nodesep: 40,
      ranksep: 80,
      animate: false,
    },
  });

  // 绑定节点点击事件
  graph.on('node:click', (evt) => {
    const nodeId = evt.target.id;
    if (nodeId) {
      selectedTaskId.value = nodeId;
    }
  });

  // 初始渲染（如果父组件已传入数据）
  if (props.graphData?.data) {
    renderGraph(props.graphData.data);
    // graph.setData(props.graphData.data);
    // graph.render().then(() => {
    //   graph.fitView();
    // });
  }
});

onUnmounted(() => {
  if (graph) {
    graph.destroy();
    graph = null;
  }
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