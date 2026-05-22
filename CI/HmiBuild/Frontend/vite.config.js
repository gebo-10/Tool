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
        rewrite: (path) => path.replace(/^\/api/, '') // 去掉 /api 前缀
      }
    }
  }
})
