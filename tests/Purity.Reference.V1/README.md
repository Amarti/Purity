# Frozen reference engine

`Source/` is an exact copy of `src/Purity` at tag `engine-v1-reference` (commit `e4af0e8`), the engine in production when the port began. Only the project file is new.

Do not edit anything under `Source/`, even to fix a bug. Every port is measured against this code, and `ReferenceIntegrityTests` fails if a single byte changes. Rule changes belong in the new engine, with a rules-version bump and a reviewed list of changed dates.
