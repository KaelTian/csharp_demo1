<script setup lang="ts">
import { ref } from 'vue'
import SignalRChat from './components/SignalRChat.vue'
import FtpImageViewer from './components/FtpImageViewer.vue'

const activeTab = ref<'chat' | 'images'>('chat')
</script>

<template>
  <div class="app-layout">
    <nav class="tab-bar">
      <button
        :class="['tab-btn', { active: activeTab === 'chat' }]"
        @click="activeTab = 'chat'"
      >
        💬 SignalR 聊天
      </button>
      <button
        :class="['tab-btn', { active: activeTab === 'images' }]"
        @click="activeTab = 'images'"
      >
        🖼️ 图片浏览
      </button>
    </nav>

    <SignalRChat v-if="activeTab === 'chat'" />
    <FtpImageViewer v-else />
  </div>
</template>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  background: #0f172a;
  color: #e2e8f0;
  min-height: 100vh;
}

#app {
  max-width: 900px;
  margin: 0 auto;
  padding: 20px;
}

.app-layout {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.tab-bar {
  display: flex;
  gap: 8px;
  padding: 12px 16px;
  background: #1a2332;
  border: 1px solid #1e293b;
  border-radius: 12px;
}

.tab-btn {
  flex: 1;
  padding: 10px 16px;
  border: 1px solid transparent;
  border-radius: 8px;
  background: transparent;
  color: #64748b;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.tab-btn:hover {
  background: #1e293b;
  color: #94a3b8;
}

.tab-btn.active {
  background: #1e3a5f;
  color: #60a5fa;
  border-color: #3b82f6;
}
</style>
