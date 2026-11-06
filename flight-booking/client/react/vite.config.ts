import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Force a single instance of shared @a2ui packages to avoid
    // duplicate-declaration type errors from the nested node_modules.
    dedupe: ['@a2ui/web_core', '@a2ui/react', 'react', 'react-dom'],
  },
  build: {
    // Increase chunk size warning limit for large Syncfusion components
    chunkSizeWarningLimit: 1000,
  },
  server: {
    port: 5173,
    strictPort: false,
    host: '0.0.0.0',
    allowedHosts: true,
  },
});
