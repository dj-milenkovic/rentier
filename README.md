# Rentier

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![.NET 8.0+](https://img.shields.io/badge/.NET-8.0+-512BD4.svg)](https://dotnet.microsoft.com/)
[![Active Development](https://img.shields.io/badge/Status-Active%20Development-brightgreen.svg)](#project-status)

A modern, efficient desktop application for automated Serbian PP-OPO (passive income from securities) tax filing and management. Built with **.NET 8+** and **Avalonia** using **Clean Architecture principles**.

## Overview

Rentier simplifies the complex process of filing Serbian PP-OPO (passive income from securities) taxes by automating:

- **Statement Parsing**: Direct integration with Interactive Brokers (IBKR) and universal CSV import
- **Tax Calculation**: Automatic computation of withholding taxes and exchange rate adjustments
- **Filing Management**: Lifecycle tracking (draft → filed → paid) with deadline automation
- **Exchange Rates**: Automatic NBS (Serbian National Bank) exchange rate fetching and caching
- **Email Sync**: Secure mailbox synchronization for automated statement retrieval

## Features

✅ **Automated Statement Import** – Parse IBKR CSV exports and custom statement formats  
✅ **Real-Time Tax Calculations** – Accurate withholding tax & foreign exchange adjustments  
✅ **Secure Credential Management** – OS-level credential store (no passwords in SQLite)  
✅ **Multi-Year Filing Support** – Manage filings across multiple tax years  
✅ **NBS Exchange Rate Integration** – Auto-fetch and cache daily Serbian exchange rates  
✅ **IMAP Email Sync** – Automated statement retrieval with secure mailbox management  
✅ **Holiday Calendar** – Serbian public holidays for deadline calculations  
✅ **Fully Async** – Non-blocking UI with ReactiveCommand architecture  
✅ **Comprehensive Testing** – xUnit + FluentAssertions coverage  

## Technology Stack

- **Language**: C# 12+
- **Framework**: .NET 8.0+
- **UI**: Avalonia (cross-platform MVVM)
- **Database**: SQLite with Entity Framework Core
- **Architecture**: Clean Architecture with CQRS pattern
- **Async**: Full async/await with no blocking calls
- **Testing**: xUnit, FluentAssertions, NSubstitute
- **API Integration**: HttpClient (typed client pattern)

## Architecture

Rentier follows **Clean Architecture** principles with strict layer separation:

```
Rentier.Domain          → Pure C# records, value objects, domain logic
  ↑
Rentier.Application     → CQRS commands/queries, business rules, IRepository interfaces
  ↑
Rentier.Infrastructure  → EF Core, API clients, email services, credential store
  ↑
Rentier.Desktop        → Avalonia UI, ViewModels, event handling
```

**Key Patterns:**
- **CQRS**: Commands (`*Command`) and Queries (`*Query`) with corresponding handlers
- **Result Pattern**: Infrastructure returns `Result<T, Error>` (no exception-as-control-flow)
- **Value Objects**: `Money`, `MailboxCursor`, `TaxFilingStatus` enforce domain invariants
- **Dependency Injection**: Composition root in `Rentier.Desktop/Composition/`

## Prerequisites

- **.NET 8.0 SDK** or later ([download](https://dotnet.microsoft.com/download))
- **Windows 10+** (Avalonia supports Linux/macOS, but UI is Windows-optimized)
- **Visual Studio 2022** or **Rider** (recommended)

## Getting Started

### Clone & Build

```bash
git clone https://github.com/djordje.milenkovic96/rentier.git
cd rentier
dotnet restore
dotnet build
```

### Run the Application

```bash
# From workspace root
dotnet run --project src/Rentier.Desktop/Rentier.Desktop.csproj
```

### Run Tests

```bash
# All tests
dotnet test

# With coverage (if coverlet installed)
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover

# Integration tests only (NBS API tests)
dotnet test --filter "Category!=Integration"
```

## Development Workflow

### 1. Create a Feature Branch
```bash
git checkout -b feature/your-feature-name
```

### 2. Implementation Guidelines

**Domain Layer** (`Rentier.Domain/`)
- Pure C# records, no external dependencies
- Enforce business rules in value object constructors
- Define `DomainException` for invalid state transitions

**Application Layer** (`Rentier.Application/`)
- Create `*Query`/`*Command` records
- Implement `*QueryHandler`/`*CommandHandler`
- Define repository interfaces (`IRepository<T>`)

**Infrastructure Layer** (`Rentier.Infrastructure/`)
- Implement EF Core repositories
- Add migration: `dotnet ef migrations add MigrationName`
- Implement external service clients (NBS, IMAP, etc.)

**Desktop/UI** (`Rentier.Desktop/`)
- `*ViewModel` → calls Application use cases via `IMediator.Send()`
- All async: use `ReactiveCommand.CreateFromTask()`
- Bind to observables, no event handlers in code-behind

### 3. Monetary Values & Dates

**Always use**:
- `decimal` for money, tax rates, exchange rates
- `DateOnly` (not `DateTime`) for date-only values
- Convert at infrastructure boundary only

### 4. Async/Await Standards

```csharp
// ✅ Good
public async Task<Result<Filing>> ProcessAsync(CancellationToken ct)
{
    var filing = await _repository.GetAsync(id, ct);
    return Result.Ok(filing);
}

// ❌ Bad
public Task<Filing> Process() => Task.FromResult(_repository.Get(id).Result);
```

### 5. Testing

Create corresponding test file in `tests/Rentier.*.Tests/`:

**Domain Tests** (pure logic, no mocks)
```csharp
[Fact]
public void FilingStatus_InitToFiled_Succeeds()
{
    var filing = new Filing { Status = FilingStatus.Init };
    var result = filing.MarkAsFiled();
    result.IsSuccess.Should().BeTrue();
}
```

**Application Tests** (mock repositories)
```csharp
[Fact]
public async Task ProcessReportsCommandHandler_WithValidReports_UpdatesDatabase()
{
    var mockRepo = Substitute.For<IReportRepository>();
    var handler = new ProcessReportsCommandHandler(mockRepo);
    
    var result = await handler.Handle(new ProcessReportsCommand { ... }, CancellationToken.None);
    
    result.IsSuccess.Should().BeTrue();
    mockRepo.Received(1).SaveAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
}
```

**Integration Tests** (real EF Core InMemory)
```csharp
[Fact, Trait("Category", "Integration")]
public async Task NbsExchangeRateFetcher_FetchesRates_CachesResults()
{
    // Uses real HttpClient + fake handler
}
```

## Commit Message Convention

```
feat: Add NBS exchange rate fetcher
fix: Correct holiday deadline calculation
refactor: Extract MailboxSyncService logic
test: Add edge cases for filing status transitions
docs: Update architecture decision for Result pattern
chore: Update dependencies
```

## Documentation

Key architectural decisions and domain knowledge are documented in:
- [`.github/copilot-instructions.md`](.github/copilot-instructions.md) – Architecture rules, Clean Architecture constraints, CQRS flow
- [`.github/skills/`](.github/skills/) – Domain-specific implementation guides

## Contribution Guidelines

We welcome contributions! Before opening a pull request:

1. **Fork** this repository
2. **Create** a feature branch (`git checkout -b feature/descriptive-name`)
3. **Follow** the [Development Workflow](#development-workflow)
4. **Write tests** – Domain, Application, and UI layers all require coverage
5. **Run tests locally** – `dotnet test`
6. **Format code** – Follow C# conventions (PascalCase classes, camelCase members)
7. **Commit** with [conventional messages](#commit-message-convention)
8. **Push** and open a **Pull Request** with a clear description

### Code Review Expectations

- Clean Architecture boundaries preserved
- No passwords/secrets in code
- All async methods use `CancellationToken`
- `decimal` for monetary values, `DateOnly` for dates
- 100% async (no `.Result`, `.Wait()`, or blocking calls)
- Tests follow xUnit + FluentAssertions naming conventions

## Roadmap

- [ ] Multi-account support (multiple IBKR/email accounts)
- [ ] Web API for headless processing
- [ ] Bulk filing import/export
- [ ] Alternative statement format providers (Revolut, Wise, etc.)
- [ ] Cross-platform desktop (Linux/macOS Avalonia improvements)

## Support & Issues

- **Bug Reports**: Open an [issue](../../issues) with reproduction steps
- **Feature Requests**: Use [discussions](../../discussions) or file an issue with `enhancement` label
- **Q&A**: Check existing issues/discussions before asking

## License

This project is licensed under the **Apache License 2.0** – see [LICENSE](LICENSE) for details.

## Authors

- **Djordje Milenkovic**

## Acknowledgments

- [Avalonia](https://avaloniaui.net/) for cross-platform MVVM framework
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) for data access
- [Serbian National Bank (NBS)](https://www.nbs.rs/) for exchange rate services
- Open source community feedback and contributions
