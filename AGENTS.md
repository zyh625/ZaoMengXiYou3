# Project file preservation

- Preserve the user's uncommitted changes and newly generated art. Never reset, clean, or replace the whole project to resolve a task-local problem.
- Do not create junctions or symbolic links from validation projects to this project's `Assets`, `Packages`, `ProjectSettings`, or `Library`. Use independent physical copies.
- Do not place a nested Unity validation project under this project's `Temp`; Unity owns and clears that directory.
- Before any recursive cleanup or move, verify the absolute target and inspect reparse points. Never traverse a reparse point into production files. Remove only a verified link entry when detaching it.
- Limit changes to the task's files. Preserve alternative versions outside `Assets` when merging concurrent edits.
- Recovery evidence and the independent backup location are documented in `Docs/项目文件恢复报告-20261103.md`.
