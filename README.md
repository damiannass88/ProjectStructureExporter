# Project Structure Exporter

A lightweight Windows WPF app that scans a .NET project directory and produces a compact, readable text snapshot designed for use as an AI prompt. It captures the directory tree and file contents, with an option to strip C# method bodies to reduce tokens while keeping useful API signatures (interfaces, classes, records, properties, and method signatures).

## Why
- Share a project’s structure and code context with an AI without uploading a repository.
- Reduce token usage by removing implementation details and keeping only contracts and signatures.
- Preserve what matters for reasoning: folders, project files, interfaces, public API surface.

## Key Features
- Folder selection with history: quickly revisit previously scanned directories.
- Two scan modes:
 - Full: directory tree + full file contents.
 - NoBodies: for `.cs` files, removes method/constructor/destructor/operator bodies and converts properties/indexers to semicolon accessors, keeping interfaces and signatures intact (default interface methods become declarations).
- Save or copy: export to `.txt` or copy to clipboard.
- Smart filtering:
 - Includes: `.cs`, `.json`, `.razor`, `.csproj`, `.sln`, `.xml`, `.config`, `.yml`, `.yaml`
 - Excludes directories: `bin`, `obj`, `.git`, `.vs`, `node_modules`, `.idea`
 - Skips generated C# files: `*.g.cs`, `*.g.i.cs`, `*.designer.cs`
- Scalable UI: virtualized output list for large projects.
- Safe I/O: handles locked/missing/unauthorized files and folders gracefully.

## How It Works
- Builds a directory tree and iterates allowed files.
- For `.cs` files in NoBodies mode, uses Roslyn to rewrite syntax trees to signatures-only:
 - Methods/constructors/destructors/operators → declarations with semicolons (no bodies)
 - Properties/indexers → `get; set;` accessors with no bodies
 - Default interface methods → signatures only
- Outputs a single text stream suitable for AI prompts.

## UI Guide (Toolbar)
- "Choose Folder": opens a folder picker. The selected path is stored in history.
- "Scan": full scan (directory tree + full contents of included files).
- "Scan NoBodies": compact scan for AI; `.cs` files are rewritten to signatures-only.
- "Save TXT": saves the current output to a `.txt` file and also copies it to clipboard.
- "Copy": copies the current output to clipboard.

### Path History Combo (editable)
- Dropdown with previously scanned paths.
- Editable; paste or type a new path.
- Most recent paths appear at the top.

### Output Area
- Virtualized list of lines for performance on large outputs.
- Shows the directory tree section followed by per-file content sections.

## File Selection and Filtering
- Allowed extensions: `.cs`, `.json`, `.razor`, `.csproj`, `.sln`, `.xml`, `.config`, `.yml`, `.yaml`
- Excluded directories: `bin`, `obj`, `.git`, `.vs`, `node_modules`, `.idea`
- Skipped generated C# files: `*.g.cs`, `*.g.i.cs`, `*.designer.cs`

## Usage
1. Click "Choose Folder" and select the root of your project/repo.
2. Click "Scan" for a full snapshot, or "Scan NoBodies" for compact output.
3. Click "Save TXT" to export and copy, or "Kopie" to copy directly.
4. Paste the output into your AI prompt.

## Good For
- Supplying an AI assistant with the essential shape of a codebase (API surface, interfaces, contracts, project files) without leaking implementation.
- Quickly summarizing repos or subprojects during refactors, reviews, planning, or bug triage.

## Requirements
- Windows
- .NET9
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

## License
Open source under the MIT License. See `LICENSE` for details.