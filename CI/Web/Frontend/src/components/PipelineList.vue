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
import { ref, computed, h, onMounted, reactive } from 'vue';
import { NTag, NIcon, NButton } from 'naive-ui';
import { useRouter } from 'vue-router';
import { SyncCircle, PauseCircle } from '@vicons/ionicons5';
import request from '../utils/request'; // 请根据实际路径调整

const router = useRouter();

// ---- 状态映射（与后端返回的状态值匹配）----
const statusMap = {
  Pending: { label: '等待中', color: 'default' },
  Running: { label: '运行中', color: 'info' },
  Completed: { label: '已完成', color: 'success' },
  Failed: { label: '失败', color: 'error' },
  Cancelled: { label: '已取消', color: 'warning' }
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

// ---- 表格列定义（注意字段映射）----
const columns = [
  { title: '任务ID', key: 'id', width: 120 },
  { title: '提交人', key: 'creator',  width: 120},
  { title: '任务名称', key: 'name', width: 120},
  { title: '参数', key: 'params', ellipsis: { tooltip: true } },
  {
    title: '状态',
    key: 'status',
    width: 100,
    render(row) {
      const statusInfo = statusMap[row.status] || { label: row.status, color: 'default' };
      return h(NTag, { type: statusInfo.color, size: 'small' }, () => statusInfo.label);
    },
  },
  { title: '进度', key: 'progress', width: 100, render: (row) => `${row.progress ?? 0}%` },
  { title: '开始时间', key: 'startTime', width: 160, render: (row) => formatTime(row.startTime) },
  { title: '结束时间', key: 'endTime', width: 160, render: (row) => formatTime(row.endTime) },
  {
    title: '操作',
    key: 'actions',
    width: 80,
    render(row) {
      const isFinished = row.status === 'Completed' || row.status === 'Failed' || row.status === 'Cancelled';
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

      const iconComponent = isFinished ? SyncCircle : PauseCircle;
      return h(
        NButton,
        { size: 'small', quaternary: true, onClick: handleClick },
        () => h('span', { style: { display: 'inline-flex' } }, [h(NIcon, { component: iconComponent, size: 18, style: iconStyle })])
      );
    },
  },
];

// ---- 数据与分页 ----
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
  onUpdatePageSize: (size) => { pageSize.value = size; currentPage.value = 1; fetchTasks(); },
}));

// ---- 行点击进入详情 ----
const rowProps = (row) => ({
  style: 'cursor: pointer;',
  onClick: () => router.push(`/pipelines/${row.id}`),
});

// ---- 斑马纹 ----
const getRowClassName = (row, index) => (index % 2 === 0 ? 'row-even' : 'row-odd');

// ---- 获取任务列表（真实 API）----
const fetchTasks = async () => {
  loading.value = true;
  try {
    // 构建查询参数
    const params = {
      page: currentPage.value,
      size: pageSize.value,
    };
    if (searchKeyword.value) params.search = searchKeyword.value;
    // 注意：后端当前 GET /api/pipelines 只支持 search，不支持 status 筛选
    // 如果需要按状态筛选，需扩展后端，此处暂不实现

    const response = await request.get('/pipelines', { params });
    // 后端返回格式：{ pageIndex, pageSize, totalCount, items }
    const data = response.data;
    totalCount.value = data.totalCount;
    // 将 items 映射为前端需要的字段
    tableData.value = data.items.map(item => ({
      id: item.id,
      name: item.name,
      creator: item.creator,
      status: item.status,          // "Pending", "Running", 等
      progress: item.progress ?? 0, // 如果后端未提供，暂时为 0
      startTime: item.createdAt,    // 使用创建时间作为开始时间
      endTime: item.completedAt,    // 完成时间
    }));
  } catch (error) {
    console.error('获取任务列表失败', error);
  } finally {
    loading.value = false;
  }
};

// 查询/筛选
const handleSearch = () => {
  currentPage.value = 1;
  fetchTasks();
};

// 暂停任务（需后端支持）
const handlePause = (taskId) => {
  console.log('暂停任务:', taskId);
  // TODO: await request.put(`/api/pipelines/${taskId}/cancel`);
};

// 重启任务（需后端支持）
const handleRestart = (taskId) => {
  clickCounts[taskId] = (clickCounts[taskId] || 0) + 1;
  console.log('重新启动任务:', taskId);
  // TODO: await request.post(`/api/pipelines/${taskId}/restart`);
};

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