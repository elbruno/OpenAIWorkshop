import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    host: '127.0.0.1',  // Force IPv4
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:8001',  // Use IPv4 explicitly
        changeOrigin: true,
      },
      '/ws': {
        target: 'ws://127.0.0.1:8001',  // Use IPv4 explicitly
        ws: true,
      },
    },
  },
})
