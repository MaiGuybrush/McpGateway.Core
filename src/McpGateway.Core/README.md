# McpGateway.Core

Shared library for building department-specific MCP Gateways.

## Overview

McpGateway.Core provides common infrastructure for transforming REST APIs into LLM-friendly tools through the Model Context Protocol (MCP). This package is used by department gateways to avoid duplicating authentication, audit, validation, and transport code.

## Use Cases

- **McpGateway.Report**: Report generation and delivery tools
- **McpGateway.Spc**: Service Protection and Capacity tools
- **McpGateway.Qc**: Quality Control and audit tools

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add MCP Gateway services
builder.Services.AddMcpGateway();

/var app = builder.Build();
app.MapMcp("/report");
await app.RunMcpGatewayAsync();
```

## Architecture

The package provides:

- **Hosting**: Service registration and application startup
- **Tools**: Base classes for tool implementation
- **Auth**: JWT/API-KEY/NTLM authentication proxy (ADR-006)
- **Downstream**: HTTP client factory for downstream API calls
- **Validation**: Startup validation per ADR-004
- **Audit**: PII-redacted audit logging per ADR-006
- **Observability**: Health checks and metrics
- **Projection**: Field transformation for LLM-friendly output

## Package Details

- **PackageId**: McpGateway.Core
- **Version**: 0.2.3-preview
- **Target Framework**: net9.0
- **Authors**: Platform Team

## Dependencies

- ModelContextProtocol 1.4.1
- ModelContextProtocol.AspNetCore 1.4.1
- Microsoft.Extensions.* (transitive)

## Installation

From internal NuGet feed:

```bash
dotnet add package McpGateway.Core --version 0.2.3-preview --source http://10.53.216.186:5000/v3/index.json
```

## Contributing

This is a platform component with high bus factor (≥2 maintainers required).

## License

Internal use only.
