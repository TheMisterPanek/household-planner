## ADDED Requirements

### Requirement: Item rows expose no edit control
The system SHALL render each `/list` item row with exactly two buttons — `[✓ Name qty]` (mark bought) and `[✗ Убрать]` (remove) — and SHALL NOT include any in-place rename/requantify control. Changing an item's name or quantity requires removing it and re-adding it via `/buy`.

#### Scenario: Item row has exactly two buttons
- **WHEN** a group member sends `/list`
- **THEN** each item row contains exactly two inline buttons: `[✓ Name qty]` and `[✗ Убрать]`, with no third "edit" button

#### Scenario: No callback exists for in-place item editing
- **WHEN** any callback data matching a legacy `item:edit:*` pattern is received (e.g. from a stale cached keyboard on an old list message)
- **THEN** `UpdateDispatcher` finds no matching `ICallbackHandler` prefix, logs it at Debug level, and takes no further action — no dialog opens, no error is shown to the user
