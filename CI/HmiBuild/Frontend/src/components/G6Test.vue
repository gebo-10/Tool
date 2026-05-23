<template>
  <div id="container"></div>
</template>

<script setup>
  import { onMounted } from 'vue';
  import { Rect, register, ExtensionCategory, Graph } from '@antv/g6';



  class DagTaskNode extends Rect {
  // 绘制外框和主要图形（背景、标题、进度条等）
  drawKeyShape(attributes, container) {
    // 从 attributes 中获取业务数据
    const { label = '', status = 'pending', progress = 0 } = attributes;
    const width = 180;
    const height = 72;

    // 状态色盘
    const statusColors = {
      pending:  { bg: '#E5E7EB', text: '#6B7280', prog: '#9CA3AF' },
      running:  { bg: '#DBEAFE', text: '#1D4ED8', prog: '#3B82F6' },
      completed:{ bg: '#D1FAE5', text: '#047857', prog: '#10B981' },
      failed:   { bg: '#FEE2E2', text: '#B91C1C', prog: '#EF4444' },
    };
    const colors = statusColors[status] || statusColors.pending;
    const statusLabel = { pending: '等待中', running: '运行中', completed: '已完成', failed: '失败' }[status] || status;

    // ---------- 绘制背景矩形 ----------
    const keyShape = this.upsert('key', 'rect', {
      width,
      height,
      radius: 8,
      fill: colors.bg,
      stroke: '#E5E7EB',
      lineWidth: 1,
      shadowBlur: 10,
      shadowColor: 'rgba(0,0,0,0.05)',
      shadowOffsetY: 2,
    }, container);

    // ---------- 任务名称 ----------
    this.upsert('label', 'text', {
      x: 14,
      y: 14,
      text: label,
      fontSize: 14,
      fontWeight: 600,
      fill: '#1F2937',
      fontFamily: 'system-ui, sans-serif',
    }, container);

    // ---------- 状态标签 ----------
    this.upsert('status-text', 'text', {
      x: 14,
      y: 36,
      text: statusLabel,
      fontSize: 12,
      fill: colors.text,
    }, container);

    // ---------- 进度条背景 ----------
    const barY = height - 10;
    this.upsert('progress-bg', 'rect', {
      x: 10,
      y: barY,
      width: width - 20,
      height: 4,
      radius: 2,
      fill: '#F3F4F6',
      stroke: '#E5E7EB',
    }, container);

    // ---------- 进度条填充 ----------
    this.upsert('progress-bar', 'rect', {
      x: 10,
      y: barY,
      width: (width - 20) * (progress / 100),
      height: 4,
      radius: 2,
      fill: colors.prog,
    }, container);

    return keyShape;
  }

  // 数据更新时，G6 会调用此方法重新绘制图形
  processStyle(style, attributes) {
    // 这里可以不做处理，G6 v5 默认会重新调用 drawKeyShape
    // 如果想增量更新，可以在这里用 this.upsert 修改子图形属性
    return style;
  }
}

// 注册到 G6
register(ExtensionCategory.NODE, 'dag-task', DagTaskNode);



  onMounted(() => {
    const graph = new Graph({
      container: document.getElementById('container'),
      width: 500,
      height: 500,
      node: {
          // 全局默认节点类型设为矩形
          type: 'dag-task',
          style: {
            size: [50, 20],  // 宽度 100，高度 40
            radius: 6,        // 圆角（可选）
            fill: '#DBEAFE',
            stroke: '#3B82F6',
            draggable: true,   // 节点可拖拽
            labelText: (d) => d.data?.label || d.id,   // 动态文字
            ports: [
              { key: 'left', placement: [0, 0.5] },  // 左边缘中心
              { key: 'right', placement: [1, 0.5] }, // 右边缘中心
              // ...
            ],
          },
      },
      edge: {
    type: 'cubic-horizontal',
    style: {
      labelText: (d) => d.id,
      labelBackground: true,
      endArrow: true,
    },
  },
      behaviors: [
        'drag-canvas',  // 画布拖拽
        'zoom-canvas',  // 画布缩放
        'drag-element',    // 节点拖拽
      ],
      data: {
        nodes: [
          {
            id: 'node-1',
            style: { x: 50, y: 100 },
          },
          {
            id: 'node-2',
            style: { x: 150, y: 100 },
          },
        ],
        edges: [{ id: 'edge-1', source: 'node-1', target: 'node-2' ,sourcePort: 'right', targetPort: 'left'}],
      },
    });

    graph.render();



// fetch('https://assets.antv.antgroup.com/g6/graph.json')
//     .then((res) => res.json())
//     .then((data) => {
//       const graph = new Graph({
//         container: 'container',
//         autoFit: 'view',
//         data,
//         node: {
//           style: {
//             size: 10,
//           },
//           palette: {
//             field: 'group',
//             color: 'tableau',
//           },
//         },
//         layout: {
//           type: 'd3-force',
//           manyBody: {},
//           x: {},
//           y: {},
//         },
//         behaviors: ['drag-canvas', 'zoom-canvas', 'drag-element'],
//       });

//       graph.render();
//     });
  



// const graph = new Graph({
//   container: 'container',
//   autoFit: 'view',
//   data: {
//     nodes: [{ id: 'node1' }, { id: 'node2' }],
//     edges: [{ source: 'node1', target: 'node2' }],
//   },
//   node: {
//     style: {
//       size: 10,
//     },
//     palette: {
//       field: 'group',
//       color: 'tableau',
//     },
//   },
//   layout: {
//     type: 'd3-force',
//     manyBody: {},
//     x: {},
//     y: {},
//   },
//   behaviors: ['drag-canvas', 'zoom-canvas', 'drag-element'],
// });

// graph.render();



  });
</script>