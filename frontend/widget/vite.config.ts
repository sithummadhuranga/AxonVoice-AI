import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    copyPublicDir: false,
    lib: {
      entry: 'src/loader.ts',
      name: 'AxonVoiceWidget',
      formats: ['iife'],
      fileName: () => 'axonvoice-widget.js',
    },
    rollupOptions: {
      external: [],
    },
    minify: true,
    sourcemap: true,
  },
});
