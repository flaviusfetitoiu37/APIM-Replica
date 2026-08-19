# Fixtures

Răspunsuri **reale**, capturate din backendul pornit pe `http://localhost:5230` la 18.08.2026.
Nu sunt scrise de mână — sunt exact ce întoarce API-ul, formatate cu indentare.

Folosește-le ca mock-uri ca să poți lucra la frontend fără backendul pornit.

## Răspunsuri de succes

| Fișier | Endpoint | Ce conține |
|---|---|---|
| `apis-list.json` | `GET /apis` | 4 API-uri, acoperă `healthy`, `unhealthy`, `down` |
| `api-detail.json` | `GET /apis/16` | obiectul complet, cu `schema` ca string escapat |
| `api-create-201.json` | `POST /apis/rest` → 201 | răspunsul de creare, cu `healthStatus: "unknown"` |
| `versions.json` | `GET /apis/16/versions` | două versiuni, ordonate descrescător |
| `diff.json` | `GET /apis/16/diff?from=1&to=2` | 2 adăugate, 1 șters, 4 neschimbate |
| `ask.json` | `POST /apis/16/ask` | răspuns real de la llama3.2 |

`healthStatus: "unknown"` apare doar în `api-create-201.json` — e starea unui API abia înregistrat, până la primul ciclu de verificare (15 secunde).

## Răspunsuri de eroare

Extensia spune formatul: `.json` = `application/problem+json`, `.txt` = `text/plain`.
Ambele formate apar în practică — vezi helperul `readError()` din `API_CONTRACT.md`.

| Fișier | Cod | Situația |
|---|---|---|
| `errors/404-problem-details.json` | 404 | `GET /apis/999` — 404 fără mesaj |
| `errors/404-versions-text.txt` | 404 | `GET /apis/999/versions` — 404 cu mesaj |
| `errors/404-ask-text.txt` | 404 | `POST /apis/999/ask` |
| `errors/400-validation-problem-details.json` | 400 | `?from=abc` — tip greșit de parametru |
| `errors/400-create-required-fields.json` | 400 | `POST /apis/rest` cu body `{}` |
| `errors/400-create-invalid-name.txt` | 400 | `name` cu spațiu |
| `errors/400-diff-missing-params.txt` | 400 | diff fără `from`/`to` |
| `errors/400-schema-not-json.txt` | 400 | `schemaUrl` întoarce HTML/YAML |
| `errors/409-duplicate-name.txt` | 409 | nume deja folosit |
| `errors/502-schema-unreachable.txt` | 502 | `schemaUrl` cu host inexistent |

Lipsesc `503` (Ollama oprit), `504` (timeout la 30s) și `429` (rate limit) — greu de capturat determinist.
`429` oricum vine cu **corp gol**, deci n-are ce fixture să aibă.

## Atenție

Id-urile din fixtures (1, 16, 17, 18) nu mai există în baza de date — API-urile de test au fost șterse după captură. Folosește-le ca date statice, nu ca id-uri reale.
