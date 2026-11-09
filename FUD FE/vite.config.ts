import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'url';
import { dirname } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));

export default defineConfig(({ mode }) => {
  // Nạp biến môi trường chính xác từ thư mục chứa vite.config.ts
  const env = loadEnv(mode, __dirname, '');

  return {
    plugins: [react()],
    server: {
      port: Number(env.VITE_PORT) || 5173,
      proxy: {
        '/api': {
          target: env.VITE_BACKEND_TARGET || 'http://localhost:5145',
          changeOrigin: true,
          secure: false,
        },
      },
    },
  };
});
