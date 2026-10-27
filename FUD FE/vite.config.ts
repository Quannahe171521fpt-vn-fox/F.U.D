import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'https://localhost:7212',
        changeOrigin: true,
        secure: false, // Bỏ qua cảnh báo SSL certificate localhost tự ký của .NET
      },
    },
  },
})
