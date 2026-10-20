# __Department__ MCP 端點分流與對應說明

## 服務端點

- **部門 Ingress 端點**：`http://localhost:__Port__/__department__/mcp`
- **子系統分流端點**：`http://localhost:__Port__/__department__/{system}/mcp`
  - 範例：`http://localhost:__Port__/__department__/mes/mcp`

## 認證標頭

- `X-API-KEY: <your-api-key>`
