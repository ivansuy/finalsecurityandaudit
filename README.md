```markdown
# 🚗 AutoInventorySecure

Sistema de **inventario automotriz** con **autenticación robusta** (MFA TOTP, JWT, backoff) y **auditoría**, bajo arquitectura **cliente–servidor**.

## 🧱 Arquitectura y tecnologías
- **Backend**: ASP.NET Core **Web API** (.NET 8), **Identity**, **JWT**, **EF Core**, SQL Server.
- **Frontend**: ASP.NET Core **MVC (Razor)**, sesión con JWT, **Chart.js** para dashboard.
- **Auditoría**: logs de autenticación y de requests por endpoint.

Estructura del repo:
```

/AutoInventorySecure
├─ AutoInventoryBackend     # API
└─ Frontend                 # MVC

````

---

## 👥 Roles y permisos
- **Visitante**: catálogo público de vehículos (solo lectura).
- **Administrador**: CRUD completo de vehículos (**soft delete**).
- **Super-Administrador**: gestión de usuarios + acceso a **Dashboard** (y todo lo del Admin).

---

## 🔐 Seguridad implementada
- **MFA (TOTP)**: habilitar (QR + clave), verificar y uso en login (`requiresMfa=true`).
- **JWT**: expiración **2 minutos**; claims: `sub`, `email`, `nameid`, `name`, `role`.
- **Exponential Backoff** (IP+usuario): delay creciente (máx. ~30s) y **bloqueo** a la 8ª falla/10 min.
- **Auditoría**: 
  - `AuthAttemptLogs`: éxito/fallo, IP, motivo (InvalidPassword, MfaRequired, InvalidOtp, Blocked…), timestamps.
  - `RequestLogs`: método, path, status, userId, IP, elapsedMs, user-agent, timestamps.

---

## ⚙️ Cómo ejecutar localmente

### Backend (API)
1) Configura `AutoInventoryBackend/appsettings.json` (SQL Server local):
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=AutoInventory;Trusted_Connection=True;TrustServerCertificate=True;"
}
````

2. (Opcional) Crear BD

```bash
cd AutoInventoryBackend
dotnet ef database update
```

3. Ejecutar

```bash
dotnet run
```

👉 Por defecto: **[https://localhost:7229](https://localhost:7229)** (y [http://localhost:5123](http://localhost:5123))

### Frontend (MVC)

1. Configura `Frontend/appsettings.json`:

```json
"BackendApi": { "BaseUrl": "https://localhost:7229/" }
```

2. Ejecutar

```bash
cd Frontend
dotnet run
```

---

## 🔑 Endpoints clave (resumen)

### Autenticación

* `POST /api/auth/login` → `{ "email", "password", "otpCode"? }`

  * Respuestas:

    * `{"requiresMfa": true}` (si aplica)
    * `{"requiresMfa": false, "token": "<JWT>", "expiresAtUtc": "..."}`
* `POST /api/auth/enable-mfa` *(Bearer)* → `{ "manualKey", "otpauthUri" }`
* `POST /api/auth/verify-mfa` *(Bearer)* → body **string JSON** `"123456"` → “MFA habilitado”

### Usuarios *(SuperAdmin)*

* `GET /api/users` — listar
* `POST /api/users` — `{ "email", "password", "role" }`
* `DELETE /api/users/{id}`

### Vehículos

* `GET /api/vehicles?q=&page=&pageSize=` (público)
* `GET /api/vehicles/{id}` (público)
* `POST /api/vehicles` *(Admin/SuperAdmin)* — crear
* `PUT /api/vehicles/{id}` *(Admin/SuperAdmin)* — actualizar
* `DELETE /api/vehicles/{id}` *(Admin/SuperAdmin)* — **soft delete**

### Dashboard *(SuperAdmin)*

* `GET /api/dashboard/summary` → `{ windowHours, authSuccess, authFailed, topEndpoints[] }`

---

## 📊 Dashboard (Frontend)

* Tarjetas: ventana (h), logins exitosos, fallidos, **tasa de éxito**.
* Gráficas **Chart.js**:

  * **Doughnut**: éxitos vs fallos.
  * **Barras**: top endpoints.
* Tabla: endpoints y conteos.

---

## 🧪 Demostración sugerida

1. **MFA**: Habilitar (QR) → Verificar → Logout → Login ⇒ `requiresMfa=true` → ingresar OTP ⇒ token OK.
2. **Backoff**: Repetir contraseñas erróneas → ver **delay creciente** y **bloqueo** a la 8ª falla/10 min.
3. **Inventario**: CRUD de vehículos (soft delete visible en DB).
4. **Dashboard**: revisar métricas (gráficas + tabla) tras navegar/login.

---

## ✅ Checklist

* [x] MFA TOTP + JWT (2 min) + claims
* [x] Backoff + bloqueo
* [x] Auditoría de intentos y requests
* [x] Dashboard visual
* [x] Roles: Visitante / Admin / SuperAdmin
* [x] Frontend MVC consumiendo API con JWT

---

**Autor:** Gerson Sisimit · UMG 2025

```
```
