import { resolve } from "path";
import { defineConfig } from "vite";

export default defineConfig({
  build: {
    outDir: "../wwwroot",
    emptyOutDir: true,
    rollupOptions: {
      input: {
        main: resolve(__dirname, "index.html"),
        perf: resolve(__dirname, "perf/index.html"),
      },
      output: {
        manualChunks: {
          monaco: ["monaco-editor"],
        },
      },
    },
  },
  server: { strictPort: true, port: 5173 },
});
