<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import * as signalR from '@microsoft/signalr'

const HUB_URL = '/hubs/demo'

interface ChatMessage {
  clientName: string
  message: string
  source: string
  timestamp: string
}

const connection = ref<signalR.HubConnection | null>(null)
const connectionId = ref('')
const connectionState = ref('断开')
const messages = ref<ChatMessage[]>([])
const inputMessage = ref('')
const sending = ref(false)

let messageListEl: HTMLElement | null = null

function scrollToBottom() {
  nextTick(() => {
    if (messageListEl) {
      messageListEl.scrollTop = messageListEl.scrollHeight
    }
  })
}

function addMessage(clientName: string, message: string, source: string) {
  messages.value.push({
    clientName,
    message,
    source,
    timestamp: new Date().toLocaleTimeString('zh-CN', { hour12: false })
  })
  scrollToBottom()
}

onMounted(async () => {
  messageListEl = document.getElementById('message-list')

  const conn = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, {
      withCredentials: false
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build()

  connection.value = conn

  conn.on('Connected', (id: string) => {
    connectionId.value = id
  })

  // 接收来自 Client1 的消息
  conn.on('ReceiveMessage', (clientName: string, message: string, source: string) => {
    addMessage(clientName, message, source)
  })

  // 接收来自自身的消息回显
  conn.on('ReceiveMessageFromClient2', (clientName: string, message: string, source: string) => {
    addMessage(clientName, message, source)
  })

  conn.onreconnecting(() => {
    connectionState.value = '重连中...'
  })

  conn.onreconnected((id?: string) => {
    connectionState.value = '已连接'
    if (id) connectionId.value = id
  })

  conn.onclose(() => {
    connectionState.value = '已断开'
    connectionId.value = ''
  })

  try {
    await conn.start()
    connectionState.value = '已连接'
  } catch (err) {
    connectionState.value = '连接失败'
    console.error('SignalR 连接失败:', err)
  }
})

onUnmounted(() => {
  connection.value?.stop()
})

async function sendMessage() {
  if (!inputMessage.value.trim() || !connection.value) return

  sending.value = true
  try {
    await connection.value.invoke('SendMessageFromClient2', 'Client2', inputMessage.value)
    addMessage('Client2', inputMessage.value, 'client2')
    inputMessage.value = ''
  } catch (err) {
    console.error('发送失败:', err)
  } finally {
    sending.value = false
  }
}

function getConnectionStateClass(state: string): string {
  switch (state) {
    case '已连接': return 'state-connected'
    case '重连中...': return 'state-reconnecting'
    default: return 'state-disconnected'
  }
}
</script>

<template>
  <div class="chat-container">
    <!-- 头部 -->
    <header class="chat-header">
      <h1>SignalR 实时通信</h1>
      <div class="connection-info">
        <span :class="['state-dot', getConnectionStateClass(connectionState)]"></span>
        <span class="state-text">{{ connectionState }}</span>
        <span v-if="connectionId" class="conn-id">ID: {{ connectionId.slice(0, 8) }}...</span>
      </div>
    </header>

    <!-- 消息列表 -->
    <div id="message-list" ref="messageListEl" class="message-list">
      <div v-if="messages.length === 0" class="empty-hint">
        等待消息中...
      </div>
      <div
        v-for="(msg, index) in messages"
        :key="index"
        :class="['message-item', msg.source === 'client1' ? 'from-client1' : 'from-client2']"
      >
        <div class="message-meta">
          <span class="message-sender">{{ msg.clientName }}</span>
          <span class="message-time">{{ msg.timestamp }}</span>
          <span class="message-tag">{{ msg.source === 'client1' ? 'Client1' : 'Client2' }}</span>
        </div>
        <div class="message-bubble">
          {{ msg.message }}
        </div>
      </div>
    </div>

    <!-- 底部发送 -->
    <div class="input-area">
      <input
        v-model="inputMessage"
        type="text"
        placeholder="输入消息，反向发送给 Client1..."
        @keyup.enter="sendMessage"
      />
      <button @click="sendMessage" :disabled="!connection || sending">
        {{ sending ? '发送中...' : '发送' }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.chat-container {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 40px);
  border: 1px solid #1e293b;
  border-radius: 12px;
  overflow: hidden;
  background: #1a2332;
}

.chat-header {
  padding: 16px 20px;
  border-bottom: 1px solid #1e293b;
  background: #0f172a;
}

.chat-header h1 {
  font-size: 18px;
  font-weight: 600;
  margin-bottom: 8px;
}

.connection-info {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.state-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.state-connected {
  background: #22c55e;
  box-shadow: 0 0 6px #22c55e;
}

.state-reconnecting {
  background: #f59e0b;
  animation: pulse 1s infinite;
}

.state-disconnected {
  background: #ef4444;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

.state-text {
  color: #94a3b8;
}

.conn-id {
  color: #64748b;
  font-family: monospace;
  font-size: 12px;
  margin-left: auto;
}

.message-list {
  flex: 1;
  overflow-y: auto;
  padding: 16px 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.empty-hint {
  text-align: center;
  color: #475569;
  margin-top: 60px;
  font-size: 14px;
}

.message-item {
  max-width: 80%;
}

.message-item.from-client1 {
  align-self: flex-start;
}

.message-item.from-client2 {
  align-self: flex-end;
}

.message-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 4px;
  font-size: 12px;
}

.message-sender {
  font-weight: 600;
  color: #94a3b8;
}

.message-time {
  color: #64748b;
}

.message-tag {
  font-size: 10px;
  padding: 1px 6px;
  border-radius: 4px;
}

.from-client1 .message-tag {
  background: #1e3a5f;
  color: #60a5fa;
}

.from-client2 .message-tag {
  background: #1f3a2f;
  color: #4ade80;
}

.message-bubble {
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 14px;
  line-height: 1.5;
  word-break: break-word;
}

.from-client1 .message-bubble {
  background: #1e3a5f;
  color: #e2e8f0;
  border-bottom-left-radius: 2px;
}

.from-client2 .message-bubble {
  background: #166534;
  color: #e2e8f0;
  border-bottom-right-radius: 2px;
}

.input-area {
  display: flex;
  gap: 10px;
  padding: 16px 20px;
  border-top: 1px solid #1e293b;
  background: #0f172a;
}

.input-area input {
  flex: 1;
  padding: 10px 14px;
  border: 1px solid #334155;
  border-radius: 8px;
  background: #1a2332;
  color: #e2e8f0;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s;
}

.input-area input:focus {
  border-color: #3b82f6;
}

.input-area input::placeholder {
  color: #475569;
}

.input-area button {
  padding: 10px 24px;
  border: none;
  border-radius: 8px;
  background: #3b82f6;
  color: white;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}

.input-area button:hover:not(:disabled) {
  background: #2563eb;
}

.input-area button:disabled {
  background: #334155;
  cursor: not-allowed;
}
</style>
