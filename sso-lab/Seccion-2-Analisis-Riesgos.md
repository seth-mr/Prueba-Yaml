# 2) Análisis de riesgos (antes de construir)
Fecha: 2025-11-19

## 2.1 Activos y datos
- **A1 – Identidad del usuario (credenciales, sesión IdP)**: sesión mantenida en cookie `session` con `httpOnly`, `secure` y `sameSite=lax`, vinculada al flujo de login/callback del BFF. 【F:web-bff/index.js†L20-L133】
- **A2 – Tokens OIDC (ID/Access/Refresh)**: se almacenan en la sesión tras el callback para reutilizarlos en vistas y llamadas API. 【F:web-bff/index.js†L122-L156】
- **A3 – Datos personales básicos (email, nombre)**: se obtienen del ID Token y se renderizan en la vista de perfil. 【F:web-bff/index.js†L146-L156】
- **A4 – API protegida (endpoints, lógica de negocio)**: endpoint `/api/perfil` requiere `Authorization: Bearer` y consulta `userinfo` con el access token. 【F:api/perfil.js†L5-L25】
- **A5 – Infraestructura (VM, SO, red, servicios)**: BFF Node.js expuesto en HTTPS local con certificados PEM; el IdP se descubre por la URL de entorno (`ISSUER`) y el tráfico hacia él es HTTP. 【F:web-bff/index.js†L9-L12】【F:web-bff/index.js†L44-L56】【F:web-bff/index.js†L170-L175】

> Referencia rápida de propietarios/criticidad: ver `assets.yml` en la raíz del repo.

## 2.2 Contexto y supuestos
- **Uso docente y red interna**: el BFF activa `trust proxy` y levanta un servidor HTTPS local con certificados propios, pensado para laboratorio sin exposición pública. 【F:web-bff/index.js†L9-L32】【F:web-bff/index.js†L170-L175】
- **IdP centralizado (Keycloak)**: el cliente OIDC se descubre con `Issuer.discover` y se registra como *public client* (`token_endpoint_auth_method: "none"`). 【F:web-bff/index.js†L44-L56】
- **Gestión de sesión y correlación en el BFF**: `/login` genera `code_verifier`, `code_challenge` (S256), `state` y `nonce`; `/callback` reaprovecha esos valores desde la sesión para canjear el código. 【F:web-bff/index.js†L67-L133】
- **Validación del access token por `userinfo`**: la API integrada usa `client.userinfo(accessToken)` y devuelve el JSON resultante; no hay validación local de firma. 【F:api/perfil.js†L5-L25】
- **Canales actuales**: navegador↔BFF por HTTPS (certificados locales); BFF/API↔Keycloak según `ISSUER` (HTTP en este laboratorio). 【F:web-bff/index.js†L9-L12】【F:web-bff/index.js†L44-L56】

## 2.3 DFD (Data Flow Diagram) y fronteras de confianza
```mermaid
flowchart TD
  subgraph Usuario
    U[Browser]
  end

  subgraph BFF[Web BFF (HTTPS :3000)]
    APP[Express app
/web-bff/index.js]
    API[/Router /api/perfil
(api/perfil.js)/]
  end

  subgraph IdP[Keycloak (HTTP :8080)]
    KC[Issuer OIDC]
  end

  U -- "HTTPS GET /login, /callback, /perfil" --> APP
  U -- "HTTPS GET /api/perfil\nAuthorization: Bearer AT" --> API

  APP -- "HTTP OIDC discovery/auth/token" --> KC
  API -- "HTTP userinfo(access_token)" --> KC
```

**Fronteras reales**
- **Navegador ↔ BFF**: HTTPS con certificados locales (cookie `secure`, `httpOnly`, `sameSite=lax`).
- **BFF/API ↔ IdP**: HTTP según `ISSUER` de entorno; sin cifrado en fase 1.

> Nota: no existe base de datos en el código actual; la única persistencia es la sesión en memoria.

