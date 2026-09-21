# Mock API

Die Mock API simuliert externe Verarbeitungssysteme für die Auftragsverarbeitung.

**Base URL**

```text
https://mockapi-ms5j.onrender.com/health
```

## Authentifizierung

Alle **POST**-Endpunkte benötigen folgende Header:

```text
X-Api-Key: 56ef6b6aa58231ac727d5de50b912387
Content-Type: application/json
```

## Endpunkte

| Endpoint       | Methode | Bearbeitungszeit |
| -------------- | ------- | ---------------: |
| `/health`      | GET     |           sofort |
| `/service-a/1` | POST    |      30 Sekunden |
| `/service-a/2` | POST    |      40 Sekunden |
| `/service-a/3` | POST    |      50 Sekunden |
| `/service-b/1` | POST    |      45 Sekunden |
| `/service-b/2` | POST    |      60 Sekunden |

## Request

Alle POST-Endpunkte erwarten folgenden JSON-Body:

```json
{
  "id": "7c4c3a59-5c3f-4d74-b8c8-c2d14f4fbec0",
  "createdAt": "2026-07-09T12:00:00Z",
  "status": "InProgress"
}
```

## Response

Bei erfolgreicher Verarbeitung wird **HTTP 200 OK** zurückgegeben.

Bei einem ungültigen oder fehlenden API-Key wird **HTTP 401 Unauthorized** zurückgegeben.
