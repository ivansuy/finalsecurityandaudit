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

---

## 🔄 Simulador de tráfico de login

Se incluye un script en `tools/login_traffic_simulator.py` para generar tráfico
controlado hacia `POST /api/auth/login` y así validar el backoff, la auditoría y
las reglas de monitoreo.

1. Instala las dependencias del script:
   ```bash
   pip install -r tools/requirements.txt
   ```
2. Prepara tus credenciales válidas/invalidas en archivos CSV con el formato
   `email,password[,otp]`. Puedes incluir varias entradas separadas por salto de
   línea o `;` en la misma línea.
3. Ejecuta el simulador parametrizando volumen, concurrencia y tasas de éxito:
   ```bash
   python tools/login_traffic_simulator.py \
       --base-url http://localhost:5000 \
       --good-credentials data/good_creds.csv \
       --bad-credentials data/bad_creds.csv \
       --total-requests 120 --concurrency 12 --success-rate 0.25 \
       --log-file login_traffic.csv
   ```

### Parámetros destacados
- `--ip-pool`: rota direcciones IP personalizadas o genera rangos con
  `random:<cantidad>`.
- `--jitter`: agrega un retardo aleatorio antes de cada request para simular
  tráfico más natural.
- `--header`: permite inyectar encabezados adicionales (por ejemplo,
  `X-Forwarded-Proto=https`).
- `--log-file`: guarda cada intento en CSV (status, latencia, mensaje de la API,
  errores de transporte, etc.).

El resumen final muestra la distribución de respuestas (éxitos, fallos,
solicitudes bloqueadas, MFA requerido) y métricas de latencia (promedio, p95,
p99).
