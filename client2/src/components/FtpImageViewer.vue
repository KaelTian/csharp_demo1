<script setup lang="ts">
import { ref, onMounted } from 'vue'

/* ==================== FTP 源 ==================== */
const FTP_API_BASE = '/api/ftp/images'
const HTTP_IMAGE_BASE = 'http://192.168.0.189:9526'

const ftpImages = ref<string[]>([])
const ftpLoading = ref(false)
const ftpError = ref('')
const selectedImage = ref<string | null>(null)
const previewType = ref<'ftp' | 'http'>('ftp')

async function loadFtpImages() {
  ftpLoading.value = true
  ftpError.value = ''
  try {
    const res = await fetch(FTP_API_BASE)
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    ftpImages.value = await res.json()
  } catch (err: any) {
    ftpError.value = '无法连接 FTP 服务器: ' + err.message
  } finally {
    ftpLoading.value = false
  }
}

function ftpImageUrl(name: string) {
  return `${FTP_API_BASE}/${encodeURIComponent(name)}`
}

function openFtpPreview(name: string) {
  selectedImage.value = name
  previewType.value = 'ftp'
}

/* ==================== HTTP 源 ==================== */
const httpFilename = ref('')
const httpPreviewUrl = ref('')
const httpError = ref('')

function loadHttpImage() {
  const name = httpFilename.value.trim()
  if (!name) {
    httpError.value = '请输入文件名'
    return
  }
  httpError.value = ''
  httpPreviewUrl.value = `${HTTP_IMAGE_BASE}/${name}`
  selectedImage.value = name
  previewType.value = 'http'
}

/* ==================== 通用 ==================== */
function closePreview() {
  selectedImage.value = null
  httpPreviewUrl.value = ''
}

onMounted(loadFtpImages)
</script>

<template>
  <div class="image-viewer">
    <h2 class="section-title">📁 FTP 服务器图片</h2>
    <p class="section-desc">FTP: ftp://192.168.0.189（通过 API 代理）</p>

    <!-- FTP 加载状态 -->
    <div v-if="ftpLoading" class="status-info">加载中...</div>
    <div v-else-if="ftpError" class="status-error">{{ ftpError }}</div>

    <!-- FTP 图片网格 -->
    <div v-else-if="ftpImages.length === 0" class="status-info">暂无图片</div>
    <div v-else class="image-grid">
      <div
        v-for="name in ftpImages"
        :key="name"
        class="image-card"
        @click="openFtpPreview(name)"
      >
        <img :src="ftpImageUrl(name)" :alt="name" loading="lazy" />
        <span class="image-name">{{ name }}</span>
      </div>
    </div>

    <hr class="divider" />

    <h2 class="section-title">🌐 HTTP 服务器图片</h2>
    <p class="section-desc">HTTP: http://192.168.0.189:9526（直接访问）</p>

    <!-- HTTP 输入 -->
    <div class="http-input-row">
      <input
        v-model="httpFilename"
        type="text"
        placeholder="输入文件名，如 image.jpg"
        @keyup.enter="loadHttpImage"
      />
      <button @click="loadHttpImage">查看</button>
    </div>
    <p v-if="httpError" class="status-error">{{ httpError }}</p>

    <hr class="divider" />

    <!-- 大图预览（遮罩层） -->
    <div v-if="selectedImage" class="preview-overlay" @click.self="closePreview">
      <button class="preview-close" @click="closePreview">✕</button>
      <div class="preview-content">
        <p class="preview-filename">
          {{ previewType === 'ftp' ? '[FTP] ' : '[HTTP] ' }}{{ selectedImage }}
        </p>
        <img
          v-if="previewType === 'ftp'"
          :src="ftpImageUrl(selectedImage)"
          :alt="selectedImage"
        />
        <img
          v-else
          :src="httpPreviewUrl"
          :alt="selectedImage"
          @error="httpError = '图片加载失败，请检查文件名是否正确'"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.image-viewer {
  padding: 20px;
  background: #1a2332;
  border-radius: 12px;
  border: 1px solid #1e293b;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 4px;
}

.section-desc {
  font-size: 12px;
  color: #64748b;
  margin-bottom: 14px;
}

.divider {
  border: none;
  border-top: 1px solid #1e293b;
  margin: 20px 0;
}

.status-info {
  color: #64748b;
  padding: 20px 0;
  text-align: center;
}

.status-error {
  color: #f87171;
  font-size: 13px;
  margin-top: 6px;
}

.image-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 12px;
}

.image-card {
  background: #0f172a;
  border: 1px solid #1e293b;
  border-radius: 8px;
  overflow: hidden;
  cursor: pointer;
  transition: border-color 0.2s, transform 0.15s;
}

.image-card:hover {
  border-color: #3b82f6;
  transform: translateY(-2px);
}

.image-card img {
  width: 100%;
  height: 120px;
  object-fit: cover;
  display: block;
}

.image-name {
  display: block;
  padding: 6px 8px;
  font-size: 11px;
  color: #94a3b8;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.http-input-row {
  display: flex;
  gap: 10px;
}

.http-input-row input {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid #334155;
  border-radius: 6px;
  background: #0f172a;
  color: #e2e8f0;
  font-size: 13px;
  outline: none;
}

.http-input-row input:focus {
  border-color: #3b82f6;
}

.http-input-row button {
  padding: 8px 18px;
  border: none;
  border-radius: 6px;
  background: #3b82f6;
  color: #fff;
  font-size: 13px;
  cursor: pointer;
}

.http-input-row button:hover {
  background: #2563eb;
}

/* 遮罩预览 */
.preview-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.preview-close {
  position: absolute;
  top: 20px;
  right: 28px;
  background: none;
  border: none;
  color: #e2e8f0;
  font-size: 28px;
  cursor: pointer;
  z-index: 10;
}

.preview-content {
  text-align: center;
  max-width: 90vw;
  max-height: 90vh;
}

.preview-filename {
  color: #94a3b8;
  font-size: 13px;
  margin-bottom: 12px;
}

.preview-content img {
  max-width: 100%;
  max-height: 80vh;
  border-radius: 8px;
  box-shadow: 0 4px 30px rgba(0, 0, 0, 0.5);
}
</style>
