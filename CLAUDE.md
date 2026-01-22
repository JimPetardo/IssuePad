# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build (debug)
dotnet build

# Build (release)
dotnet build -c Release

# Run
dotnet run

# Publish single-file exe
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Architecture

IssuePad è un'applicazione Windows Forms .NET 9 per la gestione di issue/task locali.

### Struttura del progetto

```
IssuePad/
├── Program.cs              # Entry point e gestione eccezioni globali
├── Models/
│   ├── IssueRow.cs         # Record immutabile per rappresentare un issue
│   └── DbLine.cs           # DTO per serializzazione eventi JSON
├── Services/
│   └── IssueRepository.cs  # Logica di persistenza (load/save)
└── Forms/
    └── MainForm.cs         # Form principale (UI)
```

### Componenti principali

- **Program.cs**: Entry point, configura handler globale per eccezioni non gestite (log in `%LOCALAPPDATA%/IssuePad/crash.log`)
- **Models/IssueRow.cs**: Record immutabile che rappresenta lo stato corrente di un issue
- **Models/DbLine.cs**: DTO per eventi JSON (`add`, `resolve`, `reopen`)
- **Services/IssueRepository.cs**: Gestisce la persistenza dati, caricamento e salvataggio eventi
- **Forms/MainForm.cs**: Form principale con layout a due pannelli (SplitContainer):
  - Pannello sinistro: DataGridView con lista issue (checkbox completamento, titolo, descrizione)
  - Pannello destro: Editor con campi titolo, descrizione, note e pulsanti azione

### Persistenza dati

Il database è un file JSONL (append-only event log) in `%LOCALAPPDATA%/IssuePad/issues.jsonl`:
- Eventi supportati: `add`, `resolve`, `reopen`
- Il sistema ricostruisce lo stato corrente leggendo sequenzialmente tutti gli eventi dal file
