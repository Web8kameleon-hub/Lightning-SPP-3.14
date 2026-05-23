# Lightning SPP Cloud REST API

## Start server

```bash
dotnet run -- serve 8080
```

## Endpoints

- `GET /api/health`
- `GET /api/license/status`
- `POST /api/license/activate`
- `POST /api/license/issue`

### Issue Key Request

Use form-encoded fields (`application/x-www-form-urlencoded`):

```text
email=admin@company.com
company=Company
tier=unlimited
devices=10
validityDays=365
useInternalQuantumSecurity=false
```

### Dashboard

- `GET /dashboard`

### Landing page

- `GET /`
