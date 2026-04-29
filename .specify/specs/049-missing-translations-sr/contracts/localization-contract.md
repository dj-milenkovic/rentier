# Localization Contract: String Key Parity

**Feature**: 049-missing-translations-sr

## Contract

The application exposes user-visible strings through `ILocalizationService` which resolves keys by culture code. The contract guarantees:

### Key Parity Invariant

Every public static string property in `Strings.Designer.cs` (English source) MUST have a corresponding entry in `SrLatnStrings.All` (Serbian dictionary). The reverse must also hold — no orphaned Serbian keys.

### Enum Display Contract

Every value of every UI-visible enum MUST map to a localized resource key in its display converter. The converter MUST NOT fall through to `.ToString()` for any defined enum value.

| Enum | Converter | Required Key Pattern |
|------|-----------|---------------------|
| `FilingStatus` | `FilingStatusDisplayConverter` | `FilingStatus_{Value}` |
| `IncomeType` | `IncomeTypeDisplayConverter` | `IncomeType_{Value}` |
| `ReportStatus` | `ReportStatusDisplayConverter` | `ReportStatus_{Value}` |
| `SyncMode` | `SyncModeDisplayConverter` | `Sync_Mode_{Value}` |
| `DuplicateStrategy` | `DuplicateStrategyDisplayConverter` | `Sync_Strategy_{Value}` |

### Format String Contract

Localized format strings containing `{0}`, `{1}`, etc. MUST preserve the same placeholder indices in all cultures. Format resolution MUST happen in the ViewModel layer (not AXAML StringFormat) to ensure the format template itself is localized.

### Hardcoded String Policy

No user-visible text may appear as literal strings in `.axaml` files. Exceptions:
- Language names displayed in their native script (e.g., "English", "Srpski")
- Numeric format examples (e.g., "e.g. 100.00")
- Technical identifiers that are language-neutral

### Verification

A unit test (`TranslationParity_AllEnglishKeysHaveSerbianTranslations`) MUST assert that the English key set equals the Serbian key set with zero difference.
