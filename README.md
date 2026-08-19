# APIM Replica

Registru de API-uri externe care le monitorizează și le proxează. Gateway construit în .NET 10 cu YARP, PostgreSQL și EF Core.

Ce face:

1. Înregistrezi un API REST, dându-i URL-ul definiției OpenAPI
2. Schema se descarcă și se salvează versionat (hash SHA-256 pentru deduplicare)
3. YARP creează dinamic rute `/proxy/{nume}/**` care retransmit la API-ul real
4. Un `BackgroundService` verifică sănătatea fiecărui API la 15 secunde
5. Un asistent AI local (Ollama, llama3.2) răspunde la întrebări despre schemele salvate

**Contractul API complet: [`API_CONTRACT.md`](API_CONTRACT.md).** Exemple de răspunsuri reale: [`fixtures/`](fixtures/).

---

## Ce îți trebuie

| Componentă | Versiune testată | Obligatoriu |
|---|---|---|
| .NET SDK | 10.0.302 | da |
| PostgreSQL | 18.4 | da |
| `dotnet-ef` | 10.0.10 | da, pentru migrări |
| Ollama + `llama3.2` | — | nu, doar pentru `POST /apis/{id}/ask` |

Instalează unealta EF dacă n-o ai:

```bash
dotnet tool install --global dotnet-ef
```

---

## Pornire

### 1. Baza de date

```bash
createdb apim_replica
```

Verifică apoi connection string-ul din `appsettings.json` și pune-ți userul și parola ta:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=apim_replica;Username=USER;Password=PAROLA"
}
```

### 2. Migrări

```bash
dotnet ef database update
```

Creează tabelele `Apis` și `SchemaVersions`, plus indecșii unici pe `lower("Name")` și pe `("ApiId","VersionNumber")`.

### 3. Rulare

```bash
dotnet run --launch-profile http
```

Backendul ascultă pe **`http://localhost:5230`**.

> Folosește profilul `http`. Pe profilul `https`, toate cererile HTTP primesc redirect 307, ceea ce încurcă `curl` și clienții de frontend configurați pe portul 5230.

Verificare rapidă:

```bash
curl http://localhost:5230/health
```

Răspuns așteptat: `{"status":"ok","time":"..."}`

---

## Asistentul AI (opțional)

`POST /apis/{id}/ask` are nevoie de Ollama pornit local pe portul 11434, cu modelul `llama3.2` (~2 GB la prima descărcare).

```bash
ollama pull llama3.2
```

```bash
ollama serve
```

Fără el, endpoint-ul întoarce **503** cu mesaj explicit — restul aplicației funcționează normal.

Primul apel durează 30–60 de secunde (se încarcă modelul în memorie), apoi 5–15 secunde.

---

## Alerte pe email (opțional)

`Services/EmailService.cs` trimite un email la fiecare tranziție a unui API spre starea `down`.

E **inactiv** cât timp `Smtp:Password` e gol — în locul trimiterii se scrie o linie de log:

```
warn: SMTP not configured. Would send: [APIM] X is DOWN
```

Ca să-l activezi, completează secțiunea `Smtp` din `appsettings.json` și pune parola în user-secrets (nu în fișier):

```bash
dotnet user-secrets set "Smtp:Password" "app-password-de-16-caractere"
```

Pentru Gmail trebuie un **app password**, nu parola contului.

Ca să-l dezactivezi la loc:

```bash
dotnet user-secrets remove "Smtp:Password"
```

> Dacă `Smtp:Password` e setat dar invalid, fiecare tranziție spre `down` încearcă o conexiune SMTP reală care eșuează și încetinește ciclul de verificare. Eroarea e prinsă și logată, nu oprește aplicația — dar pentru o demonstrație curată e mai bine să nu ai parola setată deloc.

---

## Structura proiectului

```
Program.cs                     config: DbContext, YARP, rate limiting, CORS, hosted service
Models/Api.cs                  API înregistrat + câmpuri de health
Models/ApiSchemaVersion.cs     versiune de schemă, many-to-one cu Api
Data/AppDbContext.cs           EF Core context + indecși unici
Controllers/ApisController.cs  toate endpoint-urile
Services/ProxyConfigService.cs construiește rutele YARP din baza de date
Services/HealthCheckService.cs BackgroundService de monitorizare (15s)
Services/EmailService.cs       MailKit, alertă la tranziție spre "down"
Services/AiService.cs          client Ollama (llama3.2, localhost:11434)
API_CONTRACT.md                contractul API — sursa adevărului pentru frontend
fixtures/                      răspunsuri reale capturate, pentru mock-uri
ApimReplica.http               cereri gata de rulat, inclusiv cazuri de eroare
```

---

## Endpoint-uri

| Metodă | Rută | Ce face |
|---|---|---|
| `GET` | `/health` | starea gateway-ului |
| `GET` | `/apis` | listă, fără câmpul `schema` |
| `GET` | `/apis/{id}` | detalii complete |
| `POST` | `/apis/rest` | înregistrare |
| `DELETE` | `/apis/{id}` | ștergere, cu tot cu versiuni și rută |
| `POST` | `/apis/{id}/refresh` | redescarcă schema, versionează dacă s-a schimbat |
| `GET` | `/apis/{id}/versions` | istoricul versiunilor |
| `GET` | `/apis/{id}/diff?from=&to=` | endpoint-uri adăugate/șterse între două versiuni |
| `POST` | `/apis/{id}/ask` | întrebare în limbaj natural despre schemă |
| `ANY` | `/proxy/{nume}/**` | reverse proxy, 5 cereri / 10 secunde per IP |

Codurile de răspuns și corpurile exacte sunt în [`API_CONTRACT.md`](API_CONTRACT.md).

---

## Probleme frecvente

**`dotnet ef` nu e recunoscut** — instalează unealta globală (vezi mai sus) și redeschide terminalul.

**`57P03: the database system is starting up` sau conexiune refuzată** — Postgres nu rulează. Pe macOS cu Homebrew: `brew services start postgresql@18`.

**Toate cererile primesc 307** — rulezi pe profilul `https`. Repornește cu `--launch-profile http`.

**`/apis/{id}/ask` întoarce 503** — Ollama nu rulează sau modelul nu e descărcat.

**Un API apare `unhealthy` deși funcționează** — probabil are `healthCheckUrl` setat spre un endpoint care întoarce 4xx. Fără `healthCheckUrl`, se verifică `baseUrl` și orice status sub 500 e considerat sănătos.
