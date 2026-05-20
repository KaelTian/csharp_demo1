<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'

/* ==================== FTP 源 ==================== */
const FTP_API_BASE = '/api/ftp/images'
const HTTP_IMAGE_BASE = 'http://192.168.0.189:9526'

const ftpFiles = ref<string[]>([])
const directories = ref<string[]>([])
const ftpLoading = ref(false)
const ftpError = ref('')
const currentDir = ref('')
const dirHistory = ref<string[]>([])

const selectedImage = ref<string | null>(null)
const previewType = ref<'ftp' | 'http'>('ftp')

// 面包屑：把 currentDir 按 "/" 拆分成 [{ name, path }, ...]
const breadcrumbs = computed(() => {
  const segments = currentDir.value ? currentDir.value.split('/') : []
  const crumbs: { name: string; path: string }[] = []
  let accumulated = ''
  for (const seg of segments) {
    accumulated = accumulated ? `${accumulated}/${seg}` : seg
    crumbs.push({ name: seg, path: accumulated })
  }
  return crumbs
})

async function loadFtpImages(dir?: string) {
  ftpLoading.value = true
  ftpError.value = ''
  try {
    const url = dir ? `${FTP_API_BASE}?path=${encodeURIComponent(dir)}` : FTP_API_BASE
    const res = await fetch(url)
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    const data = await res.json()
    directories.value = data.directories || []
    ftpFiles.value = data.files || []
    currentDir.value = data.currentPath || ''
  } catch (err: any) {
    ftpError.value = '无法连接 FTP 服务器: ' + err.message
  } finally {
    ftpLoading.value = false
  }
}

function enterDir(name: string) {
  const next = currentDir.value ? `${currentDir.value}/${name}` : name
  dirHistory.value.push(currentDir.value)
  loadFtpImages(next)
}

function goBack() {
  const prev = dirHistory.value.pop()
  if (prev !== undefined) loadFtpImages(prev || undefined)
}

function goToDir(path: string) {
  dirHistory.value.push(currentDir.value)
  loadFtpImages(path || undefined)
}

function ftpImageUrl(path: string) {
  return `${FTP_API_BASE}/${path.split('/').map(encodeURIComponent).join('/')}`
}

function openFtpPreview(name: string) {
  // name 可能是含路径的文件名，拼接 currentDir 构建完整路径
  const fullPath = currentDir.value ? `${currentDir.value}/${name}` : name
  selectedImage.value = fullPath
  previewType.value = 'ftp'
}

/* ==================== 流式下载示例 ==================== */
const downloadProgress = ref(0)
const downloading = ref(false)
const downloadStatus = ref('')

async function downloadWithProgress(name: string) {
  downloading.value = true
  downloadProgress.value = 0
  downloadStatus.value = '准备下载...'

  try {
    const res = await fetch(ftpImageUrl(name))

    const total = Number(res.headers.get('content-length') || 0)
    downloadStatus.value = `文件大小: ${(total / 1024).toFixed(1)} KB`

    const reader = res.body!.getReader()
    const chunks: BlobPart[] = []
    let received = 0

    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      chunks.push(value.buffer)
      received += value.length
      if (total) {
        downloadProgress.value = Math.round((received / total) * 100)
      }
    }

    const blob = new Blob(chunks, { type: res.headers.get('content-type') || 'image/jpeg' })
    const url = URL.createObjectURL(blob)

    const a = document.createElement('a')
    a.href = url
    a.download = name.split('/').pop() || name
    a.click()
    URL.revokeObjectURL(url)

    downloadStatus.value = '✅ 下载完成'
  } catch (err: any) {
    downloadStatus.value = '❌ 下载失败: ' + err.message
  } finally {
    downloading.value = false
    downloadProgress.value = 0
  }
}

