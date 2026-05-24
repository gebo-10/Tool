<template>
  <div id="container2"></div>
</template>

<script setup>
    import { Rect, register, Graph, ExtensionCategory } from '@antv/g6';
    import { VueNode } from 'g6-extension-vue';
    import { h,onMounted } from 'vue';

    import MyVueNode from './MyVueNode.vue';
    register(ExtensionCategory.NODE, 'vue-node', VueNode);


//const container = ref(null);

onMounted(() => {




const graph2 = new Graph({
      container: document.getElementById('container2'),
      width: container.clientWidth,
      height: 500,
      autoFit: 'center',
      node: {
          // 全局默认节点类型设为矩形
          type: 'vue-node',
          style: {
            //size: [50, 20],  // 宽度 100，高度 40
            //size: [180, 72],
            component: () => h(MyVueNode),
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
            id: 'node-1',
            //style: { x: 50, y: 100 },
          },
          {
            id: 'node-2',
            //style: { x: 150, y: 100 },
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
          { id: 'edge-1', source: 'node-1', target: 'node-2' ,sourcePort: 'right', targetPort: 'left'},
          { id: 'edge-2', source: 'node-3', target: 'node-2' ,sourcePort: 'right', targetPort: 'left'}
        ],
      },
    });

    graph2.render();





});
</script>

<style scoped>
.g6-vue-node {
  display: inline-flex;
  align-items: center;
  padding: 10px 14px;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  background-color: #ffffff;
  color: #303133;
}
.g6-vue-node.is-active {
  border-color: #409eff;
  background-color: #ecf5ff;
}
.node-icon {
  display: inline-flex;
  align-items: center;
  margin-right: 8px;
}
.node-content {
  display: inline-flex;
  flex-direction: column;
}
</style>
