<template>
  <div class="task-list">
    <!-- 顶部筛选栏（保持不变） -->
    <div class="filter-bar">
      <n-space align="center" wrap>
        <n-input
          v-model:value="searchKeyword"
          placeholder="搜索任务名称"
          clearable
          style="width: 200px"
          @clear="handleSearch"
          @keyup.enter="handleSearch"
        />
        <n-select
          v-model:value="selectedStatus"
          :options="statusOptions"
          placeholder="状态筛选"
          clearable
          style="width: 140px"
          @update:value="handleSearch"
        />
        <n-button type="primary" @click="handleSearch">查询</n-button>
      </n-space>
    </div>

    <!-- 表格：添加行点击事件和自定义类名 -->
    <n-data-table
      :columns="columns"
      :data="tableData"
      :loading="loading"
      :pagination="pagination"
      remote
      :row-class-name="getRowClassName"
      :row-props="rowProps"
    />
  </div>
</template>

<script setup>
import { ref, computed, h, onMounted ,reactive} from 'vue';
import { NTag, NIcon, NButton } from 'naive-ui';
import { useRouter } from 'vue-router';
import { SyncCircle, PauseCircle } from '@vicons/ionicons5';
const router = useRouter();



// ---- 状态映射（不变） ----
const statusMap = {
  pending: { label: '等待中', color: 'default' },
  running: { label: '运行中', color: 'info' },
  completed: { label: '已完成', color: 'success' },
  failed: { label: '失败', color: 'error' },
};

const statusOptions = Object.entries(statusMap).map(([value, { label }]) => ({
  label,
  value,
}));

const clickCounts = reactive({}); 
const formatTime = (timestamp) => {
  if (!timestamp) return '-';
  const date = new Date(timestamp);
  const pad = (n) => String(n).padStart(2, '0');
  return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
};
// ---- 表格列定义 ----
const columns = [
  { title: '任务ID', key: 'id', width: 120 },
  { title: '任务名称', key: 'name', ellipsis: { tooltip: true } },
  {
    title: '状态',
    key: 'status',
    width: 100,
    render(row) {
      return h(NTag, { type: statusMap[row.status]?.color || 'default', size: 'small' }, () => statusMap[row.status]?.label || row.status);
    },
  },
  { title: '进度', key: 'progress', width: 100, render: (row) => `${row.progress}%` },
  { title: '开始时间', key: 'startTime', width: 160, render: (row) => formatTime(row.startTime) },
  { title: '结束时间', key: 'endTime', width: 160, render: (row) => formatTime(row.endTime) },
  {
    title: '操作',
    key: 'actions',
    width: 80,
    render(row) {
        const isFinished = row.status === 'completed' || row.status === 'failed';
        const count = clickCounts[row.id] || 0;
        const hasClicked = count > 0;
        const iconStyle = isFinished && hasClicked
        ? { animation: 'spin-once 0.6s ease-in-out' }
        : {};

        const handleClick = (e) => {
        e.stopPropagation();
        if (isFinished) {
            handleRestart(row.id);
        } else {
            handlePause(row.id);
        }
        };

        const iconComponent = isFinished ? SyncCircle : PauseCircle; // 需要导入 PauseCircle

        return h(
        NButton,
        {
            size: 'small',
            quaternary: true,
            onClick: handleClick,
        },
        () =>
            h(
            'span',
            {
                key: row.id + '_' + count,
                style: { display: 'inline-flex' },
            },
            [h(NIcon, { component: iconComponent, size: 18, style: iconStyle })]
            )
        );
    },
    },
];

// 旋转动画注入到全局（因为 scoped 下需深度或全局）
// 可直接在 <style> 中不加 scoped 或使用 :global
// 这里我们在组件中添加一个全局样式

// ---- 数据与分页（不变） ----
const loading = ref(false);
const tableData = ref([]);
const totalCount = ref(0);
const currentPage = ref(1);
const pageSize = ref(15);
const searchKeyword = ref('');
const selectedStatus = ref(null);

const pagination = computed(() => ({
  page: currentPage.value,
  pageSize: pageSize.value,
  itemCount: totalCount.value,
  showSizePicker: true,
  pageSizes: [15, 20, 50],
  onUpdatePage: (page) => { currentPage.value = page; fetchTasks(); },
  onUpdatePageSize: (pageSize) => { pageSize.value = pageSize; currentPage.value = 1; fetchTasks(); },
}));

// ---- 行点击进入详情 ----
const rowProps = (row) => {
  return {
    style: 'cursor: pointer;',
    onClick: () => {
      router.push(`/pipeline/${row.id}`);
    },
  };
};

// ---- 斑马纹 ----
const getRowClassName = (row, index) => {
  return index % 2 === 0 ? 'row-even' : 'row-odd';
};

// ---- 获取任务列表（模拟） ----
const fetchTasks = async () => {
  loading.value = true;
  // 模拟...
  await new Promise((resolve) => setTimeout(resolve, 400));
  const mockData = [];
  const statuses = ['pending', 'running', 'completed', 'failed'];
  for (let i = 0; i < pageSize.value; i++) {
    const idx = (currentPage.value - 1) * pageSize.value + i + 1;
    const baseTime = Date.now() - 86400000 * idx; // 模拟不同时间
    mockData.push({
      id: `${String(idx).padStart(4, '0')}`,
      name: `打包任务 ${idx}`,
      status: statuses[idx % statuses.length],
      progress: Math.floor(Math.random() * 101),
      startTime: baseTime,
      endTime: idx % 3 === 0 ? baseTime + 3600000 : null, // 有些可能尚未结束
    });
  }
  totalCount.value = 46;
  tableData.value = mockData;
  loading.value = false;
};

defineExpose({ fetchTasks });

// 查询/筛选
const handleSearch = () => {
  currentPage.value = 1;
  fetchTasks();
};


const handlePause = (taskId) => {
  // TODO: 调用 API 暂停任务
  console.log('暂停任务:', taskId);
  // 可以在此更新任务状态（若需立即反馈）
};

const handleRestart = (taskId) => {
  clickCounts[taskId] = (clickCounts[taskId] || 0) + 1; // 触发旋转动画
  // TODO: 调用 API 重新启动任务
  console.log('重新启动任务:', taskId);
};


// const handleReapply = (taskId) => {
//   // 增加计数，触发 key 变化 → 图标元素重新创建 → 动画播放一次
//   clickCounts[taskId] = (clickCounts[taskId] || 0) + 1;
//   console.log('重新申请任务:', taskId);
// };
onMounted(fetchTasks);
</script>

<style scoped>
.task-list {
  padding: 16px;
}
.filter-bar {
  margin-bottom: 16px;
}

/* 斑马纹 */
:deep(.row-even td) { background-color: #ffffff !important; }
:deep(.row-odd td) { background-color: #f5f5f5 !important; }

/* 表头加深 */
:deep(.n-data-table th) {
  background-color: #e0e0e0 !important;
  color: #333;
  font-weight: 600;
}

/* 行高紧凑 */
:deep(.n-data-table td) {
  padding-top: 4px !important;
  padding-bottom: 4px !important;
  font-size: 13px;
}
:deep(.n-data-table th) {
  padding-top: 4px !important;
  padding-bottom: 4px !important;
}

:deep(.n-data-table tr:hover td) {
  background-color: #e6f7ff !important; /* 浅蓝色，或自行调整 */
}
</style>

<!-- 全局旋转动画（不要 scoped） -->
<style>
@keyframes spin-once {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>