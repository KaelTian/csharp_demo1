import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/hubs': {
        target: 'http://localhost:5060',
        ws: true
      },
      '/api': {
        target: 'http://localhost:5060'
      }
    }
  }
})
