# Technische Aufgabe: Skalierbarer BackgroundService zur Auftragsverarbeitung

## Ziel

Entwickle einen .NET 10 `BackgroundService`, der offene Aufträge aus
einer PostgreSQL-Datenbank lädt, intern queued und parallel verarbeitet.

Der Service muss auch bei mehreren gleichzeitig laufenden Instanzen
sicherstellen, dass kein Auftrag doppelt verarbeitet wird.

---

## Anforderungen

### Aufträge abrufen

- Neue Aufträge haben den Status `Open`
- Abruf in konfigurierbaren Intervallen
- Anzahl gefundener Aufträge protokollieren

### Mehrere Service-Instanzen

- Mehrere Instanzen können parallel laufen
- Ein Auftrag darf nur von einer Instanz übernommen werden
- PostgreSQL-Synchronisation mittels `FOR UPDATE SKIP LOCKED`
- Statuswechsel `Open -> InProgress` muss atomar erfolgen

### Interne Queue

- Thread-sicher
- Konfigurierbare maximale Größe
- Keine Duplikate innerhalb der Queue

### Parallele Verarbeitung

- Konfigurierbare Anzahl paralleler Worker
- Begrenzung mittels `SemaphoreSlim`

```json
{
  "MaxParallelJobs": 5
}
```

### Verarbeitung

- Die Verarbeitung erfolgt über die bereitgestellte externe Mock API
- Pro Auftrag müssen beide API-Gruppen verwendet werden:
  - Service A (`/service-a/1`, `/service-a/2`, `/service-a/3`)
  - Service B (`/service-b/1`, `/service-b/2`)
- Jeder Auftrag gilt erst dann als erfolgreich verarbeitet, wenn alle
  API-Aufrufe erfolgreich abgeschlossen wurden
- Unterstützung von `CancellationToken`
- Bei Service-Stopp sollen bereits laufende Prozesse kontrolliert
  beendet werden
- Fehlerbehandlung

### Timeout

```json
{
  "JobTimeoutMinutes": 5
}
```

Wenn ein Auftrag länger als 5 Minuten läuft:

```text
InProgress -> Timeout
```

Dabei müssen:

- Verarbeitung abgebrochen werden
- Timeout-Zeitpunkt gespeichert werden
- Vorfall protokolliert werden
- Semaphore zuverlässig freigegeben werden

### Erfolgreicher Abschluss

```text
InProgress -> Completed
```

Zusätzlich speichern:

- Abschlusszeitpunkt

---

## Logging

Verwendung von:

```csharp
ILogger<T>
```

Mindestens zu protokollieren:

- Service gestartet/beendet
- Aufträge gefunden
- Auftrag übernommen
- Auftrag übersprungen
- Auftrag in Bearbeitung
- Auftrag abgeschlossen
- Auftrag mit Timeout beendet
- Fehler
- Queue-Größe
- Anzahl aktiver Worker

---

## Technische Vorgaben

- .NET 10
- BackgroundService
- PostgreSQL
- Npgsql
- Möglichst nur Microsoft-Bibliotheken verwenden
- Keine ORM-Technologien (kein EF Core, kein Dapper)
- Repository Pattern
- Dependency Injection
- SemaphoreSlim
- Thread-sichere Queue
- `appsettings.json`
- CancellationToken-Unterstützung
- Strukturierte Logs
- SOLID-Prinzipien
