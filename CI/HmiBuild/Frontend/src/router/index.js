import { createRouter, createWebHistory } from 'vue-router';
import HomeView from '../views/HomeView.vue';     // 你原来的 App.vue 内容可移到这里
import LoginView from '../views/LoginView.vue';

const routes = [
  {
    path: '/',
    name: 'home',
    component: HomeView,
    meta: { requiresAuth: true },
  },
  {
    path: '/login',
    name: 'login',
    component: LoginView,
  },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

// 全局前置守卫：未登录跳转登录页
router.beforeEach((to, from, next) => {
  const token = localStorage.getItem('token');
  if (to.meta.requiresAuth && !token) {
    next('/login');
  } else if (to.path === '/login' && token) {
    next('/');  // 已登录则跳转首页
  } else {
    next();
  }
});

export default router;