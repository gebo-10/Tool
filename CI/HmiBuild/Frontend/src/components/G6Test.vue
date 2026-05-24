<template>
  <div id="container"></div>
</template>

<script setup>
  import { onMounted } from 'vue';
  import { Rect, register, Graph, ExtensionCategory  } from '@antv/g6';



  class DagTaskNode extends Rect {


  // 绘制外框和主要图形（背景、标题、进度条等）
  drawExtShape(attributes, container) {
    // 从 attributes 中获取业务数据
    const { label = '', status = 'pending', progress = 0 } = attributes;
    const size = this.getSize(); // 返回 [width, height]
    const [width, height] = size;

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
    // const keyShape = this.upsert('key', 'rect', {
    //   width,
    //   height,
    //   radius: 8,
    //   fill: colors.bg,
    //   stroke: '#E5E7EB',
    //   lineWidth: 1,
    //   shadowBlur: 10,
    //   shadowColor: 'rgba(0,0,0,0.05)',
    //   shadowOffsetY: 2,
    // }, container);

    const startx= -width/2;
    const starty= -height/2;
    //---------- 任务名称 ----------
    this.upsert('status-name', 'text', {
      x: startx+10,
      y: 0 ,
      text: label,
      textAlign: 'left',
      textBaseline: 'middle',
      fontSize: 14,
      fontWeight: 600,
      fill: '#8F4997',
      fontFamily: 'system-ui, sans-serif',
    }, container);

    // // ---------- 状态标签 ----------
    this.upsert('status-text', 'text', {
      x: width/2 - 10,
      y: 0,
      text: statusLabel,
      fontSize: 12,
      fill: colors.text,
      textAlign: 'right',
      textBaseline: 'middle',
    }, container);

    // ---------- 进度条背景 ----------
    const barY = height/2 - 10;
    const barX = -width/2+10;
    this.upsert('progress-bg', 'rect', {
      x: barX,
      y: barY,
      width: width - 20,
      height: 4,
      radius: 2,
      fill: '#F3F4F6',
      stroke: '#E5E7EB',
    }, container);

    // ---------- 进度条填充 ----------
    this.upsert('progress-bar', 'rect', {
      x: barX,
      y: barY,
      width: (width - 20) * (progress / 100),
      height: 4,
      radius: 2,
      fill: colors.prog,
    }, container);

    //return keyShape;
  }


getButtonStyle(attributes) {
    return {
      x: 40,
      y: -10,
      width: 20,
      height: 20,
      radius: 10,
      fill: '#1890ff',
      cursor: 'pointer', // 鼠标指针变为手型
    };
  }

  drawButtonShape(attributes, container) {
    const btnStyle = this.getButtonStyle(attributes, container);
    const btn = this.upsert('button', 'rect', btnStyle, container);

    // 为按钮添加点击事件
    if (!btn.__clickBound) {
      btn.addEventListener('click', (e) => {
        // 阻止事件冒泡，避免触发节点的点击事件
        e.stopPropagation();

        // 执行业务逻辑
        console.log('Button clicked on node:', this.id);

        // 如果数据中有回调函数，则调用
        if (typeof attributes.onButtonClick === 'function') {
          attributes.onButtonClick(this.id, this.data);
        }
      });
      btn.__clickBound = true; // 标记已绑定事件，避免重复绑定
    }
  }




  // // 数据更新时，G6 会调用此方法重新绘制图形
  // processStyle(style, attributes) {
  //   // 这里可以不做处理，G6 v5 默认会重新调用 drawKeyShape
  //   // 如果想增量更新，可以在这里用 this.upsert 修改子图形属性
  //   return style;
  // }

  render(attributes = this.parsedAttributes, container) {
    // 1. 渲染基础矩形和主标题
    super.render(attributes, container);

    // 2. 添加副标题
    this.drawExtShape(attributes, container);
    //this.drawButtonShape(attributes, container);
  }
}

// 注册到 G6
register(ExtensionCategory.NODE, 'dag-task', DagTaskNode);



  onMounted(() => {
    const graph = new Graph({
      container: document.getElementById('container'),
      width: container.clientWidth,
      height: 500,
      autoFit: 'center',
      node: {
          // 全局默认节点类型设为矩形
          type: 'dag-task',
          style: {
            //size: [50, 20],  // 宽度 100，高度 40
            size: [130, 52],
            radius: 6,        // 圆角（可选）
            fill: '#DBEAFE',
            stroke: '#3B82F6',
            draggable: true,   // 节点可拖拽
            //labelText: (d) => d.data?.label || d.id,   // 动态文字
            ports: [
              { key: 'left', placement: [0, 0.5] },  // 左边缘中心
              { key: 'right', placement: [1, 0.5] }, // 右边缘中心
              // ...
            ],
          },
          state: {
            active: {
              fill: '#338833', // 选中时的填充色
              stroke: 'transparent', // 去掉边框
            }
          },
      },
      edge: {
        type: 'cubic-horizontal',
        style: {
          //labelText: (d) => d.id,
          labelBackground: true,
          endArrow: true,
        },
      },
      behaviors: [
        'drag-canvas',  // 画布拖拽
        //'zoom-canvas',  // 画布缩放
        'drag-element',    // 节点拖拽
        'click-select',
        {
          type: 'click-select',
          key: 'click-select-1',
          degree: 2, // 选中扩散范围
          state: 'active', // 选中的状态
          neighborState: 'neighborActive', // 相邻节点附着状态
          unselectedState: 'inactive', // 未选中节点状态
        },
      ],
      // 关键：配置布局
      layout: {
        type: 'dagre',          // 使用 dagre 布局
        rankdir: 'LR',          // 方向：LR（左→右）、TB（上→下）
        nodesep: 40,            // 同层节点水平间距
        ranksep: 80,            // 层级之间的间距
      },
      data: {
        nodes: [
          {
            id: 'node-0',
            style: {
              label: '开始',
              status: 'completed',
              progress: 100,
            },
          },
          {
            id: 'node-1',
            style: {
              label: '任务1',
              status: 'pending',
              progress: 10,
            },
          },
          {
            id: 'node-2',
            style: {
              label: '任务2',
              status: 'running',
              progress: 10,
            },
          },
          {
            id: 'node-3',
            style: {

              label: '任务三',
              status: 'running',
              progress: 10,
            },
          }
        ],
        edges: [
          {id: 'edge-01', source: 'node-0', target: 'node-1' ,sourcePort: 'right', targetPort: 'left'},
          {id: 'edge-03', source: 'node-0', target: 'node-3' ,sourcePort: 'right', targetPort: 'left'},
          { id: 'edge-1', source: 'node-1', target: 'node-2' ,sourcePort: 'right', targetPort: 'left'},
          { id: 'edge-2', source: 'node-3', target: 'node-2' ,sourcePort: 'right', targetPort: 'left'}
        ],
      },
    });


graph.on('node:click', (evt) => {
  const nodeId = evt.target.id;

    console.log('Node clicked:', nodeId);
  
    // 示例：点击节点后更新其状态和进度
    // const item = evt.item;
    // const model = item.getModel();
    // const newStatus = model.style.status === 'running' ? 'completed' : 'running';
    // const newProgress = model.style.progress >= 100 ? 0 : model.style.progress + 10;
  
    // graph.updateItem(item, {
    //   style: {
    //     ...model.style,
    //     status: newStatus,
    //     progress: newProgress,
    //   },
    // });
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