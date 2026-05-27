<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue';
import G6 from '@antv/g6';

const props = defineProps({
  graphData: { type: Object, required: true }
});

const graphContainer = ref(null);
let graph = null;

// 状态色盘（柔和现代风）
const STATUS_COLORS = {
  pending:  { bg: '#E5E7EB', text: '#6B7280', progress: '#9CA3AF' },
  running:  { bg: '#DBEAFE', text: '#1D4ED8', progress: '#3B82F6' },
  completed:{ bg: '#D1FAE5', text: '#047857', progress: '#10B981' },
  failed:   { bg: '#FEE2E2', text: '#B91C1C', progress: '#EF4444' },
};

const STATUS_LABEL = {
  pending: '等待中',
  running: '运行中',
  completed: '已完成',
  failed: '失败',
};

onMounted(() => {
  // 注册美化后的自定义节点
  G6.registerNode('dag-task', {
    draw(cfg, group) {
      const { label, status = 'pending', progress = 0 } = cfg;
      const width = 180;
      const height = 72;
      const radius = 10;

      const colors = STATUS_COLORS[status] || STATUS_COLORS.pending;

      // 阴影卡片
      const keyShape = group.addShape('rect', {
        attrs: {
          x: -width / 2,
          y: -height / 2,
          width,
          height,
          radius,
          fill: colors.bg,
          stroke: '#E5E7EB',
          lineWidth: 1,
          shadowColor: 'rgba(0,0,0,0.06)',
          shadowBlur: 10,
          shadowOffsetX: 0,
          shadowOffsetY: 2,
          cursor: 'pointer',
        },
        name: 'main-box',
      });

      // 标题
      group.addShape('text', {
        attrs: {
          x: -width / 2 + 14,
          y: -height / 2 + 22,
          text: label,
          fontSize: 14,
          fontWeight: 600,
          fill: '#1F2937',
          fontFamily: 'system-ui, -apple-system, sans-serif',
        },
        name: 'label-text',
      });

      // 状态文字
      group.addShape('text', {
        attrs: {
          x: -width / 2 + 14,
          y: -height / 2 + 44,
          text: STATUS_LABEL[status] || status,
          fontSize: 12,
          fill: colors.text,
          fontFamily: 'system-ui, -apple-system, sans-serif',
        },
        name: 'status-text',
      });

      // 进度条背景
      const barY = height / 2 - 10;
      group.addShape('rect', {
        attrs: {
          x: -width / 2 + 10,
          y: barY,
          width: width - 20,
          height: 4,
          radius: 2,
          fill: '#F3F4F6',
          stroke: '#E5E7EB',
          lineWidth: 0.5,
        },
        name: 'progress-bg',
      });

      // 进度条填充
      group.addShape('rect', {
        attrs: {
          x: -width / 2 + 10,
          y: barY,
          width: (width - 20) * (progress / 100),
          height: 4,
          radius: 2,
          fill: colors.progress,
        },
        name: 'progress-bar',
      });

      return keyShape;
    },
    update(cfg, item) {
      const group = item.getContainer();
      const { status = 'pending', progress = 0 } = cfg;
      const width = 180;
      const colors = STATUS_COLORS[status] || STATUS_COLORS.pending;

      // 更新背景颜色
      group.find(e => e.get('name') === 'main-box')?.attr('fill', colors.bg);
      // 更新状态文字
      const statusText = group.find(e => e.get('name') === 'status-text');
      if (statusText) {
        statusText.attr({
          text: STATUS_LABEL[status] || status,
          fill: colors.text,
        });
      }
      // 更新进度条
      const progBar = group.find(e => e.get('name') === 'progress-bar');
      if (progBar) {
        progBar.attr({
          width: (width - 20) * (progress / 100),
          fill: colors.progress,
        });
      }
    },
  }, 'single-node');

  // 初始化 Graph
  graph = new G6.Graph({
    container: graphContainer.value,
    width: graphContainer.value.clientWidth,
    height: graphContainer.value.clientHeight || 500,
    fitView: true,
    fitViewPadding: 30,
    layout: {
      type: 'dagre',
      rankdir: 'LR',
      nodesep: 40,
      ranksep: 100,
    },
    defaultNode: {
      type: 'dag-task',
    },
    defaultEdge: {
      type: 'polyline',
      style: {
        stroke: '#9CA3AF',
        lineWidth: 1.5,
        endArrow: {
          path: G6.Arrow.triangle(6, 8, 0),
          fill: '#9CA3AF',
        },
        radius: 8,
      },
    },
    modes: {
      default: ['drag-canvas', 'zoom-canvas', 'drag-node'],
    },
    animate: true,
  });

  graph.data(props.graphData);
  graph.render();

  // 窗口大小变化自适应
  const resizeFn = () => {
    if (graph && graphContainer.value) {
      graph.changeSize(
        graphContainer.value.clientWidth,
        graphContainer.value.clientHeight || 500
      );
    }
  };
  window.addEventListener('resize', resizeFn);
});

onUnmounted(() => {
  if (graph) graph.destroy();
  window.removeEventListener('resize', () => {});
});

watch(() => props.graphData, (newData) => {
  if (graph) {
    graph.changeData(newData);
  }
}, { deep: true });
</script>

<template>
  <div ref="graphContainer" class="dag-container"></div>
</template>

<style scoped>
.dag-container {
  width: 100%;
  height: 500px;
  background: #F9FAFB;
  border-radius: 8px;
  border: 1px solid #E5E7EB;
}
</style>