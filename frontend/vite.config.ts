import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In dev, /api and the SignalR hub are proxied to the ASP.NET Core API. In production set VITE_API_BASE_URL.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5175,
    proxy: {
      '/api': 'http://localhost:5082',
      '/hubs': { target: 'http://localhost:5082', ws: true },
    },
  },
})
