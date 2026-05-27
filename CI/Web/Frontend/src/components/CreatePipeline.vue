<template>
    <n-modal v-model:show="show" preset="card" title="创建新流水线" style="width: 600px">
        <n-form ref="formRef" :model="form" :rules="rules" label-placement="left" label-width="100">
        <n-form-item label="流水线名称" path="name">
            <n-input v-model:value="form.name" placeholder="例如：V1.2 正式发布" />
        </n-form-item>
        <n-form-item label="描述" path="description">
            <n-input v-model:value="form.description" type="textarea" placeholder="可选描述" />
        </n-form-item>
        <n-form-item label="子任务模板" path="template">
            <n-select
            v-model:value="form.template"
            :options="templateOptions"
            placeholder="选择预置模板"
            />
        </n-form-item>
        <!-- 可以继续添加更多参数 -->
        </n-form>
        <template #footer>
        <n-space justify="end">
            <n-button @click="onClose">取消</n-button>
            <n-button type="primary" :loading="submitting" @click="onSubmit">创建</n-button>
        </n-space>
        </template>
    </n-modal>
</template>

<script setup>
import { ref, reactive } from 'vue';
import { NModal, NForm, NFormItem, NInput, NSelect, NButton, NSpace } from 'naive-ui';
import { NMessageProvider, useMessage } from 'naive-ui';
// 如果已封装 axios，可导入 request
// import request from '../utils/request';

const show = defineModel('show', { type: Boolean, default: false });
const emit = defineEmits(['created']);
const message = useMessage();

// 模板选项（示例）
const templateOptions = [
  { label: '前端 + 后端 + 部署', value: 'full' },
  { label: '仅前端构建', value: 'frontend-only' },
  { label: '仅后端构建', value: 'backend-only' },
];

const showModal = ref(true); // 默认打开
const formRef = ref(null);
const submitting = ref(false);

const form = reactive({
  name: '',
  description: '',
  template: null,
});

const rules = {
  name: { required: true, message: '请输入流水线名称', trigger: 'blur' },
};

const onClose = () => {
  show.value = false;   // 无论从哪里调用，都通过 show 通知父组件
};

const onSubmit = async () => {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  submitting.value = true;
  try {
    // TODO: 调用后端 API
    // await request.post('/api/pipelines', form);
    await new Promise((resolve) => setTimeout(resolve, 500)); // 模拟

    message.success('流水线创建成功');
    onClose();
    emit('created');  // 通知父组件刷新列表
  } catch (err) {
    message.error('创建失败');
  } finally {
    submitting.value = false;
  }
};
</script>