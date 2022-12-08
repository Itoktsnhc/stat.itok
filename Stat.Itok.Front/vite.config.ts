import { defineConfig } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [svelte({
    onwarn(warning, defaultHandler) {
      // don't warn on <marquee> elements, cos they're cool
      if (warning.code === "a11y-distracting-elements") return;

      // handle all other warnings normally
      defaultHandler(warning);
    },
  })],
  server: {
    proxy: {
      "/api": {
        target: "http://127.0.0.1:7071/",
        changeOrigin: true,
      },
    },
  },
});
