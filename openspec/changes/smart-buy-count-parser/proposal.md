# Proposal: Smart Count Parser for /buy Input

## What & Why

When users type `/buy туалетка 8 шту`, the bot currently parses `8 шт` as the quantity — using the abbreviated form and no count indicator. The display reads `туалетка (8 шт)`, which feels terse and doesn't visually distinguish count-style quantities from volume/weight.

More importantly, the parser doesn't recognise partial unit words like `шту` (a common typo/shorthand for `штук`), silently falling back to a bare number. Users who type `шту` or `штук` or `pcs` all mean "N pieces" — the bot should understand and normalise all of these to the same display form, and should mark count quantities with an `×` prefix so they scan distinctly in a list.

This change updates `BuyInputParser` to:
1. Recognise trailing count patterns (bare integers, or integers followed by `шт`/`шту`/`штук`/`pcs`/`pieces`/`pack`/`packs`) and normalise the unit to the full canonical form (`штук` for Russian count, `pcs` for English).
2. Format the resulting quantity string with an `×` prefix for count-type quantities: `×8 штук` instead of `8 шт`.
3. Leave non-count units (л, кг, г, мл, kg, g, ml, l) unchanged (no prefix, no canonical change).

**Example:**
- Input: `туалетка 8 шту`
- Before: name=`туалетка`, qty=`8 шт`
- After:  name=`туалетка`, qty=`×8 штук`

The review message already has a `×{quantity}` template. The `×` prefix in the quantity string itself means count quantities show as `туалетка ×8 штук` in the list — a clear, at-a-glance signal.

## Proposed Solution

### Change: `BuyInputParser.Parse`

Update `NormalizeUnit` (and the qty formatting path) so that when the detected unit is a count-type (`шт`, `шту`, `штук`, `штуку`, `шт.`, `pcs`, `piece`, `pieces`, `pack`, `packs`, or bare integer), the returned quantity string is `×{number} {canonical-unit}` (or `×{number}` for a bare integer).

Non-count units (л, кг, г, мл, etc.) remain formatted as `{number} {unit}` — unchanged behaviour.

The `×` character (U+00D7, MULTIPLICATION SIGN) is already used in review templates (`×{quantity}`), so count quantities will read `туалетка ×8 штук` in review/confirm messages (the template already provides one × for non-count; for count the `×` comes from the qty string — see **Design** for the clean approach).

### Canonical units (output)

| Input variants | Output |
|---|---|
| `шт`, `шт.`, `шту`, `шту.`, `штук`, `штуку` | `штук` |
| `pcs`, `piece`, `pieces` | `pcs` |
| `pack`, `packs` | `packs` |
| bare integer | *(no unit)* |

## Affected Components

| Component | Change |
|---|---|
| `BuyInputParser` | Update `NormalizeUnit` + qty formatter; prepend `×` for count types |
| `BuyInputParserTests` | Update existing tests to new canonical output; add new cases for `шту`, bare count `×` prefix |

No localization keys, no database schema, no new handlers.

## Rollback Plan

Revert `BuyInputParser.cs` and `BuyInputParserTests.cs` to the previous commit. No migrations or DI changes.

## Affected Teams

Single-user household bot. No shared infrastructure.

## Cross-Cutting Notes

- Purely a parsing/display change. No DB writes, no new DI registrations, no Telegram callback changes.
- AOT-safe: `GeneratedRegex` attribute retained; no new reflection.
- The review template `buy.review-add-qty` currently reads `Add: {item} ×{quantity}`. When `quantity` is `×8 штук`, the review would read `Add: туалетка ××8 штук`. The design section describes how to avoid the double `×`.
