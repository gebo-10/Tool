import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,               // 前端开发服务器端口
    proxy: {
      '/api': {               // 匹配所有以 /api 开头的请求
        target: 'http://localhost:5255', // 后端地址
        changeOrigin: true,
        //rewrite: (path) => path.replace(/^\/api/, '') // 去掉 /api 前缀
        configure: (proxy) => {
        proxy.on('proxyReq', (proxyReq, req, res) => {
          // 设置请求头，告诉后端这是一个长连接
          proxyReq.setHeader('Connection', 'keep-alive');
        });
        proxy.on('proxyRes', (proxyRes, req, res) => {
          // 禁用代理响应缓冲
          proxyRes.headers['cache-control'] = 'no-cache';
          proxyRes.headers['x-accel-buffering'] = 'no'; // 禁用 nginx 类缓冲（如果有）
          delete proxyRes.headers['content-length'];     // 防止提前结束
        });
      },
      }
    }
  }
})
