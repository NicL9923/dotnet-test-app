import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

// Vite output goes directly to the dotnet wwwroot so `dotnet publish`
// picks up the built SPA without an extra copy step.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: path.resolve(__dirname, '../wwwroot'),
    emptyOutDir: true,
    sourcemap: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5099',
      '/healthz': 'http://localhost:5099',
      '/openapi': 'http://localhost:5099',
    },
  },
});
