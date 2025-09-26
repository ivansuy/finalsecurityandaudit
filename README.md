# 🚗 AutoInventorySecure

Sistema de **inventario automotriz** con **autenticación robusta** (MFA TOTP, JWT con expiración corta, backoff anti-fuerza bruta) y **auditoría**. Arquitectura **cliente–servidor**: API ASP.NET Core (.NET 8) + Frontend ASP.NET Core MVC (Razor).

---

## 🧱 Stack (breve)
- **API**: ASP.NET Core Web API, Identity, JWT, EF Core, SQL Server.
- **Frontend**: ASP.NET Core MVC (Razor), sesión con JWT, **Chart.js** para dashboard.
- **Logs**: intentos de autenticación y requests por endpoint.

---

## 👥 Roles
- **Visitante**: catálogo público (solo lectura).
- **Administrador**: CRUD de vehículos (**soft delete**).
- **Super-Administrador**: gestión de usuarios + acceso a **Dashboard** (y todo lo del Admin).

---

## 🔐 Seguridad
- **MFA (TOTP)**: habilitar (QR + clave), verificar y uso en login (`requiresMfa=true`).
- **JWT**: expiración **2 minutos**; claims: `sub`, `email`, `nameid`, `name`, `role`.
- **Exponential Backoff** (IP+usuario): delay progresivo (máx. ~30s) y **bloqueo** a la 8ª falla en 10 min.
- **Auditoría**  
  - `AuthAttemptLogs`: éxito/fallo, IP, motivo (InvalidPassword, MfaRequired, InvalidOtp, Blocked…), timestamps.  
  - `RequestLogs`: método, path, status, userId, IP, elapsedMs, user-agent, timestamps.
