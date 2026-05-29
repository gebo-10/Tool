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
import { NModal, NForm, NFormItem, NInput, NSelect, NButton, NSpace, useMessage } from 'naive-ui';
import axios from 'axios'; // 如果项目中已封装 request，可替换为你的实例

const show = defineModel('show', { type: Boolean, default: false });
const emit = defineEmits(['created']);
const message = useMessage();

// 模板选项（示例）
const templateOptions = [
    { label: '前端 + 后端 + 部署', value: 'full' },
    { label: '仅前端构建', value: 'frontend-only' },
    { label: '仅后端构建', value: 'backend-only' },
];

// 根据模板生成默认的 DAG JSON（可自定义）
function generateDagJson(template) {
    // 简单示例，实际应根据模板生成有效的 DAG 结构
    const baseDag = {
        nodes: [],
        edges: []
    };
    if (template === 'full') {
        baseDag.nodes = [
            { id: 'build-frontend', type: 'BuildFrontend', parameters: {} },
            { id: 'build-backend', type: 'BuildBackend', parameters: {} },
            { id: 'deploy', type: 'Deploy', parameters: {} }
        ];
        baseDag.edges = [
            { fromNode: 'build-frontend', fromPin: 'output', toNode: 'deploy', toPin: 'frontend' },
            { fromNode: 'build-backend', fromPin: 'output', toNode: 'deploy', toPin: 'backend' }
        ];
    } else if (template === 'frontend-only') {
        baseDag.nodes = [{ id: 'build-frontend', type: 'BuildFrontend', parameters: {} }];
    } else if (template === 'backend-only') {
        baseDag.nodes = [{ id: 'build-backend', type: 'BuildBackend', parameters: {} }];
    }
    return JSON.stringify(baseDag);
}

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
    show.value = false;
};

const onSubmit = async () => {
    try {
        await formRef.value?.validate();
    } catch {
        return;
    }

    submitting.value = true;
    try {
        // 根据选择的模板生成 DAG JSON
        const dagJson = generateDagJson(form.template);
        // 调用后端 API
        await axios.post('/api/pipelines', {
            name: form.name,
            description: form.description,
            dagJson: dagJson
        });
        message.success('流水线创建成功');
        onClose();
        emit('created'); // 通知父组件刷新列表
    } catch (err) {
        console.error(err);
        message.error(err.response?.data?.message || '创建失败');
    } finally {
        submitting.value = false;
    }
};
</script>