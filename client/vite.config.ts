import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Geliştirmede React, .NET API'sinden ayrı bir portta çalışır; /api istekleri
// API'ye yönlendirilir, böylece tarayıcı tarafında CORS'a gerek kalmaz.
// Üretimde `npm run build` çıktısı doğrudan API tarafından servis edilir.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: process.env.BETAKAS_API ?? "http://localhost:5187",
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: "dist",
    sourcemap: true,
  },
});
