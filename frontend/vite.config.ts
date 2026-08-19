import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/apis': 'http://localhost:5230',
      '/proxy': 'http://localhost:5230',
      '/health': 'http://localhost:5230',
    },
  },
});
