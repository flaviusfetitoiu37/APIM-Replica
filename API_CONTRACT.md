# APIM Replica — Contract API

**Base URL:** `http://localhost:5230`
**Content-Type:** `application/json` pentru toate cererile cu body.

> Versiune actualizată după auditul din 18.08.2026. Secțiunea [Ce s-a schimbat](#ce-s-a-schimbat-fata-de-versiunea-anterioara) de la final listează diferențele față de varianta veche a acestui document — citește-o dacă ai apucat să scrii cod pe ea.

---

## Model: Api (rezumat)

Returnat de `GET /apis`.

```json
{
  "id": 1,
  "name": "Petstore",
  "type": "rest",
  "baseUrl": "https://petstore3.swagger.io/api/v3",
  "healthStatus": "healthy",
  "lastLatencyMs": 277,
  "lastCheckedAt": "2026-08-17T20:31:25.529Z",
  "createdAt": "2026-08-16T18:04:11.201Z"
}
```

**Valori posibile pentru `healthStatus`** — mereu string, **niciodată `null`**:

| Valoare | Semnificație | Sugestie UI |
|---|---|---|
| `"healthy"` | A răspuns OK | verde |
| `"unhealthy"` | A răspuns, dar cu eroare | portocaliu |
| `"down"` | Nu a răspuns deloc / timeout / DNS eșuat | roșu |
| `"unknown"` | Încă neverificat (abia înregistrat) | gri |

Cum se decide `healthy` vs `unhealthy`:

- API-ul **are** `healthCheckUrl` → doar 2xx înseamnă `"healthy"`.
- API-ul **nu are** `healthCheckUrl` (se testează `baseUrl`) → orice status **sub 500** înseamnă `"healthy"`. Motiv: un `baseUrl` de tip `.../api/v3` întoarce de obicei 404 deși API-ul e perfect funcțional.

`lastLatencyMs` este `null` când statusul e `"down"`. Altfel, întreg, milisecunde.

`type` este momentan mereu `"rest"`.

Verificarea rulează automat la fiecare **15 secunde**.

---

## 1. Listează API-urile

```
GET /apis
```

**200 OK** — array de obiecte Api (vezi mai sus). Array gol dacă nu există niciunul.

Câmpul `schema` **nu** este inclus aici. Pentru el, folosește `GET /apis/{id}`.

---

## 2. Detalii despre un API

```
GET /apis/{id}
```

**200 OK** — obiectul complet:

```json
{
  "id": 1,
  "name": "Petstore",
  "type": "rest",
  "baseUrl": "https://petstore3.swagger.io/api/v3",
  "schemaUrl": "https://petstore3.swagger.io/api/v3/openapi.json",
  "healthCheckUrl": "https://petstore3.swagger.io/api/v3/openapi.json",
  "schema": "{\"openapi\":\"3.0.4\",\"info\":{...}}",
  "healthStatus": "healthy",
  "lastLatencyMs": 277,
  "lastCheckedAt": "2026-08-17T20:31:25.529Z",
  "createdAt": "2026-08-16T18:04:11.201Z"
}
```

> ⚠️ **`schema` este un string, nu un obiect.** Conține documentul OpenAPI serializat. Ca să lucrezi cu el:
> ```js
> const api = await res.json();
> const openapi = JSON.parse(api.schema);   // abia acum ai obiectul
> ```
> Dimensiunea tipică: 18–40 KB. `schemaUrl` și `healthCheckUrl` pot fi `null`.

**404 Not Found** — id inexistent. Corp: ProblemDetails.

---

## 3. Înregistrează un API REST

```
POST /apis/rest
```

**Body:**

```json
{
  "name": "Petstore",
  "baseUrl": "https://petstore3.swagger.io/api/v3",
  "schemaUrl": "https://petstore3.swagger.io/api/v3/openapi.json",
  "healthCheckUrl": "https://petstore3.swagger.io/api/v3/openapi.json"
}
```

| Câmp | Obligatoriu | Reguli de validare |
|---|---|---|
| `name` | da | 1–64 caractere, **doar** litere ASCII, cifre, `-` și `_`. Fără spații. Devine cheia de rutare proxy (lowercase) |
| `baseUrl` | da | URL absolut `http://` sau `https://` |
| `schemaUrl` | da | URL absolut `http(s)`, trebuie să întoarcă **JSON** (nu YAML) |
| `healthCheckUrl` | nu | URL absolut `http(s)` dacă e prezent. Fallback pe `baseUrl` |

**201 Created** — header `Location: /apis/{id}`. Corp:

```json
{
  "id": 3,
  "name": "Petstore",
  "type": "rest",
  "baseUrl": "https://petstore3.swagger.io/api/v3",
  "schemaUrl": "https://petstore3.swagger.io/api/v3/openapi.json",
  "healthCheckUrl": "https://petstore3.swagger.io/api/v3/openapi.json",
  "healthStatus": "unknown",
  "createdAt": "2026-08-17T21:57:11.267Z"
}
```

> ⚠️ Răspunsul de la 201 **nu include `schema`** (ar fi ~20 KB inutili la fiecare creare). Dacă ai nevoie de ea imediat, cheamă `GET /apis/{id}` după.

Se salvează automat versiunea 1 a schemei, iar ruta proxy devine imediat activă. Api-ul și versiunea 1 se scriu într-o singură tranzacție — nu poate exista un API fără versiunea 1.

**Erori:**

| Cod | Când | Corp |
|---|---|---|
| 400 | câmp obligatoriu lipsă | ProblemDetails cu `errors` |
| 400 | `name` invalid | `Name must be 1-64 characters of letters, digits, '-' or '_'.` |
| 400 | `baseUrl` / `schemaUrl` / `healthCheckUrl` nu e URL absolut http(s) | `baseUrl must be an absolute http(s) URL.` |
| 400 | schema descărcată nu e JSON valid | `Schema is not valid JSON. Only JSON OpenAPI documents are supported, not YAML.` |
| 409 | nume deja folosit (case-insensitive) | `An API named 'Petstore' already exists.` |
| 502 | `schemaUrl` inaccesibil (DNS, conexiune refuzată) | `Schema URL unreachable: ...` |
| 502 | `schemaUrl` a răspuns non-2xx | `Schema URL returned HTTP 404.` |
| 504 | `schemaUrl` n-a răspuns în 30s | `Schema URL timed out after 30s.` |

> Unicitatea numelui e garantată de un index unic pe `lower(name)` în baza de date, nu doar de o verificare în cod. Două cereri simultane cu același nume dau 201 + 409, niciodată două API-uri.

---

## 4. Șterge un API

```
DELETE /apis/{id}
```

**204 No Content** — șters. Versiunile de schemă și ruta proxy dispar odată cu el (cascade în DB).

**404 Not Found** — id inexistent. Corp: ProblemDetails.

---

## 5. Reîmprospătează schema

```
POST /apis/{id}/refresh
```

Fără body. Descarcă din nou schema de la `schemaUrl` și compară hash-ul SHA-256 cu ultima versiune salvată.

**200 OK**, două forme:

```json
{ "message": "New version saved.", "version": 2 }
```
```json
{ "message": "No changes.", "version": 1 }
```

**Erori:**

| Cod | Când | Corp |
|---|---|---|
| 404 | id inexistent | ProblemDetails |
| 400 | API-ul n-are `schemaUrl` | `API has no schema URL.` |
| 400 | schema descărcată nu e JSON valid | `Schema is not valid JSON. ...` |
| 502 / 504 | `schemaUrl` inaccesibil / timeout | ca la §3 |
| 409 | refresh-uri simultane pe același API | `Another refresh for this API is in progress, try again.` |

---

## 6. Listează versiunile de schemă

```
GET /apis/{id}/versions
```

**200 OK** — ordonate descrescător după `versionNumber`:

```json
[
  { "versionNumber": 2, "fetchedAt": "2026-08-17T19:12:03.441Z", "sizeBytes": 18310 },
  { "versionNumber": 1, "fetchedAt": "2026-08-16T18:04:11.318Z", "sizeBytes": 18286 }
]
```

Array gol dacă API-ul există dar n-are versiuni.

**404 Not Found** — id inexistent. Corp: `API 999 not found.` (text simplu)

> `sizeBytes` e lungimea documentului **normalizat de Postgres** (chei reordonate, spații eliminate), nu a fișierului original de pe `schemaUrl`. Folosește-l ca indicator relativ, nu ca dimensiune exactă a fișierului.

---

## 7. Compară două versiuni

```
GET /apis/{id}/diff?from=1&to=2
```

Ambii parametri sunt obligatorii și trebuie să fie numere întregi.

**200 OK:**

```json
{
  "from": 1,
  "to": 2,
  "added": ["GET /pet/{petId}", "POST /pet"],
  "removed": ["DELETE /store/order/{orderId}"],
  "unchanged": 17
}
```

`added` și `removed` sunt liste de string-uri în formatul `METODĂ /cale`.
`unchanged` e un **număr**, nu o listă.

**Erori:**

| Cod | Când | Corp |
|---|---|---|
| 400 | `from` sau `to` lipsește | `Query parameters 'from' and 'to' are required.` |
| 400 | `from` sau `to` nu e număr | ProblemDetails cu `errors` |
| 400 | schema salvată nu e un document OpenAPI valid | `Stored schema is not a valid OpenAPI document.` |
| 404 | id inexistent | `API 999 not found.` |
| 404 | una sau ambele versiuni lipsesc | `One or both versions not found.` |

> Comparația se face pe existența endpoint-urilor, nu pe conținutul lor. O modificare de parametri în cadrul aceluiași endpoint nu apare în diff.
>
> Se numără doar operațiile HTTP reale (`get`, `put`, `post`, `delete`, `options`, `head`, `patch`, `trace`). Cheile de nivel path din OpenAPI — `summary`, `description`, `parameters`, `servers`, `$ref` — sunt ignorate.

---

## 8. Întreabă asistentul AI

```
POST /apis/{id}/ask
```

**Body:**

```json
{ "question": "How do I find a pet by its ID?" }
```

**200 OK:**

```json
{
  "question": "How do I find a pet by its ID?",
  "answer": "You can use the GET /pet/{petId} endpoint to find a pet by its ID."
}
```

**Erori:**

| Cod | Când | Corp |
|---|---|---|
| 400 | `question` lipsă sau gol | ProblemDetails cu `errors` |
| 404 | id inexistent sau API fără schemă salvată | `API or schema not found.` |
| 503 | Ollama nu rulează / a răspuns cu eroare / timeout | `AI assistant unreachable at localhost:11434 ...` |

> **Important pentru UI:** primul apel poate dura 30–60 de secunde (se încarcă modelul în memorie), apoi 5–15 secunde. Pune un indicator de încărcare și un timeout generos (minim 2 minute — serverul renunță la 2 minute și întoarce 503).
>
> Necesită Ollama pornit local pe portul 11434, cu modelul `llama3.2`.

---

## 9. Proxy către API-urile înregistrate

```
ANY /proxy/{nume}/{cale}
```

`{nume}` e câmpul `name` al API-ului, cu litere mici. Potrivirea e case-insensitive, deci și `/proxy/Petstore/...` funcționează.

**Exemplu:** `GET /proxy/petstore/pet/1` → retransmis la
`https://petstore3.swagger.io/api/v3/pet/1`

Răspunsul este cel al API-ului destinație, transmis ca atare (status, headere, body).

**404** dacă numele nu corespunde niciunui API înregistrat.

**Rate limiting:** maxim **5 cereri la fiecare 10 secunde, per adresă IP client**, indiferent de câte API-uri diferite apelezi. Fereastră fixă.
Peste limită → **429 Too Many Requests**, **fără corp de răspuns și fără header `Retry-After`**. Așteaptă până la 10 secunde și reîncearcă.

Rutele de administrare (`/apis`, `/health`) nu sunt limitate.

---

## 10. Health check al gateway-ului

```
GET /health
```

**200 OK:**

```json
{ "status": "ok", "time": "2026-08-17T20:43:33.120Z" }
```

Verifică dacă backendul rulează. Nu are legătură cu starea API-urilor înregistrate.

---

## Note de integrare

### Cele două formate de eroare

Backendul întoarce erorile în **două formate diferite**. Tratează-le pe amândouă:

**a) ProblemDetails** (`application/problem+json`) — pentru 404-urile fără mesaj și pentru erorile de validare a modelului:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "traceId": "00-3b0b..."
}
```

Validarea modelului adaugă și `errors`:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Name": ["The Name field is required."] }
}
```