## 2.4 Modelo de amenazas (STRIDE)
| Categoría | Ejemplo en este sistema | Evidencia | Controles propuestos (basados en código actual) |
| --- | --- | --- | --- |
| Spoofing | Suplantar sesión o token sin correlación | Generación/uso de `state` y `nonce` en `/login` y `/callback`. | Fortalecer MFA en fase 2; forzar validación de `iss/aud/exp/nonce` del ID Token antes de usarlo. 【F:web-bff/index.js†L67-L133】 |
| Tampering | Modificar código/tokens en tránsito IdP↔BFF | `ISSUER` apunta a HTTP; los flujos OIDC y `userinfo` viajan sin TLS. | Migrar a HTTPS (fase 2 con Nginx+Certbot); validar hash `code_challenge` en IdP y registrar redirect URIs exactas. 【F:web-bff/index.js†L44-L56】【F:web-bff/index.js†L84-L120】【F:api/perfil.js†L15-L22】 |
| Repudiation | Usuario niega acciones | El BFF y la API no registran auditoría de llamadas ni `sub/jti`. | Añadir logging firmado con correlación de `sub` y `state`, y retención WORM para revisiones. 【F:web-bff/index.js†L94-L135】【F:api/perfil.js†L24-L27】 |
| Information Disclosure | Fuga de tokens o datos personales | Tokens se guardan en sesión; `userinfo` e ID Token incluyen email/nombre. | Evitar loguear tokens; aplicar mínimos scopes y activar HTTPS extremo a extremo; limpiar sesión al cerrar. 【F:web-bff/index.js†L67-L168】【F:api/perfil.js†L15-L25】 |
| DoS | Caída del servicio por fallo de IdP | `initOidcClient` termina el proceso con `process.exit(1)` si falla la discovery. | Implementar reintentos y circuit breaker; health checks para reinicios controlados. 【F:web-bff/index.js†L44-L65】 |
| Elevation of Privilege | Uso de API sin validar firma del access token | `/api/perfil` usa `client.userinfo` pero no verifica JWKS localmente; depende de llamada HTTP. | Validar JWT localmente (RS256 + JWKS); limitar roles/claims antes de responder; añadir rate limiting. 【F:api/perfil.js†L5-L25】 |

## 2.5 Matriz de riesgos
Probabilidad (P) e Impacto (I) de 1–5; Riesgo = P×I (Rojo ≥ 12).

| ID | Amenaza | Activo | P | I | Riesgo | Controles (ASVS/OAuth/OIDC) | Evidencia |
| --- | --- | --- | --- | --- | --- | --- | --- |
| R1 | Intercepción o manipulación del code/token en IdP↔BFF (HTTP) | A2 | 4 | 4 | 16 | Migrar a HTTPS (V1), PKCE+state/nonce estrictos (RFC 7636), redirect URIs exactas (OAuth 2.1). | `ISSUER` HTTP y uso de PKCE/state/nonce. 【F:web-bff/index.js†L44-L120】 |
| R2 | Access token manipulado sin validación de firma local | A2/A4 | 3 | 4 | 12 | Validar RS256+JWKS (V2), verificar `aud/exp` antes de `userinfo`, cache de JWKS. | `/api/perfil` confía en `client.userinfo` sin validar JWT. 【F:api/perfil.js†L15-L25】 |
| R3 | Fuga de tokens/datos por captura de sesión | A1/A2/A3 | 3 | 4 | 12 | HTTPS extremo (V3), políticas de expiración corta y regeneración de sesión tras login, no loguear tokens. | Tokens en sesión y logging del callback. 【F:web-bff/index.js†L67-L136】 |
| R4 | Caída del BFF si falla discovery del IdP | A5 | 3 | 3 | 9 | Reintentos con backoff, circuit breaker y healthchecks (V1 Disponibilidad). | `process.exit(1)` ante error en `initOidcClient`. 【F:web-bff/index.js†L44-L65】 |
| R5 | Bypass o error en API por cliente OIDC no inicializado | A4 | 3 | 2 | 6 | Validar `req.client` y proteger acceso con middleware dedicado; pruebas negativas. | Chequeo `req.client` y dependencia de variable global. 【F:api/perfil.js†L5-L25】【F:web-bff/index.js†L139-L145】 |
| R6 | Falta de trazabilidad/auditoría | A1/A4 | 2 | 3 | 6 | Logging firmado con `sub`, `jti`, `state` (V2, V3); correlación en reverse proxy. | Ausencia de logs de seguridad en rutas principales. 【F:web-bff/index.js†L94-L168】【F:api/perfil.js†L24-L27】 |