/* ==================== Range 请求示例 ==================== */
async function fetchImageRange(name: string, signal?: AbortSignal) {
  const headRes = await fetch(ftpImageUrl(name), { method: 'HEAD' })
  const total = Number(headRes.headers.get('content-length') || 0)
  if (!total) throw new Error('无法获取文件大小')

  const chunkSize = Math.ceil(total / 4)
  const chunks: BlobPart[] = []

  for (let start = 0; start < total; start += chunkSize) {
    const end = Math.min(start + chunkSize - 1, total - 1)
    const res = await fetch(ftpImageUrl(name), {
      headers: { Range: `bytes=${start}-${end}` },
      signal,
    })

    if (res.status !== 206) throw new Error('服务器不支持 Range 请求')
    chunks.push(await res.blob())
  }

  return new Blob(chunks, { type: headRes.headers.get('content-type') || 'image/jpeg' })
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

onMounted(() => loadFtpImages())
</script>

<template>
  <div class="image-viewer">
    <h2 class="section-title">📁 FTP 服务器图片</h2>
    <p class="section-desc">FTP: ftp://192.168.0.189（通过 API 代理）</p>

    <!-- 面包屑导航 -->
    <nav v-if="!ftpLoading" class="breadcrumb">
      <span
        class="crumb"
        :class="{ active: !currentDir }"
        @click="goToDir('')"
      >根目录</span>
      <template v-for="(crumb, i) in breadcrumbs" :key="crumb.path">
        <span class="crumb-sep">/</span>
        <span
          class="crumb"
          :class="{ active: i === breadcrumbs.length - 1 }"
          @click="goToDir(crumb.path)"
        >{{ crumb.name }}</span>
      </template>
    </nav>

    <!-- FTP 加载状态 -->
    <div v-if="ftpLoading" class="status-info">加载中...</div>
    <div v-else-if="ftpError" class="status-error">{{ ftpError }}</div>
    <template v-else>
      <!-- 目录网格 -->
      <div v-if="directories.length > 0" class="dir-grid">
        <div
          v-for="dir in directories"
          :key="dir"
          class="dir-card"
          @click="enterDir(dir)"
        >
          <span class="dir-icon">📁</span>
          <span class="dir-name">{{ dir }}</span>
        </div>
      </div>

      <!-- 返回上级 -->
      <button
        v-if="dirHistory.length > 0"
        class="back-btn"
        @click="goBack"
      >⬆ 返回上级</button>

      <!-- 图片网格 -->
      <div v-if="ftpFiles.length === 0 && directories.length === 0" class="status-info">此目录下没有文件</div>
      <div v-else-if="ftpFiles.length > 0" class="image-grid">
        <div
          v-for="name in ftpFiles"
          :key="name"
          class="image-card"
          @click="openFtpPreview(name)"
        >
          <img :src="ftpImageUrl(currentDir ? `${currentDir}/${name}` : name)" :alt="name" loading="lazy" />
          <span class="image-name">{{ name }}</span>
        </div>
      </div>
    </template>

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

/* ===== 面包屑 ===== */
.breadcrumb {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 2px;
  margin-bottom: 14px;
  padding: 8px 12px;
  background: #0f172a;
  border-radius: 6px;
  font-size: 13px;
}

.crumb {
  color: #64748b;
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 4px;
  transition: color 0.15s, background 0.15s;
}

.crumb:hover {
  color: #60a5fa;
  background: #1e293b;
}

.crumb.active {
  color: #e2e8f0;
  font-weight: 500;
  cursor: default;
}

.crumb-sep {
  color: #334155;
  margin: 0 2px;
}

/* ===== 目录网格 ===== */
.dir-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 10px;
  margin-bottom: 16px;
}

.dir-card {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  background: #0f172a;
  border: 1px solid #1e293b;
  border-radius: 8px;
  cursor: pointer;
  transition: border-color 0.2s, background 0.15s;
}

.dir-card:hover {
  border-color: #3b82f6;
  background: #1e293b;
}

.dir-icon {
  font-size: 18px;
  line-height: 1;
}

.dir-name {
  font-size: 12px;
  color: #94a3b8;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* ===== 返回上级 ===== */
.back-btn {
  display: block;
  width: 100%;
  padding: 8px 12px;
  margin-bottom: 14px;
  background: #0f172a;
  border: 1px dashed #334155;
  border-radius: 6px;
  color: #64748b;
  font-size: 12px;
  cursor: pointer;
  transition: color 0.15s, border-color 0.15s;
}

.back-btn:hover {
  color: #60a5fa;
  border-color: #3b82f6;
}

/* ===== 图片网格 ===== */
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

/* ===== HTTP ===== */
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