**b) Text simplu** (`text/plain`) — pentru toate mesajele custom (409, 502, 504, 503 și majoritatea 400/404-urilor cu explicație).

Helper recomandat:

```js
async function readError(res) {
  const type = res.headers.get('content-type') || '';
  if (type.includes('json')) {
    const p = await res.json();
    if (p.errors) return Object.values(p.errors).flat().join(' ');
    return p.title || `HTTP ${res.status}`;
  }
  return (await res.text()) || `HTTP ${res.status}`;
}
```

### Recapitulare coduri

| Cod | Înseamnă | Ce afișezi |
|---|---|---|
| 400 | cerere invalidă sau schemă neparsabilă | mesajul din corp, lângă câmpul greșit |
| 404 | resursă inexistentă | „nu există" |
| 409 | nume duplicat / refresh concurent | mesajul din corp |
| 429 | rate limit proxy depășit | „prea multe cereri, reîncearcă în 10s" |
| 502 | `schemaUrl` inaccesibil sau non-2xx | mesajul din corp |
| 503 | Ollama indisponibil | „asistentul AI nu e pornit" |
| 504 | `schemaUrl` timeout la 30s | „sursa schemei nu răspunde" |

**Nu mai există 500 pe căile normale.** Dacă primești 500, e un bug — trimite `traceId`-ul.

