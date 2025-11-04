# Project Structure Exporter

A lightweight Windows WPF app that scans a .NET project directory and produces a compact, readable text snapshot designed for use as an AI prompt. It captures the directory tree and file contents, with an option to strip C# method bodies to reduce tokens while keeping useful API signatures (interfaces, classes, records, properties, and method signatures).

<img width="1358" height="866" alt="image" src="https://github.com/user-attachments/assets/dcf24aee-64a3-4da0-a49e-449c7a25ba0e" />


## Why
- Share a project’s structure and code context with an AI without uploading a repository.
- Reduce token usage by removing implementation details and keeping only contracts and signatures.
- Preserve what matters for reasoning: folders, project files, interfaces, public API surface.

## Key Features
- Folder selection with history: quickly revisit previously scanned directories.
- Two scan modes:
 - Full: directory tree + full file contents.
 - NoBodies: for `.cs` files, removes method/constructor/destructor/operator bodies and converts properties/indexers to semicolon accessors, keeping interfaces and signatures intact (default interface methods become declarations).
- Editable output area with standard text editing:
 - Select, type, cut/copy/paste, delete, undo/redo, select-all.
 - Right-click context menu and standard shortcuts (Ctrl+Z/Y, Ctrl+X/C/V, Ctrl+A).
 - You can modify or paste additional text before saving or copying.
- Save or copy: export to `.txt` or copy to clipboard in one click.
- Smart filtering:
 - Includes: `.cs`, `.json`, `.razor`, `.csproj`, `.sln`, `.xml`, `.config`, `.yml`, `.yaml`
 - Excludes directories: `bin`, `obj`, `.git`, `.vs`, `node_modules`, `.idea`
 - Skips generated C# files: `*.g.cs`, `*.g.i.cs`, `*.designer.cs`
- Scalable UI: virtualized-friendly flow via a single text buffer.
- Safe I/O: handles locked/missing/unauthorized files and folders gracefully.

## How It Works
- Builds a directory tree and iterates allowed files.
- For `.cs` files in NoBodies mode, uses Roslyn to rewrite syntax trees to signatures-only:
 - Methods/constructors/destructors/operators → declarations with semicolons (no bodies)
 - Properties/indexers → `get; set;` accessors with no bodies
 - Default interface methods → signatures only
- Outputs a single editable text stream suitable for AI prompts.

## UI Guide (Toolbar)
- "Choose Folder": opens a folder picker. The selected path is stored in history.
- "Scan": full scan (directory tree + full contents of included files).
- "Scan NoBodies": compact scan for AI; `.cs` files are rewritten to signatures-only.
- "Save TXT": saves the current text (including your edits) to a `.txt` file and also copies it to clipboard.
- "Copy": copies the current text (including your edits) to clipboard.

### Path History Combo (editable)
- Dropdown with previously scanned paths.
- Editable; paste or type a new path.
- Most recent paths appear at the top.

### Output Area (editable)
- A single editable `TextBox` containing the full output.
- Supports selection, typing, cut/copy/paste, delete, undo/redo, select-all via context menu and keyboard shortcuts.

## File Selection and Filtering
- Allowed extensions: `.cs`, `.json`, `.razor`, `.csproj`, `.sln`, `.xml`, `.config`, `.yml`, `.yaml`
- Excluded directories: `bin`, `obj`, `.git`, `.vs`, `node_modules`, `.idea`
- Skipped generated C# files: `*.g.cs`, `*.g.i.cs`, `*.designer.cs`

## Usage
1. Click "Choose Folder" and select the root of your project/repo.
2. Click "Scan" for a full snapshot, or "Scan NoBodies" for compact output.
3. Optionally edit the text (add notes, prune sections, paste snippets).
4. Click "Save TXT" to export and copy, or "Copy" to copy directly.
5. Paste the output into your AI prompt.

## Good For
- Supplying an AI assistant with the essential shape of a codebase (API surface, interfaces, contracts, project files) without leaking implementation.
- Quickly summarizing repos or subprojects during refactors, reviews, planning, or bug triage.

## Requirements
- Windows
- .NET 9
- WPF

## Build
- Open the solution in Visual Studio (or build with `dotnet build`).
- If rebuilding while the app is running, the executable may be locked. Close the running instance or stop debugging before building again.

## Privacy & Security
- All scanning is local. No files are uploaded.
- Output is plain text for easy review and redaction before sharing.

## Roadmap Ideas
- Public-API-only mode (filter to `public` members).
- Interfaces-only mode.
- Configurable include/exclude patterns per file extension and folder.
- Optional size limits per file.

<img width="1358" height="866" alt="image" src="https://github.com/user-attachments/assets/6e9515df-2de4-42f7-9bee-e499066e5cd0" />

## License
Open source under the MIT License. See `LICENSE` for details.
