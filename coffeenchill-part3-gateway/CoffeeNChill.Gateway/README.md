# CoffeeNChill Gateway (Part 3 – Peter)

ASP.NET Core 8 Web API that gives clients one front door to the CoffeeNChill storage:
**Tables** (menu, orders), **Queues** (order placement) and **Files** (staff documents).
It talks to Azure Storage directly through the SDKs, using the same table/queue/share names and
schemas as the Functions project, so both can run side by side.

```
Client ──► Gateway (:5000) ──► Azure Table  MenuItems / Orders
                          ├──► Azure Queue  order-processing-queue ──► ProcessOrderQueue Function ──► Orders table
                          └──► Azure Files  staff-docs
```

## Endpoints

| Method | Route | Access |
|---|---|---|
| GET | `/api/menu` | Public |
| GET | `/api/menu/category/{category}` | Public |
| GET | `/api/menu/{category}/{id}` | Public |
| POST / PUT / DELETE | `/api/menu`, `/api/menu/{category}/{id}` | Admin, Staff |
| POST | `/api/orders/queue` | Public |
| GET | `/api/orders/{orderId}` | Public |
| PUT | `/api/orders/status` | Admin, Staff |
| POST | `/api/documents/upload` (multipart, field `file`) | Admin, Staff |
| GET | `/api/documents`, `/api/documents/download/{fileName}` | Admin, Staff |
| DELETE | `/api/documents/{fileName}` | Admin, Staff |
| GET | `/health`, `/swagger` | Public |

Errors are RFC 7807 `application/problem+json` with a `traceId`:
400 validation, 401/403 auth, 404 not found, 409 conflict, 503 storage/queue unreachable.

## Run locally

```bash
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite \
  azurite --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0 --skipApiVersionCheck
dotnet run --project CoffeeNChill.Gateway      # http://localhost:5000/swagger
dotnet test                                    # xUnit tests (no Azurite needed)
```

## Hand-off notes

**Thandokuhle (auth):** replace the `AUTH PLACEHOLDER` block in `Program.cs` (and delete
`Auth/AuthPlaceholder.cs`'s handler; keep `AppRoles`) with JwtBearer + the Swagger "Bearer"
definition (see the TODO in `AddSwaggerGen`). Controller attributes already use
`[Authorize(Roles = "Admin,Staff")]`, so nothing else changes. Until then protected routes return 401.

**Ukhona (Docker/VPS):**
- Listen on 5000: set `ASPNETCORE_URLS=http://+:5000` (.NET 8 images default to 8080).
- Settings are read from environment variables: `AzureWebJobsStorage` (Tables + Queues) and
  `AzureFileStorage` (File share). In compose, point `AzureWebJobsStorage` at the Azurite service name.
- **Azurite does not emulate Azure Files.** The document endpoints need a real storage account
  connection string in `AzureFileStorage` (same value the Functions project uses).
- Build context is the repo root (`CoffeeNChill.Gateway/` is a separate project from the root Functions project).

**Pfano (Postman):** routes above; queue body example:
`{"customerName":"Jane Smith","selectedItemSKUs":["COF-001","PAS-104"],"totalPrice":65.00}`
(`orderId`/`orderTimestamp` are optional). Menu create uses `category`, `sku`, `name`, `description`, `price`, `isAvailable`.