### Altele

**CORS** e activat pentru orice origine, metodă și header. Fără credentials (`AllowAnyOrigin` exclude cookies).

**Autentificare:** nu există. Orice client poate înregistra și șterge API-uri.

**Formatul datelor** e ISO 8601 UTC (`2026-08-17T20:31:25.529Z`). În JavaScript: `new Date(str)`.

**Durate:** `POST /apis/rest` și `POST /apis/{id}/refresh` pot dura până la 30 de secunde (descărcarea schemei). `POST /apis/{id}/ask` până la 2 minute. Setează timeout-uri per-endpoint, nu unul global de 5 secunde.

**Portul** poate diferi — verifică `Properties/launchSettings.json` sau output-ul de la `dotnet run`. Rulează cu profilul `http` (`dotnet run --launch-profile http`); pe profilul `https` toate cererile HTTP primesc redirect 307.

---

## Ce s-a schimbat față de versiunea anterioară

Dacă ai scris deja cod pe varianta veche a acestui document, astea sunt diferențele care te afectează:

| Zonă | Înainte | Acum |
|---|---|---|
| `healthStatus` | putea fi `null` | mereu string; neverificat = `"unknown"` |
| `healthStatus` fără `healthCheckUrl` | 4xx → `"unhealthy"` | orice sub 500 → `"healthy"` |
| `GET /apis/{id}/versions`, id inexistent | `200 []` | `404` |
| `POST /apis/rest`, răspuns 201 | includea `schema` | fără `schema` |
| `POST /apis/rest`, schemaUrl mort sau non-JSON | `500` | `400` / `502` / `504` |
| `POST /apis/{id}/ask`, Ollama oprit | `500` | `503` |
| `GET /apis/{id}/diff` fără `from`/`to` | `404` | `400` |
| `GET /apis/{id}/diff` | lista putea conține intrări false (`SUMMARY /a`, `PARAMETERS /a`) | doar operații HTTP reale |
| Rate limit proxy | un singur contor global | per adresă IP client (tot 5 / 10s) |
| `name` la înregistrare | orice string | 1–64 caractere, litere/cifre/`-`/`_` |
