# CLAUDE.md

Guida per Claude Code (claude.ai/code) per lavorare con questo repository.

## Cos'è IssuePad

IssuePad è un'applicazione Windows Forms .NET 9 per la gestione di issue/task locali. Salva i dati in un file JSONL locale, senza bisogno di database esterni.

## Quick Start

**Workflow consigliato:**

```bash
# 1. Prima verifica che tutto funzioni
dotnet run

# 2. Se funziona, compila in release
dotnet build -c Release

# 3. Per creare l'exe distribuibile (chiudi l'app prima!)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

> **Nota:** Se il publish fallisce con "file in use", chiudi IssuePad o cancella la cartella `publish` prima di riprovare.

## Struttura del progetto

```
IssuePad/
├── Program.cs                  # Entry point e gestione eccezioni globali
├── Models/
│   ├── IssueRow.cs             # Record immutabile per rappresentare un issue
│   └── DbLine.cs               # DTO per serializzazione eventi JSON
├── Services/
│   └── IssueRepository.cs      # Logica di persistenza (load/save)
└── Forms/
    ├── MainForm.cs             # Campi, costruttore, event handlers
    ├── MainForm.Setup.cs       # Setup UI (griglia, pannelli, status strip)
    ├── MainForm.Actions.cs     # Azioni CRUD (Add, Update, Delete, Toggle)
    └── MainForm.Data.cs        # Filtri, reload dati, binding
```

## Componenti principali

### Program.cs
Entry point dell'applicazione. Configura handler globale per eccezioni non gestite con log in `%LOCALAPPDATA%/IssuePad/crash.log`.

### Models
- **IssueRow.cs**: Record immutabile che rappresenta lo stato corrente di un issue (Id, Title, Description, Notes, Done, CreatedUtc)
- **DbLine.cs**: DTO per eventi JSON (`add`, `update`, `resolve`, `reopen`, `delete`)

### Services
- **IssueRepository.cs**: Gestisce la persistenza dati, caricamento e salvataggio eventi su file JSONL

### Forms/MainForm (partial class)
Il form principale è diviso in 4 file per responsabilità:

| File | Responsabilità |
|------|----------------|
| `MainForm.cs` | Campi UI, costruttore, registrazione event handlers |
| `MainForm.Setup.cs` | Setup colonne griglia, pannelli sinistro/destro, status strip |
| `MainForm.Actions.cs` | Logica CRUD: AddIssue, UpdateSelectedIssue, DeleteSelectedIssue, ToggleDone |
| `MainForm.Data.cs` | Caricamento dati, gestione filtri, aggiornamento contatori |

**Layout UI:**
- **Pannello sinistro:** Area filtri (GroupBox con ComboBox + checkbox "Solo aperte") + DataGridView
- **Pannello destro:** Editor con campi titolo, descrizione, note e pulsanti azione (Aggiungi/Salva affiancati)
- **StatusStrip:** Contatori globali (Totale, Aperte, Chiuse)

## Persistenza dati

I dati vengono salvati in `%LOCALAPPDATA%/IssuePad/issues.jsonl` come append-only event log.

**Eventi supportati:**
- `add` - Crea nuovo issue
- `update` - Modifica titolo/descrizione/note
- `resolve` - Marca come completato
- `reopen` - Riapre issue completato
- `delete` - Elimina issue

Il sistema ricostruisce lo stato corrente leggendo sequenzialmente tutti gli eventi dal file all'avvio.

## File di sistema

| Percorso | Descrizione |
|----------|-------------|
| `%LOCALAPPDATA%/IssuePad/issues.jsonl` | Database issue (JSONL) |
| `%LOCALAPPDATA%/IssuePad/crash.log` | Log errori non gestiti |
