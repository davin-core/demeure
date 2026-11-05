import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'API_')
  const apiTarget = env.API_PROXY_TARGET || 'http://localhost:5076'
  const target = new URL(apiTarget)

  if (!['http:', 'https:'].includes(target.protocol)) {
    throw new Error('API_PROXY_TARGET must use HTTP or HTTPS.')
  }

  const proxy = { '/api': { target: apiTarget, changeOrigin: true } }

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
    },
    server: {
      host: 'localhost',
      port: 5173,
      strictPort: true,
      proxy,
    },
    preview: {
      host: 'localhost',
      port: 4173,
      strictPort: true,
      proxy,
    },
  }
})
