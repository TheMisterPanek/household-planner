# Design: Smart Count Parser for /buy Input

## Overview

All changes are confined to `BuyInputParser` (and its tests). No new types, no DI, no DB, no localization keys.

## Parsing Flow

```
User text: "туалетка 8 шту"
           │
           ▼
     QtyPattern regex
           │
     name="туалетка"   qtyRaw="8 шту"
           │
           ▼
     NormalizeQty("8 шту")
      ├── numPart = "8"
      └── unitPart = "шту"
           │
           ▼
     NormalizeUnit("шту")
      └── returns "штук"          ← CHANGED (was "шт")
           │
           ▼
     Result: name="туалетка", qty="8 штук"
```

Review template (`buy.review-add-qty`: `"Add: {item} ×{quantity}?"`) already prepends `×`, so the rendered message becomes:

```
Add: туалетка ×8 штук?
```

No template changes needed. The `×` originates from the template, not the quantity string.

## Unit Normalisation Rules

### Count units (Russian)

| Input | Current output | New output |
|---|---|---|
| `шт` | `шт` | `штук` |
| `шт.` | `шт` | `штук` |
| `шту` | `шт` | `штук` |
| `шту.` | `шт` | `штук` |
| `штуку` | `шт` | `штук` |
| `штук` | `шт` | `штук` |

All Russian piece-count variants → canonical `штук`.

### Count units (Latin)

| Input | Current output | New output |
|---|---|---|
| `pcs` | `pcs` | `pcs` *(no change)* |
| `piece` | `pcs` | `pcs` *(no change)* |
| `pieces` | `pcs` | `pcs` *(no change)* |
| `pack` | `packs` | `packs` *(no change)* |
| `packs` | `packs` | `packs` *(no change)* |

### Non-count units (unchanged)

`л`, `кг`, `г`, `мл`, `kg`, `g`, `ml`, `l` — pass through as-is.

## Regex: No Change Required

The existing `QtyPattern` already matches `шту` via the alternation `шт(?:уку?|\.)?`. It correctly captures `шту` as the unit part. The only change is in the post-match normalisation step.

## Bare-integer fallback

The existing fallback (last token is a standalone integer → treated as qty) is unchanged. "хлеб 2" → qty=`"2"`. The template renders it as `×2`.

## Tests to add / update

- `Parse_ShtuUnit_ReturnsShtuokCanonical`: `"туалетка 8 шту"` → `qty="8 штук"`
- `Parse_ShtukaUnit_ReturnsShtuokCanonical`: `"туалетка 3 штука"` → `qty="3 штук"` *(if not covered)*
- Update `Parse_WithKupitPrefixAndUnit_ReturnsNormalizedQty`: expected qty changes from `"1 шт"` → `"1 штук"`
- Update `Parse_WithoutKupitPrefix_ReturnsNormalizedQty`: expected qty changes from `"1 шт"` → `"1 штук"`

## AOT Safety

No new reflection, no dynamic code gen. `[GeneratedRegex]` attribute unchanged.
