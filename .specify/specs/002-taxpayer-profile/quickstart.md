# Quickstart: Taxpayer Profile Management (Feature 002)

**Generated**: 2026-04-06

---

## Prerequisites

- .NET 8 SDK installed
- Repository cloned; dependencies restored (`dotnet restore`)
- Feature branch `feature/002-taxpayer-profile` checked out

---

## Step 1 — Apply the Migration

When the application starts it applies pending EF Core migrations automatically.
To apply manually from the repository root:

```powershell
dotnet ef database update --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop
```

Verify the `TaxpayerProfiles` table exists in the SQLite database file (typically
`rentier.db` in the application data folder). It should contain a `UNIQUE` index on the
`Jmbg` column.

---

## Step 2 — Run the Application

```powershell
dotnet run --project src/Rentier.Desktop
```

The application should start without errors and display the main shell with the navigation
sidebar.

---

## Step 3 — Navigate to Settings → Profile

1. Click **Settings** in the left navigation panel.
2. The Settings area now shows a `TabControl` with a **Profile** tab.
3. The Profile form is displayed with all fields empty (first run against a fresh database).

**Expected**: All text inputs are blank. The **Save Profile** button is disabled (no valid data yet).

---

## Step 4 — Fill In the Profile Form

Enter data in each field:

| Field | Example Value | Required? |
|-------|--------------|-----------|
| JMBG | `1234567890123` | ✅ Yes (exactly 13 digits) |
| Full Name | `Petar Petrović` | ✅ Yes |
| Address | `Ulica 1, Beograd` | ✅ Yes |
| Opstina Code | `70122` | ✅ Yes |
| Phone Number | `+381 60 1234567` | ❌ Optional |
| Email | `petar@example.com` | ❌ Optional |

**Validation check**: While typing a JMBG with fewer than 13 digits, the inline error
`"JMBG must be exactly 13 digits"` appears and the **Save Profile** button remains disabled.
Once exactly 13 numeric digits are entered (and all other required fields are non-empty), the
button becomes enabled.

---

## Step 5 — Save the Profile

1. Press **Save Profile**.
2. The button shows a loading/disabled state briefly during the async save.
3. A success message `"Profile saved successfully."` appears in the form.

**Expected**: No error message. The database now contains one row in `TaxpayerProfiles`.

---

## Step 6 — Verify Persistence (Restart Test)

1. Close and restart the application.
2. Navigate back to **Settings → Profile**.
3. **Expected**: All previously saved values are pre-populated in the form exactly as entered.
   The success message is not shown (it is transient, not persisted).

---

## Step 7 — Edit Existing Profile

1. Change the **Address** field to a new value.
2. Press **Save Profile**.
3. Close and restart the application.
4. Navigate to **Settings → Profile**.
5. **Expected**: The updated address is shown. The `Id` in the database is unchanged (no new row
   was inserted). The database still contains exactly one row.

---

## Step 8 — Run the Tests

From the repository root:

```powershell
dotnet test
```

### Expected test results

| Test Project | Test Class | Tests | Expected |
|---|---|---|---|
| `Rentier.Domain.Tests` | `TaxpayerProfileTests` | 14 | ✅ All pass | <!-- 9 property/invariant tests from US1 + 5 JMBG boundary tests from US3 --> |
| `Rentier.Application.Tests` | `SaveTaxpayerProfileCommandHandlerTests` | ~5 | ✅ All pass |
| `Rentier.Application.Tests` | `GetTaxpayerProfileQueryHandlerTests` | ~3 | ✅ All pass |
| `Rentier.Infrastructure.Tests` | `TaxpayerProfileRepositoryTests` | ~5 | ✅ All pass |
| `Rentier.Desktop.Tests` | `SettingsViewModelTests` | ~6 | ✅ All pass |

All previously passing tests (e.g., `FilingStatusTransitionTests`) must continue to pass — no
regressions introduced.

---

## Troubleshooting

### Migration not applied on startup

Check that `AppDbContext` startup code calls `dbContext.Database.MigrateAsync()`. If the table
is missing, run the manual migration command in Step 1.

### Save button never enables

Ensure JMBG is exactly **13 numeric digits** (not letters or spaces). Check that FullName, Address,
and OpstinaCode are all non-blank. The `SaveCommand.CanExecute` observable requires all four
conditions simultaneously.

### `DomainException` on save

The Application handler caught a `DomainException` from the `TaxpayerProfile` constructor. This
should not happen if the ViewModel validation is working correctly, as the Save button should be
disabled for any invalid input. Check that the ViewModel `canSave` observable covers all the same
conditions as the Domain invariants.

### JMBG unique index violation

This would indicate a bug where a second insert was attempted. The singleton enforcement in the
`SaveTaxpayerProfileCommandHandler` should prevent this. Check the `GetAsync` call in the handler
and verify `AsNoTracking` is used in the repository.
