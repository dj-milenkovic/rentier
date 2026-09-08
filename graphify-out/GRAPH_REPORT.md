# Graph Report - rentier  (2026-08-26)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 5830 nodes · 17428 edges · 264 communities (242 shown, 22 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 1781 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `79c0ca49`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Community 0
- Community 1
- Community 2
- Community 3
- Community 4
- Community 5
- Community 6
- Community 7
- Community 8
- Community 9
- Community 10
- Community 11
- Community 12
- Community 13
- Community 14
- Community 15
- Community 16
- Community 17
- Community 18
- Community 19
- Community 20
- Community 21
- Community 22
- Community 23
- Community 24
- Community 25
- Community 26
- Community 27
- Community 28
- Community 29
- Community 30
- Community 31
- Community 32
- Community 33
- Community 34
- Community 35
- Community 36
- Community 37
- Community 38
- Community 39
- Community 40
- Community 41
- Community 42
- Community 43
- Community 44
- Community 45
- Community 46
- Community 47
- Community 48
- Community 49
- Community 50
- Community 51
- Community 52
- Community 53
- Community 54
- Community 55
- Community 56
- Community 57
- Community 58
- Community 59
- Community 60
- Community 61
- Community 62
- Community 63
- Community 64
- Community 65
- Community 66
- Community 67
- Community 68
- Community 69
- Community 70
- Community 71
- Community 72
- Community 73
- Community 74
- Community 75
- Community 76
- Community 77
- Community 78
- Community 79
- Community 80
- Community 81
- Community 82
- Community 83
- Community 84
- Community 85
- Community 86
- Community 87
- Community 88
- Community 89
- Community 90
- Community 91
- Community 92
- Community 93
- Community 94
- Community 95
- Community 96
- Community 97
- Community 98
- Community 99
- Community 100
- Community 101
- Community 102
- Community 103
- Community 104
- Community 105
- Community 106
- Community 107
- Community 108
- Community 109
- Community 110
- Community 111
- Community 112
- Community 113
- Community 114
- Community 115
- Community 116
- Community 117
- Community 118
- Community 119
- Community 120
- Community 121
- Community 122
- Community 123
- Community 124
- Community 125
- Community 126
- Community 127
- Community 128
- Community 129
- Community 130
- Community 131
- Community 132
- Community 133
- Community 134
- Community 135
- Community 136
- Community 137
- Community 138
- Community 139
- Community 140
- Community 141
- Community 142
- Community 143
- Community 144
- Community 145
- Community 146
- Community 147
- Community 148
- Community 149
- Community 150
- Community 151
- Community 152
- Community 153
- Community 154
- Community 155
- Community 156
- Community 157
- Community 158
- Community 159
- Community 160
- Community 161
- Community 162
- Community 163
- Community 164
- Community 165
- Community 166
- Community 167
- Community 168
- Community 169
- Community 170
- Community 171
- Community 172
- Community 173
- Community 174
- Community 175
- Community 176
- Community 177
- Community 178
- Community 179
- Community 180
- Community 181
- Community 182
- Community 183
- Community 184
- Community 185
- Community 186
- Community 187
- Community 188
- Community 189
- Community 190
- Community 191
- Community 192
- Community 193
- Community 194
- Community 195
- Community 196
- Community 197
- Community 198
- Community 200
- Community 201
- Community 202
- Community 203
- Community 204
- Community 205
- Community 206
- Community 207
- Community 208
- Community 209
- Community 210
- Community 211
- Community 212
- Community 213
- Community 214
- Community 215
- Community 216
- Community 217
- Community 218
- Community 219
- Community 220
- Community 221
- Community 222
- Community 223
- Community 224
- Community 225
- Community 226
- Community 227
- Community 228
- Community 229
- Community 230
- Community 231
- Community 232
- Community 233
- Community 234
- Community 235
- Community 236
- Community 237
- Community 238
- Community 239
- Community 240
- Community 241
- Community 242
- Community 243
- Community 244
- Community 245
- Community 246
- Community 247
- Community 248
- Community 249
- Community 250
- Community 251
- Community 252
- Community 253
- Community 254
- Community 255
- Community 256
- Community 257
- Community 258

## God Nodes (most connected - your core abstractions)
1. `Result` - 277 edges
2. `Strings` - 264 edges
3. `Rentier.Application.DTOs` - 135 edges
4. `Rentier.Application.Common` - 133 edges
5. `Rentier.Application.Interfaces` - 133 edges
6. `ICommandHandler` - 130 edges
7. `Rentier.Application.Commands` - 121 edges
8. `IQueryHandler` - 118 edges
9. `Rentier.Domain.Enums` - 116 edges
10. `VoidResult` - 112 edges

## Surprising Connections (you probably didn't know these)
- `ImportReportCommandHandlerTests` --references--> `ProcessReportsCommand`  [EXTRACTED]
  tests/Rentier.UnitTests/Application/ImportReportCommandHandlerTests.cs → src/Rentier.Application/Commands/ProcessReportsCommand.cs
- `NullCredentialStoreTests` --references--> `Error`  [EXTRACTED]
  tests/Rentier.Infrastructure.Tests/Security/NullCredentialStoreTests.cs → src/Rentier.Application/Common/Error.cs
- `ImportReportCommandHandlerTests` --references--> `Result`  [EXTRACTED]
  tests/Rentier.UnitTests/Application/ImportReportCommandHandlerTests.cs → src/Rentier.Application/Common/Result.cs
- `ImportReportCommandHandlerTests` --references--> `ProcessReportsResult`  [EXTRACTED]
  tests/Rentier.UnitTests/Application/ImportReportCommandHandlerTests.cs → src/Rentier.Application/DTOs/ProcessReportsResult.cs
- `AddImporterCommandHandlerTests` --references--> `AddImporterCommandHandler`  [EXTRACTED]
  tests/Rentier.UnitTests/Application/AddImporterCommandHandlerTests.cs → src/Rentier.Application/Handlers/AddImporterCommandHandler.cs

## Import Cycles
- None detected.

## Communities (264 total, 22 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.01
Nodes (264): Strings, Appearance_Header, Appearance_Subheader, AppTitle, BulkDelete_Button_Template, BulkDelete_Cancel_Button, BulkDelete_ClearSelection_Button, BulkDelete_Confirm_Button (+256 more)

### Community 1 - "Community 1"
Cohesion: 0.06
Nodes (11): Rentier.Application.Parsing, Rentier.Tests.Common.Fakes, Rentier.Application.Repositories, Rentier.Application.Commands, Rentier.Application.Services, Rentier.UnitTests.Application, Rentier.Application.Common, Rentier.Application.Handlers (+3 more)

### Community 2 - "Community 2"
Cohesion: 0.05
Nodes (17): Rentier.Desktop.Models, Rentier.Desktop.Composition, Rentier.Application.DTOs, Rentier.UnitTests.Desktop, Rentier.Desktop.Resources, Rentier.Application.Queries, Rentier.Desktop.Dialogs, Rentier.Desktop.Services (+9 more)

### Community 3 - "Community 3"
Cohesion: 0.05
Nodes (45): CancellationToken, Guid, Task, CancellationToken, Task, CancellationToken, Guid, Task (+37 more)

### Community 4 - "Community 4"
Cohesion: 0.05
Nodes (71): INotifyPropertyChanged, Guid, DeleteFilingCommand, Guid, ImportReportCommand, IReadOnlyList, SaveHolidayConfCommand, Guid (+63 more)

### Community 5 - "Community 5"
Cohesion: 0.06
Nodes (37): Rentier.Infrastructure.Tests.Security, Rentier.Infrastructure.Security, ExitCode, Info, IsSuccess, SecretService, Store, Task (+29 more)

### Community 6 - "Community 6"
Cohesion: 0.08
Nodes (21): DbUpdateException, CancellationToken, Guid, IReadOnlyList, Items, Task, TotalCount, ReportRepository (+13 more)

### Community 7 - "Community 7"
Cohesion: 0.05
Nodes (9): Rentier.Domain.Enums, Rentier.Domain.ValueObjects, Rentier.UnitTests.Application.Services, Rentier.Infrastructure.Tests.Serialization, Rentier.UnitTests.Domain.Services, Rentier.Domain.Services, Rentier.UnitTests, Rentier.Infrastructure.Serialization (+1 more)

### Community 8 - "Community 8"
Cohesion: 0.06
Nodes (46): ConcurrentDictionary, Guid, DeleteMailboxCommand, Guid, Error, Result, Error, IsSuccess (+38 more)

### Community 9 - "Community 9"
Cohesion: 0.08
Nodes (36): Currency, Date, dividends, Entity, GeneratedRegex, interest, rates, CancellationToken (+28 more)

### Community 10 - "Community 10"
Cohesion: 0.11
Nodes (9): IReadOnlyList, FilingsPageResult, CancellationToken, DateOnly, Error, Fact, Func, Task (+1 more)

### Community 11 - "Community 11"
Cohesion: 0.07
Nodes (12): Rentier.Infrastructure.Persistence.Configurations, Rentier.Domain.Entities, Rentier.Infrastructure.Sync, Rentier.Infrastructure.Repositories, Rentier.Infrastructure.Persistence, Rentier.UnitTests.Domain, Rentier.Infrastructure, Rentier.Tests.Common.Builders (+4 more)

### Community 12 - "Community 12"
Cohesion: 0.15
Nodes (10): profile, Items, TotalCount, DateOnly, Fact, Guid, SqliteConnection, Task (+2 more)

### Community 13 - "Community 13"
Cohesion: 0.10
Nodes (29): Guid, ExportFilingCommand, Func, ILogger, Task, HandlerHelper, CancellationToken, Error (+21 more)

### Community 14 - "Community 14"
Cohesion: 0.04
Nodes (55): Code, ReactiveUserControl, IReadOnlyList, AppearanceSettingsViewModel, IsDark, IsEnglish, IsLight, IsSrLatn (+47 more)

### Community 15 - "Community 15"
Cohesion: 0.09
Nodes (30): Source, DateOnly, IncomeType, FilingInfo, GrossIncomeRsd, GrossTaxPayableRsd, IncomeDate, IncomeType (+22 more)

### Community 16 - "Community 16"
Cohesion: 0.13
Nodes (27): Guid, AddImporterCommand, Guid, DeleteImporterCommand, Guid, UpdateImporterCommand, Guid, ImporterDto (+19 more)

### Community 17 - "Community 17"
Cohesion: 0.08
Nodes (32): CancellationToken, DateOnly, Error, IncomeType, Task, ManualFilingCalculationResult, CancellationToken, DateOnly (+24 more)

### Community 18 - "Community 18"
Cohesion: 0.12
Nodes (22): AvaloniaTheory, Border, DataGridColumnEventArgs, LogicalTreeAttachmentEventArgs, RadioButton, MultipleDisposable, FilingsView, AvaloniaFact (+14 more)

### Community 19 - "Community 19"
Cohesion: 0.07
Nodes (31): IAsyncLifetime, Guid, TaxpayerProfile, Address, Email, FullName, Id, Jmbg (+23 more)

### Community 20 - "Community 20"
Cohesion: 0.16
Nodes (15): Error, IncomeType, XNamespace, PpOpoXmlSerializer, UppercaseUtf8Encoding, BodyName, HeaderName, WebName (+7 more)

### Community 21 - "Community 21"
Cohesion: 0.15
Nodes (12): SyncAllCommand, IReadOnlyList, SyncAllResult, DateTimeOffset, SyncProgressEntry, ISyncAllCommandHandler, CancellationToken, Error (+4 more)

### Community 22 - "Community 22"
Cohesion: 0.14
Nodes (26): FilingFilterMode, All, Unpaid, FilingSortColumn, FilingDeadline, IncomeType, PayingEntity, PaymentReference (+18 more)

### Community 23 - "Community 23"
Cohesion: 0.04
Nodes (45): handler, IProgress, CancellationToken, Error, IProgress, Task, SyncProgressEntryViewModel, Icon (+37 more)

### Community 24 - "Community 24"
Cohesion: 0.04
Nodes (46): CurrentDirection, Action, Error, Func, IncomeType, MultipleDisposable, ObservableAsPropertyHelper, ReactiveCommand (+38 more)

### Community 25 - "Community 25"
Cohesion: 0.10
Nodes (26): SaveTaxpayerProfileCommand, CancellationToken, Error, Task, GetTaxpayerProfileQueryHandler, CancellationToken, Task, SaveTaxpayerProfileCommandHandler (+18 more)

### Community 26 - "Community 26"
Cohesion: 0.20
Nodes (21): Guid, IReadOnlyList, IReadOnlySet, ReportColumnFilter, CancellationToken, Error, Task, GetReportsQueryHandler (+13 more)

### Community 27 - "Community 27"
Cohesion: 0.07
Nodes (36): created, failed, IncomeEventOutcome, IncomeEventRequest, DateOnly, FilingCreationError, DateOnly, RateResolution (+28 more)

### Community 28 - "Community 28"
Cohesion: 0.07
Nodes (31): IOrderedQueryable, IQueryable, DateOnly, ExchangeRateSourceType, Guid, IncomeType, Filing, ExchangeRateSourceDate (+23 more)

### Community 29 - "Community 29"
Cohesion: 0.06
Nodes (40): Action, CancellationToken, Content, Error, FileName, Func, Guid, ImporterId (+32 more)

### Community 30 - "Community 30"
Cohesion: 0.11
Nodes (25): loc, SetUserPreferenceCommand, CancellationToken, Task, GetUserPreferenceQueryHandler, CancellationToken, Task, SetUserPreferenceCommandHandler (+17 more)

### Community 31 - "Community 31"
Cohesion: 0.17
Nodes (10): DateOnly, ManualFilingPreviewDto, Action, CancellationToken, DateOnly, Error, Fact, Guid (+2 more)

### Community 32 - "Community 32"
Cohesion: 0.05
Nodes (22): Rentier.Application.Enums, Rentier.Desktop.Converters, ComparisonOperator, Equals, GreaterThan, LessThan, IBrush, IValueConverter (+14 more)

### Community 33 - "Community 33"
Cohesion: 0.14
Nodes (14): Exception, DomainException, DateOnly, IEnumerable, BusinessDayResolver, DateOnly, HashSet, IReadOnlyList (+6 more)

### Community 34 - "Community 34"
Cohesion: 0.20
Nodes (8): UpdateCheckResult, Fact, UpdateCheckResultTests, Action, CancellationToken, Fact, Task, MainWindowViewModel_UpdateTests

### Community 35 - "Community 35"
Cohesion: 0.20
Nodes (12): Action, CancellationToken, Content, Error, Fact, FileName, Func, Guid (+4 more)

### Community 36 - "Community 36"
Cohesion: 0.15
Nodes (17): NeverSynced, CancellationToken, Guid, IReadOnlyList, Task, MailboxRepository, Fact, SqliteConnection (+9 more)

### Community 37 - "Community 37"
Cohesion: 0.29
Nodes (17): Client, Inbox, IMailboxRepository, CancellationToken, Fact, Guid, IList, IMailFolder (+9 more)

### Community 38 - "Community 38"
Cohesion: 0.09
Nodes (28): CancellationToken, Task, CancellationToken, Error, ReactiveCommand, RxVoid, Task, ViewModelActivator (+20 more)

### Community 39 - "Community 39"
Cohesion: 0.06
Nodes (35): Action, CancellationToken, DateTimeOffset, Error, Guid, IncomeType, IReadOnlyList, ReactiveCommand (+27 more)

### Community 40 - "Community 40"
Cohesion: 0.31
Nodes (14): IProgress, ProcessReportsCommand, IStatementParser, DateOnly, InterestRecord, IImporterRepository, CancellationToken, DateOnly (+6 more)

### Community 41 - "Community 41"
Cohesion: 0.11
Nodes (14): ReportProcessingDetail, SyncProgressSeverity, CursorTransition, DuplicateHandled, Error, Info, Warning, Fact (+6 more)

### Community 42 - "Community 42"
Cohesion: 0.18
Nodes (13): IReadOnlyList, HolidayConfDto, GetHolidayConfQuery, DateOnly, HolidayEntryViewModel, Date, Name, CancellationToken (+5 more)

### Community 43 - "Community 43"
Cohesion: 0.17
Nodes (14): CancellationToken, Count, DateOnly, EarliestDate, Guid, IReadOnlyDictionary, IReadOnlyList, Task (+6 more)

### Community 44 - "Community 44"
Cohesion: 0.24
Nodes (10): DateOnly, Guid, CreateManualFilingCommand, CancellationToken, DateOnly, Error, Fact, Guid (+2 more)

### Community 45 - "Community 45"
Cohesion: 0.21
Nodes (18): IReadOnlyList, ReportsPageResult, IQueryHandler, ReportsView, AvaloniaFact, Button, CancellationToken, CheckBox (+10 more)

### Community 46 - "Community 46"
Cohesion: 0.07
Nodes (28): CancellationToken, Error, IReadOnlyList, ISequencer, MultipleDisposable, ObservableCollection, ReactiveCommand, RxVoid (+20 more)

### Community 47 - "Community 47"
Cohesion: 0.06
Nodes (30): IReadOnlyList, ISequencer, MultipleDisposable, ReactiveCommand, RxVoid, ViewModelActivator, MainWindowViewModel, Activator (+22 more)

### Community 48 - "Community 48"
Cohesion: 0.12
Nodes (17): IReadOnlyList, StreamGeometry, NavigationEntry, Children, Icon, IndentLevel, IsActive, IsChild (+9 more)

### Community 49 - "Community 49"
Cohesion: 0.29
Nodes (11): IReadOnlyList, ProcessReportsResult, IReadOnlyList, SyncResult, ISyncMailboxCommandHandler, CancellationToken, Error, Fact (+3 more)

### Community 50 - "Community 50"
Cohesion: 0.11
Nodes (17): FuncValueConverter, SyncModeDisplayConverter, SyncMode, FullReplay, Incremental, ReplayFromDate, DateOnly, Guid (+9 more)

### Community 51 - "Community 51"
Cohesion: 0.08
Nodes (26): CancellationToken, Error, Func, Guid, IReadOnlyList, ISequencer, ObservableCollection, ReactiveCommand (+18 more)

### Community 52 - "Community 52"
Cohesion: 0.26
Nodes (7): CancellationToken, Error, Fact, Func, Guid, Task, FilingsViewModelBulkDeleteTests

### Community 53 - "Community 53"
Cohesion: 0.17
Nodes (9): DateOnly, Guid, FilingRowDto, Action, Guid, NewStatus, DateOnly, Fact (+1 more)

### Community 54 - "Community 54"
Cohesion: 0.12
Nodes (21): ImporterSyncResult, MimePart, ICredentialStore, IServiceCollection, Task, InfrastructureServiceExtensions, Action, CancellationToken (+13 more)

### Community 55 - "Community 55"
Cohesion: 0.12
Nodes (18): ListBoxItem, ReactiveWindow, SelectableTextBlock, IUpdateService, IsInstalled, MainWindow, AvaloniaFact, CancellationToken (+10 more)

### Community 56 - "Community 56"
Cohesion: 0.20
Nodes (12): DateOnly, Guid, CalculateManualFilingCommand, CancellationToken, DateOnly, Error, Fact, Guid (+4 more)

### Community 57 - "Community 57"
Cohesion: 0.28
Nodes (11): CancellationToken, DateOnly, Error, Task, GetDashboardQueryHandler, GetDashboardQuery, CancellationToken, DateOnly (+3 more)

### Community 58 - "Community 58"
Cohesion: 0.19
Nodes (16): IExchangeRateFetcher, HashSet, ILogger, ExchangeRateResolver, HashSet, CompositeExchangeRateFetcher, CancellationToken, DateOnly (+8 more)

### Community 59 - "Community 59"
Cohesion: 0.14
Nodes (19): CancellationToken, Guid, IReadOnlyList, Task, IReportRepository, DateOnly, Guid, Report (+11 more)

### Community 60 - "Community 60"
Cohesion: 0.28
Nodes (7): CancellationToken, Error, Fact, Func, Guid, Task, ReportsViewModelBulkDeleteTests

### Community 61 - "Community 61"
Cohesion: 0.07
Nodes (27): Accent, Adding a new token, Arc commands — use explicit spacing, Asset pipeline for bitmap icons, Control class conventions (`Controls.axaml`), Correct token usage patterns, Critical: closed paths only, Design token reference (+19 more)

### Community 62 - "Community 62"
Cohesion: 0.21
Nodes (12): IDisposable, SemaphoreSlim, Action, CancellationToken, ILogger, Task, VelopackUpdateService, IsInstalled (+4 more)

### Community 63 - "Community 63"
Cohesion: 0.07
Nodes (27): DateOnly, Guid, OverdueFilingDto, Action, CancellationToken, Error, ObservableCollection, ReactiveCommand (+19 more)

### Community 64 - "Community 64"
Cohesion: 0.31
Nodes (9): GetMailboxesQuery, CancellationToken, DateOnly, Error, Fact, Guid, IReadOnlyList, Task (+1 more)

### Community 65 - "Community 65"
Cohesion: 0.28
Nodes (13): IExchangeRateCacheRepository, CancellationToken, DateOnly, Fact, HttpRequestMessage, HttpResponseMessage, HttpStatusCode, IReadOnlyList (+5 more)

### Community 66 - "Community 66"
Cohesion: 0.13
Nodes (14): IAppVersionService, DisplayVersion, CancellationToken, Content, Error, Fact, FileName, Func (+6 more)

### Community 67 - "Community 67"
Cohesion: 0.15
Nodes (13): IObservable, ReactiveCommand, RxVoid, Signal, TextFilterFlyoutViewModel, Applied, ApplyCommand, IsActive (+5 more)

### Community 68 - "Community 68"
Cohesion: 0.17
Nodes (13): DateOnly, CancellationToken, Fact, Guid, IList, IMailFolder, ImapClient, MimeMessage (+5 more)

### Community 69 - "Community 69"
Cohesion: 0.22
Nodes (11): FiledCount, InitCount, PaidCount, TotalUnpaidRsd, DateOnly, Fact, Guid, SqliteConnection (+3 more)

### Community 70 - "Community 70"
Cohesion: 0.22
Nodes (18): SyncMailboxCommand, CancellationToken, Error, IProgress, Task, SyncMailboxCommandHandler, CancellationToken, Error (+10 more)

### Community 71 - "Community 71"
Cohesion: 0.08
Nodes (25): DateOnly, IncomeType, IReadOnlyList, ReactiveCommand, RxVoid, FilingRowViewModel, AdvanceStatusCommand, AdvanceStatusTooltip (+17 more)

### Community 72 - "Community 72"
Cohesion: 0.08
Nodes (24): For /graphify add and --watch, For /graphify query, For the commit hook and native CLAUDE.md integration, For --update and --cluster-only, /graphify, Honesty Rules, Interpreter guard for subcommands, Part A - Structural extraction for code files (+16 more)

### Community 73 - "Community 73"
Cohesion: 0.09
Nodes (21): ReactiveObject, CheckableItem, IsChecked, Label, Value, HashSet, IObservable, IReadOnlySet (+13 more)

### Community 74 - "Community 74"
Cohesion: 0.21
Nodes (13): Guid, UpdateMailboxCommand, CancellationToken, Task, UpdateMailboxCommandHandler, CancellationToken, Guid, Task (+5 more)

### Community 75 - "Community 75"
Cohesion: 0.16
Nodes (14): DateOnly, ExchangeRate, Currency, Date, RateToRsd, EntityTypeBuilder, ExchangeRateCacheConfiguration, DateOnly (+6 more)

### Community 76 - "Community 76"
Cohesion: 0.24
Nodes (14): HttpMessageHandler, CultureInfo, HttpClient, NbsWebScraper, CancellationToken, DateOnly, Fact, HttpRequestMessage (+6 more)

### Community 77 - "Community 77"
Cohesion: 0.14
Nodes (13): IElement, DateOnly, HolidayEntryDto, CancellationToken, DateOnly, Error, HttpClient, IReadOnlyList (+5 more)

### Community 78 - "Community 78"
Cohesion: 0.09
Nodes (19): DbContext, DbContextOptions, DbSet, IDesignTimeDbContextFactory, HolidayYearRange, ModelBuilder, AppDbContext, ExchangeRateCache (+11 more)

### Community 79 - "Community 79"
Cohesion: 0.23
Nodes (8): DateOnly, IReadOnlyList, DashboardDto, Action, CancellationToken, Error, Fact, DashboardViewModelTests

### Community 80 - "Community 80"
Cohesion: 0.20
Nodes (8): UserPreference, Key, Value, EntityTypeBuilder, UserPreferenceConfiguration, ArgumentNullException, Fact, UserPreferenceTests

### Community 81 - "Community 81"
Cohesion: 0.17
Nodes (15): IEntityTypeConfiguration, CancellationToken, Error, Task, HolidayYearRange, EndYear, Id, StartYear (+7 more)

### Community 82 - "Community 82"
Cohesion: 0.17
Nodes (9): Dictionary, IObservable, IReadOnlyDictionary, Signal, LocalizationService, CultureChanged, CurrentCultureCode, Fact (+1 more)

### Community 83 - "Community 83"
Cohesion: 0.35
Nodes (8): CancellationToken, DateOnly, Error, Fact, Guid, Stream, Task, ProcessReportsProgressTests

### Community 84 - "Community 84"
Cohesion: 0.28
Nodes (11): CancellationToken, Error, Guid, Task, CancellationToken, Error, Fact, Guid (+3 more)

### Community 85 - "Community 85"
Cohesion: 0.29
Nodes (11): DateOnly, IbkrExchangeRate, IReadOnlyList, StatementParseResult, CancellationToken, DateOnly, Fact, Guid (+3 more)

### Community 86 - "Community 86"
Cohesion: 0.28
Nodes (8): CancellationToken, DateOnly, Guid, IReadOnlyList, Items, Task, TotalCount, IFilingRepository

### Community 87 - "Community 87"
Cohesion: 0.23
Nodes (5): Fact, InlineData, SyncedTo, Theory, MailboxTests

### Community 88 - "Community 88"
Cohesion: 0.22
Nodes (7): DateOnly, FilingDeadlineCalculator, Fact, InlineData, Theory, FilingDeadlineCalculatorTests, NoHolidays

### Community 89 - "Community 89"
Cohesion: 0.31
Nodes (6): DateOnly, Fact, SqliteConnection, Task, ValueTask, FilingRepositoryColumnFilterTests

### Community 90 - "Community 90"
Cohesion: 0.21
Nodes (13): CancellationTokenSource, Guid, IReadOnlyList, BulkDeleteFilingsCommand, CancellationToken, Task, BulkDeleteFilingsCommandHandler, CancellationToken (+5 more)

### Community 91 - "Community 91"
Cohesion: 0.23
Nodes (9): ColumnTag, Reference, CancellationToken, Task, Id, CancellationToken, Guid, NewStatus (+1 more)

### Community 92 - "Community 92"
Cohesion: 0.32
Nodes (9): conn, db, CancellationToken, Task, ExchangeRateCacheRepository, Fact, SqliteConnection, Task (+1 more)

### Community 93 - "Community 93"
Cohesion: 0.24
Nodes (12): Guid, IReadOnlyList, BulkDeleteReportsCommand, CancellationToken, Task, BulkDeleteReportsCommandHandler, CancellationToken, Fact (+4 more)

### Community 94 - "Community 94"
Cohesion: 0.21
Nodes (14): CancellationToken, Error, IReadOnlyList, Task, FetchHolidaysFromWebCommandHandler, CancellationToken, Error, IReadOnlyList (+6 more)

### Community 95 - "Community 95"
Cohesion: 0.36
Nodes (9): DateOnly, DividendRecord, CancellationToken, DateOnly, Fact, Guid, Stream, Task (+1 more)

### Community 96 - "Community 96"
Cohesion: 0.21
Nodes (6): DateOnly, MailboxCursor, NeverSynced, SyncedTo, Fact, MailboxCursorTests

### Community 97 - "Community 97"
Cohesion: 0.22
Nodes (11): CancellationToken, HolidayYearRange, ILogger, IReadOnlyList, Task, HolidayRepository, Fact, SqliteConnection (+3 more)

### Community 98 - "Community 98"
Cohesion: 0.28
Nodes (10): ValueTask, CancellationToken, DateOnly, Error, Fact, Guid, Task, ValueTask (+2 more)

### Community 99 - "Community 99"
Cohesion: 0.11
Nodes (18): Anti-Patterns to Avoid, Application Unit Tests (CQRS Handlers), Async Rules, DI Smoke Test, Domain Unit Tests, Financial Precision, Namespace, One Test Class Per Handler (+10 more)

### Community 100 - "Community 100"
Cohesion: 0.11
Nodes (19): Base Currency Exchange Rate, Dividends, Empty import after email sync, IBKR Activity Statement Setup, Interest, Next Steps, "No recognised IBKR sections found in the CSV", Option A — Manual Export (Simplest) (+11 more)

### Community 101 - "Community 101"
Cohesion: 0.11
Nodes (19): Base Currency Exchange Rate, Dividends, IBKR Activity Statement instalacija, Interest, Korak 1 — Prijavite se na IBKR Client Portal, Korak 2 — Navigujte do Statements, Korak 3 — Generišite Activity Statement, Korak 4 — Otpremite u Rentier (+11 more)

### Community 102 - "Community 102"
Cohesion: 0.30
Nodes (10): AddMailboxCommand, CancellationToken, Guid, Task, AddMailboxCommandHandler, CancellationToken, DateOnly, Fact (+2 more)

### Community 103 - "Community 103"
Cohesion: 0.19
Nodes (11): DateOnly, Guid, ReportRowDto, ReportStatus, Error, Init, PartialError, Processed (+3 more)

### Community 104 - "Community 104"
Cohesion: 0.26
Nodes (9): DashboardView, AvaloniaFact, CancellationToken, DataGrid, Error, ProgressBar, StackPanel, TextBlock (+1 more)

### Community 105 - "Community 105"
Cohesion: 0.18
Nodes (9): Action, CancellationToken, Fact, Task, NotInstalledVelopackManager, IsInstalled, ThrowingVelopackManager, IsInstalled (+1 more)

### Community 107 - "Community 107"
Cohesion: 0.19
Nodes (6): DateOnly, Fact, IncomeType, InlineData, Theory, FilingInfoTests

### Community 108 - "Community 108"
Cohesion: 0.11
Nodes (18): IActivatableViewModel, ISequencer, MultipleDisposable, ObservableAsPropertyHelper, ObservableCollection, ReactiveCommand, RxVoid, ViewModelActivator (+10 more)

### Community 109 - "Community 109"
Cohesion: 0.23
Nodes (11): EnsureHolidaysSeededCommand, CancellationToken, Task, CancellationToken, IReadOnlyList, Task, CancellationToken, Fact (+3 more)

### Community 110 - "Community 110"
Cohesion: 0.12
Nodes (12): IServiceCollection, Task, IInfrastructureRegistrar, IServiceCollection, Task, InfrastructureRegistrar, InlineData, IServiceCollection (+4 more)

### Community 111 - "Community 111"
Cohesion: 0.25
Nodes (6): IPaginatedQuery, Page, PageSize, Fact, FakeQuery, PaginationValidatorTests

### Community 112 - "Community 112"
Cohesion: 0.27
Nodes (9): HolidaySettingsView, AvaloniaFact, Button, CancellationToken, DataGrid, Error, IReadOnlyList, TextBlock (+1 more)

### Community 113 - "Community 113"
Cohesion: 0.24
Nodes (7): Money, Amount, Currency, Fact, InlineData, Theory, MoneyTests

### Community 114 - "Community 114"
Cohesion: 0.27
Nodes (8): CancellationToken, Task, UserPreferenceRepository, Fact, SqliteConnection, Task, ValueTask, UserPreferenceRepositoryTests

### Community 115 - "Community 115"
Cohesion: 0.12
Nodes (16): Accessibility Expert, Anti-Patterns to Avoid, Checklists, Designer Checklist, Developer Checklist, Device-Independent Input, Dynamic Interfaces, Forms (+8 more)

### Community 116 - "Community 116"
Cohesion: 0.12
Nodes (16): 1. Initial state (before activation), 2. Success state (handler returns data), 3. Failure state (handler returns an error), Activating ViewModels, Anti-Patterns to Avoid, Avalonia Headless UI Tests, Collection Assertions, Derived and Formatted Properties (+8 more)

### Community 117 - "Community 117"
Cohesion: 0.33
Nodes (10): ListBox, MailboxSettingsView, AvaloniaFact, Button, CancellationToken, Error, Guid, IReadOnlyList (+2 more)

### Community 118 - "Community 118"
Cohesion: 0.32
Nodes (5): CancellationToken, Error, Fact, Task, ProfileSettingsViewModelTests

### Community 119 - "Community 119"
Cohesion: 0.12
Nodes (15): Anti-Patterns to Avoid, Category Trait — Mandatory, Credential Store Tests, Database Setup — The Golden Pattern, DateOnly and Decimal Round-Trip, External HTTP Services — No Real Network, In-Memory Connection Keep-Alive (Alternative Pattern), Naming Convention (+7 more)

### Community 120 - "Community 120"
Cohesion: 0.24
Nodes (7): IAsyncDisposable, Fact, IReadOnlyList, SqliteConnection, Task, ValueTask, MigrationChainTests

### Community 121 - "Community 121"
Cohesion: 0.28
Nodes (6): IMigrator, Guid, SqliteConnection, Task, ValueTask, MigrationBaselineFactory

### Community 122 - "Community 122"
Cohesion: 0.33
Nodes (10): Guid, DeleteReportCommand, CancellationToken, Task, DeleteReportCommandHandler, CancellationToken, Fact, Guid (+2 more)

### Community 123 - "Community 123"
Cohesion: 0.43
Nodes (7): FetchHolidaysFromWebCommand, CancellationToken, Error, Fact, IReadOnlyList, Task, HolidaySettingsViewModelFetchTests

### Community 124 - "Community 124"
Cohesion: 0.28
Nodes (8): CancellationToken, Task, DatabaseInitializer, Fact, SqliteConnection, Task, ValueTask, DatabaseInitializerTests

### Community 125 - "Community 125"
Cohesion: 0.34
Nodes (4): Fact, SyncedTo, Task, MigrationUpgradeTests

### Community 126 - "Community 126"
Cohesion: 0.15
Nodes (8): Application, App, AppBuilder, Task, Program, STAThread, AppBuilder, TestAppBuilder

### Community 127 - "Community 127"
Cohesion: 0.34
Nodes (6): DatePicker, SyncView, AvaloniaFact, CancellationToken, IProgress, SyncViewHeadlessTests

### Community 128 - "Community 128"
Cohesion: 0.13
Nodes (15): 5a — Add a Mailbox, 5b — Link the Mailbox to Your Importer, 5c — Run a Sync, Common Issues, Email filter fields (optional — required only for email automation), Getting Started with Rentier, Next Steps, Prerequisites (+7 more)

### Community 129 - "Community 129"
Cohesion: 0.13
Nodes (15): 5a — Dodavanje sandučeta, 5b — Povezivanje sandučeta sa Uvoznikom, 5c — Pokretanje sinhronizacije, Korak 1 — Preuzimanje i instalacija, Korak 2 — Kreiranje profila poreskog obveznika, Korak 3 — Podešavanje Importer-a, Korak 4 — Ručni uvoz izjave, Korak 5 — Podešavanje automatske obrade e-pošte (opciono) (+7 more)

### Community 130 - "Community 130"
Cohesion: 0.20
Nodes (9): IDocument, CancellationToken, DateOnly, IReadOnlyList, Task, CancellationToken, DateOnly, List (+1 more)

### Community 131 - "Community 131"
Cohesion: 0.13
Nodes (15): Acknowledgments, Authors, Documentation, English, Features, How It Works, License, Prerequisites (+7 more)

### Community 132 - "Community 132"
Cohesion: 0.29
Nodes (10): CancellationToken, Error, Task, GetHolidayConfQueryHandler, IHolidayRepository, CancellationToken, Fact, IReadOnlyList (+2 more)

### Community 133 - "Community 133"
Cohesion: 0.24
Nodes (5): ThemePreference, Dark, Light, System, ThemeService

### Community 135 - "Community 135"
Cohesion: 0.29
Nodes (6): BindingNotification, CultureInfo, Type, DateOnlyToStringConverter, Fact, DateOnlyToStringConverterTests

### Community 136 - "Community 136"
Cohesion: 0.14
Nodes (13): Avalonia.Headless, Avalonia.Headless.XUnit, FsCheck.Xunit.v3, Avalonia, coverlet.collector, FluentAssertions, Microsoft.Extensions.DependencyInjection, Microsoft.NET.Test.Sdk (+5 more)

### Community 137 - "Community 137"
Cohesion: 0.25
Nodes (10): ImportHolidaysFromWebCommand, CancellationToken, Error, IReadOnlyList, Task, ImportHolidaysFromWebCommandHandler, CancellationToken, Fact (+2 more)

### Community 138 - "Community 138"
Cohesion: 0.29
Nodes (5): CultureInfo, Type, NullableDateOnlyConverter, Fact, NullableDateOnlyConverterTests

### Community 139 - "Community 139"
Cohesion: 0.21
Nodes (7): FilingStatusExtensions, FilingStatus, Filed, Init, Paid, InlineData, Theory

### Community 140 - "Community 140"
Cohesion: 0.14
Nodes (14): DateOnly, Guid, ReportRowViewModel, DisplayName, EmailDate, EmailDateDisplay, FilingCount, Id (+6 more)

### Community 141 - "Community 141"
Cohesion: 0.15
Nodes (11): DateOnly, Guid, Mailbox, Cursor, Host, Id, Port, Username (+3 more)

### Community 142 - "Community 142"
Cohesion: 0.15
Nodes (12): Auto-Detect Current .NET Version, Branching & Rollback Strategy, Breaking Changes & Modernization, CI/CD Configuration Updates, Classification Rules, Discovery & Analysis Commands, .NET Upgrade Specialist, Per-Project Upgrade Flow (+4 more)

### Community 143 - "Community 143"
Cohesion: 0.15
Nodes (12): Architecture, Async/Await Standards, Clone & Build, Code Review Expectations, Commit Message Convention, Contributing to Rentier, Contribution Guidelines, Implementation Guidelines (+4 more)

### Community 144 - "Community 144"
Cohesion: 0.15
Nodes (13): Exchange Rates, Filing Deadline, Filing Lifecycle, Foreign Withholding Tax Credit (WHT), Frequently Asked Questions, Further Reading, Income Types Rentier Handles, PP-OPO Submission (+5 more)

### Community 145 - "Community 145"
Cohesion: 0.15
Nodes (13): Dodatno čitanje, Inostrani porez po odbitku (WHT), Kursni listovi, Podnošenje PP‑OPO, Poreska stopa, Pregled srpskog PP-OPO poreza, Rok prijave, Srpski praznici (+5 more)

### Community 146 - "Community 146"
Cohesion: 0.28
Nodes (5): ExcludeFromCodeCoverage, AppVersionService, DisplayVersion, Fact, AppVersionServiceTests

### Community 147 - "Community 147"
Cohesion: 0.15
Nodes (12): Ace4896.DBus.Services.Secrets, AngleSharp, CsvHelper, MailKit, Meziantou.Framework.Win32.CredentialManager, Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.Tools, Microsoft.Extensions.Http (+4 more)

### Community 148 - "Community 148"
Cohesion: 0.15
Nodes (10): CancellationToken, DateOnly, Task, CancellationToken, DateOnly, Error, Task, CancellationToken (+2 more)

### Community 149 - "Community 149"
Cohesion: 0.18
Nodes (8): Action, AssemblyMetadataAttribute, CancellationToken, Task, VelopackManagerAdapter, IsInstalled, UpdateInfo, UpdateManager

### Community 150 - "Community 150"
Cohesion: 0.45
Nodes (4): Fact, HttpStatusCode, Task, TimeAndDateHolidayScraperTests

### Community 151 - "Community 151"
Cohesion: 0.51
Nodes (5): CancellationToken, DateOnly, Fact, Task, ExchangeRateResolverTests

### Community 152 - "Community 152"
Cohesion: 0.17
Nodes (11): Absolute Rules (non-negotiable), Architecture: Clean Architecture (strict layering), Common Commands, CQRS Pattern, Directory Map, Domain Knowledge, Domain Model Enforcement, Identity (+3 more)

### Community 153 - "Community 153"
Cohesion: 0.17
Nodes (11): Analysis Order, C#/.NET Janitor, Code Modernization, Code Quality, Core Tasks, Documentation, Documentation Research, Execution Rules (+3 more)

### Community 154 - "Community 154"
Cohesion: 0.18
Nodes (7): Migration, DateOnly, MigrationBuilder, DateOnly, Guid, ModelBuilder, _0006_ExchangeRateCache

### Community 155 - "Community 155"
Cohesion: 0.17
Nodes (11): Avalonia.Controls.DataGrid, Avalonia.Desktop, Avalonia.Fonts.Inter, Avalonia.Themes.Fluent, CommunityToolkit.Mvvm, ReactiveUI, ReactiveUI.Avalonia, Avalonia (+3 more)

### Community 156 - "Community 156"
Cohesion: 0.17
Nodes (11): Microsoft.EntityFrameworkCore.InMemory, Verify.XunitV3, coverlet.collector, FluentAssertions, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.NET.Test.Sdk, NSubstitute, SQLitePCLRaw.bundle_e_sqlite3 (+3 more)

### Community 157 - "Community 157"
Cohesion: 0.38
Nodes (4): OperationCanceledException, Fact, Task, HandlerHelperTests

### Community 158 - "Community 158"
Cohesion: 0.20
Nodes (5): Action, CancellationToken, Task, IVelopackManager, IsInstalled

### Community 159 - "Community 159"
Cohesion: 0.18
Nodes (7): DateOnly, Guid, MigrationBuilder, DateOnly, Guid, ModelBuilder, _0003_HolidayConfiguration

### Community 160 - "Community 160"
Cohesion: 0.18
Nodes (7): DateOnly, Guid, MigrationBuilder, DateOnly, Guid, ModelBuilder, _0004_MailboxConfiguration

### Community 161 - "Community 161"
Cohesion: 0.18
Nodes (7): DateOnly, Guid, MigrationBuilder, DateOnly, Guid, ModelBuilder, _0007_ReportEnrichment

### Community 162 - "Community 162"
Cohesion: 0.18
Nodes (7): DateOnly, Guid, MigrationBuilder, DateOnly, Guid, ModelBuilder, _0010_SyncReplayControls

### Community 163 - "Community 163"
Cohesion: 0.18
Nodes (8): Action, CancellationToken, FakeVelopackManager, ApplyCallCount, CheckCallCount, DownloadCallCount, IsInstalled, ScheduleCallCount

### Community 164 - "Community 164"
Cohesion: 0.36
Nodes (4): DateOnly, Fact, Guid, FilingPaymentNotesTests

### Community 165 - "Community 165"
Cohesion: 0.36
Nodes (4): DateOnly, Fact, Guid, FilingTickerTests

### Community 166 - "Community 166"
Cohesion: 0.18
Nodes (10): JTBD Template, Operating Constraints (Non-Negotiable), Rentier-specific notes, Step 1: Always Ask About Users First, Step 2: Jobs-to-be-Done (JTBD) Analysis, UX/UI Designer (Avalonia, Code-First), What are their pain points?, What's their context? (+2 more)

### Community 167 - "Community 167"
Cohesion: 0.20
Nodes (5): Rentier.Infrastructure.Tests.Updates, Rentier.Infrastructure.Updates, AssemblyMetadataAttribute, Fact, UpdateFeedMetadataTests

### Community 168 - "Community 168"
Cohesion: 0.22
Nodes (6): Rentier.Infrastructure.Persistence.Migrations, Guid, MigrationBuilder, Guid, ModelBuilder, _0002_TaxpayerProfile

### Community 169 - "Community 169"
Cohesion: 0.18
Nodes (10): 2.1 Unit tests — Domain & Application, 2.2 Integration tests — Infrastructure adapters, 2.3 UI tests — ViewModels + headless views, 2.4 E2E — scenario tests, 2.5 Mutation testing (Stryker.NET), CI pipeline, Naming and structure, Rentier Testing Guide (+2 more)

### Community 170 - "Community 170"
Cohesion: 0.22
Nodes (7): Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microsoft.NET.Sdk, Microsoft.NET.Sdk, FluentAssertions, NSubstitute, Microsoft.NET.Sdk

### Community 171 - "Community 171"
Cohesion: 0.29
Nodes (8): CancellationToken, Error, IReadOnlyList, Task, GetImportersQueryHandler, Fact, Task, GetImportersQueryHandlerTests

### Community 172 - "Community 172"
Cohesion: 0.20
Nodes (9): CancellationToken, Error, IProgress, Task, SyncAllCommandHandler, CancellationToken, Error, IProgress (+1 more)

### Community 173 - "Community 173"
Cohesion: 0.22
Nodes (4): Action, CancellationToken, Task, Task

### Community 174 - "Community 174"
Cohesion: 0.31
Nodes (8): Content, FileName, Guid, ImporterId, IReadOnlyList, Task, Window, ImportDialogHelper

### Community 175 - "Community 175"
Cohesion: 0.18
Nodes (10): DateOnly, Guid, MailboxItemViewModel, DisplayName, Host, Id, LastSyncDate, LastUid (+2 more)

### Community 176 - "Community 176"
Cohesion: 0.22
Nodes (9): CancellationToken, DateOnly, HttpClient, ILogger, IReadOnlySet, List, Task, NbsExchangeRateFetcher (+1 more)

### Community 177 - "Community 177"
Cohesion: 0.20
Nodes (6): Guid, MigrationBuilder, DateOnly, Guid, ModelBuilder, _0005_ImporterConfiguration

### Community 178 - "Community 178"
Cohesion: 0.20
Nodes (6): DateOnly, MigrationBuilder, DateOnly, Guid, ModelBuilder, _0012_ReportEmailDate

### Community 179 - "Community 179"
Cohesion: 0.18
Nodes (10): coverlet.collector, FluentAssertions, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.Extensions.DependencyInjection, Microsoft.NET.Test.Sdk, NSubstitute, SQLitePCLRaw.bundle_e_sqlite3, xunit.runner.visualstudio (+2 more)

### Community 180 - "Community 180"
Cohesion: 0.25
Nodes (5): DateOnly, Fact, InlineData, Theory, ExchangeRateTests

### Community 181 - "Community 181"
Cohesion: 0.36
Nodes (4): Fact, InlineData, Theory, FilingStatusTransitionTests

### Community 182 - "Community 182"
Cohesion: 0.20
Nodes (9): Change Tracking & Saving, Data Context Design, Entity Design, Entity Framework Core Best Practices, Migrations, Performance, Querying, Security (+1 more)

### Community 183 - "Community 183"
Cohesion: 0.20
Nodes (9): 1. Discover, 2. Categorize, 3. Execute (per tier), 4. Verify before claiming done, Critical rules, NuGet Package Upgrade, References, Rentier specifics (read before upgrading anything here) (+1 more)

### Community 184 - "Community 184"
Cohesion: 0.27
Nodes (5): IObserver, ModuleInitializer, Exception, NoOpExceptionObserver, ReactiveUiTestInitializer

### Community 185 - "Community 185"
Cohesion: 0.47
Nodes (4): DateOnly, Fact, Guid, MailboxItemViewModelTests

### Community 186 - "Community 186"
Cohesion: 0.22
Nodes (9): DateOnly, Guid, PublicHoliday, Date, Id, Name, Year, EntityTypeBuilder (+1 more)

### Community 187 - "Community 187"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DateOnly, Guid, ModelBuilder, _0008_FilingsTable

### Community 188 - "Community 188"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DateOnly, Guid, ModelBuilder, _0009_FilingPaymentReference

### Community 189 - "Community 189"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DateOnly, Guid, ModelBuilder, _0013_FilingTicker

### Community 190 - "Community 190"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DateOnly, Guid, ModelBuilder, _0014_UserPreferences

### Community 191 - "Community 191"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DateOnly, Guid, ModelBuilder, _0011_FilingRateProvenance

### Community 192 - "Community 192"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DateOnly, Guid, ModelBuilder, _0015_FilingPaymentNotes

### Community 194 - "Community 194"
Cohesion: 0.22
Nodes (8): Clean Architecture + Avalonia UI Patterns for Rentier, Consuming results in ViewModels, CQRS contract, DI composition, Layer Map, Navigation model, SQLite storage, UI conventions

### Community 195 - "Community 195"
Cohesion: 0.22
Nodes (8): Assertions, Data-Driven Tests, Mocking and Isolation, Project Setup, Standard Tests, Test Organization, Test Structure, XUnit Best Practices

### Community 196 - "Community 196"
Cohesion: 0.22
Nodes (8): graphify reference: extra exports and benchmark, Step 6b - Wiki (only if --wiki flag), Step 7 - Neo4j export (only if --neo4j or --neo4j-push flag), Step 7a - FalkorDB export (only if --falkordb or --falkordb-push flag), Step 7b - SVG export (only if --svg flag), Step 7c - GraphML export (only if --graphml flag), Step 7d - MCP server (only if --mcp flag), Step 8 - Token reduction benchmark (only if total_words > 5000)

### Community 197 - "Community 197"
Cohesion: 0.22
Nodes (8): 1. Command/Query record, 2. Handler, 3. DI registration, 4. Unit tests, 5. ViewModel wiring (user-facing features), 6. Verify before claiming done, Checklist, Scaffold a New Rentier Feature

### Community 198 - "Community 198"
Cohesion: 0.22
Nodes (8): Change checklist (safety-critical), Exchange rates, Filing deadline, Filing lifecycle, Income scope, Key files, Serbian PP-OPO Tax Rules (as implemented), The computation (order and rounding are load-bearing)

### Community 200 - "Community 200"
Cohesion: 0.42
Nodes (4): FieldInfo, Fact, IEnumerable, ErrorCodesTests

### Community 201 - "Community 201"
Cohesion: 0.33
Nodes (5): NonEmptyString, Fact, PositiveInt, Property, PaymentReferenceProperties

### Community 202 - "Community 202"
Cohesion: 0.22
Nodes (8): UpdateState, Checking, Dismissed, Downloaded, Downloading, Error, Idle, UpdateAvailable

### Community 204 - "Community 204"
Cohesion: 0.25
Nodes (7): C# Async Programming Best Practices, Common Pitfalls, Exception Handling, Implementation Patterns, Naming Conventions, Performance, Return Types

### Community 205 - "Community 205"
Cohesion: 0.25
Nodes (4): Control, Rentier.Desktop, IDataTemplate, ViewLocator

### Community 206 - "Community 206"
Cohesion: 0.25
Nodes (3): Rentier.Infrastructure.Tests.Parsers, Rentier.Infrastructure.Scraping, Rentier.Infrastructure.Parsing

### Community 207 - "Community 207"
Cohesion: 0.39
Nodes (5): IReadOnlyList, CancellationToken, Fact, Task, GetMailboxesQueryHandlerTests

### Community 208 - "Community 208"
Cohesion: 0.32
Nodes (5): CultureInfo, Type, BoolToDoubleConverter, FalseValue, TrueValue

### Community 209 - "Community 209"
Cohesion: 0.29
Nodes (3): MigrationBuilder, ModelBuilder, _0001_InitialCreate

### Community 210 - "Community 210"
Cohesion: 0.46
Nodes (4): Fact, Guid, ReportType, ImporterItemViewModelTests

### Community 211 - "Community 211"
Cohesion: 0.29
Nodes (6): Async essentials, Code design rules, Error handling, Read project context first, Rentier non-negotiables, Testing (match this repository exactly)

### Community 212 - "Community 212"
Cohesion: 0.29
Nodes (5): Rentier.UnitTests.Application.Common, IReadOnlyList, List, SynchronousProgress, Entries

### Community 213 - "Community 213"
Cohesion: 0.38
Nodes (4): IValueConverter, CultureInfo, Type, FilterActiveConverter

### Community 214 - "Community 214"
Cohesion: 0.29
Nodes (5): ModelSnapshot, DateOnly, Guid, ModelBuilder, AppDbContextModelSnapshot

### Community 215 - "Community 215"
Cohesion: 0.29
Nodes (6): FuncValueConverter, DuplicateStrategyDisplayConverter, DuplicateStrategy, CreateNewRevision, ReprocessInPlace, SkipExisting

### Community 217 - "Community 217"
Cohesion: 0.33
Nodes (5): For /graphify explain, For /graphify path, graphify reference: query, path, explain, Step 0 — Constrained query expansion (REQUIRED before traversal), Step 1 — Traversal

### Community 218 - "Community 218"
Cohesion: 0.33
Nodes (5): Execution after approval, Interpreting the answer, Major Upgrade Presentation & Approval Flow, Presentation template, Research first

### Community 219 - "Community 219"
Cohesion: 0.33
Nodes (5): Handling breaking changes after a major bump, Scenario 1: Security vulnerability (CVE / CI gate failure), Scenario 2: Feature-driven upgrade, Scenario 3: Routine maintenance sweep, Upgrade Scenario Playbooks

### Community 220 - "Community 220"
Cohesion: 0.33
Nodes (5): IMultiValueConverter, CultureInfo, IList, Type, SortArrowConverter

### Community 221 - "Community 221"
Cohesion: 0.47
Nodes (3): CultureInfo, Type, IndentToMarginConverter

### Community 222 - "Community 222"
Cohesion: 0.47
Nodes (3): CultureInfo, Type, NullableIntConverter

### Community 223 - "Community 223"
Cohesion: 0.33
Nodes (4): IncomeTypeExtensions, IncomeType, Dividend, Interest

### Community 224 - "Community 224"
Cohesion: 0.47
Nodes (3): Fact, Task, ImapSyncIntegrationTests

### Community 226 - "Community 226"
Cohesion: 0.47
Nodes (4): NonNegativeInt, PositiveInt, Property, FilingDeadlineProperties

### Community 228 - "Community 228"
Cohesion: 0.40
Nodes (3): Rentier.UnitTests.Architecture, Fact, LayerDependencyTests

### Community 229 - "Community 229"
Cohesion: 0.40
Nodes (5): Option B — Email Automation via IBKR Flex Queries, Step 1 — Create a Flex Query in IBKR, Step 2 — Configure a Mailbox in Rentier, Step 3 — Configure Importer Filters, Step 4 — Run a Sync

### Community 230 - "Community 230"
Cohesion: 0.40
Nodes (5): Disclaimer, Documentation, Rentier — English Documentation, Screenshots, Serbian Documentation

### Community 231 - "Community 231"
Cohesion: 0.40
Nodes (5): Korak 1 — Kreirajte Flex Query u IBKR, Korak 2 — Konfigurišite sanduče u Rentier, Korak 3 — Konfigurišite filter Importer-a, Korak 4 — Pokrenite sinhronizaciju, Opcija B — Automatska obrada e-pošte preko IBKR Flex Queries

### Community 232 - "Community 232"
Cohesion: 0.40
Nodes (5): Dokumentacija, Odricanje od odgovornosti, Povratak na englesku dokumentaciju, Rentier — Srpska dokumentacija, Snimci ekrana

### Community 234 - "Community 234"
Cohesion: 0.40
Nodes (4): FiledCount, InitCount, PaidCount, TotalUnpaidRsd

### Community 235 - "Community 235"
Cohesion: 0.60
Nodes (3): CultureInfo, Type, ComparisonOperatorIndexConverter

### Community 236 - "Community 236"
Cohesion: 0.60
Nodes (3): CultureInfo, Type, NullableReportStatusConverter

### Community 237 - "Community 237"
Cohesion: 0.40
Nodes (3): IServiceProvider, List, StreamGeometry

### Community 238 - "Community 238"
Cohesion: 0.40
Nodes (3): PositiveInt, Property, PaginationProperties

### Community 239 - "Community 239"
Cohesion: 0.50
Nodes (3): For /graphify add, For --watch, graphify reference: add a URL and watch a folder

### Community 240 - "Community 240"
Cohesion: 0.50
Nodes (3): For git commit hook, For native CLAUDE.md integration, graphify reference: commit hook and native CLAUDE.md integration

### Community 241 - "Community 241"
Cohesion: 0.50
Nodes (3): For --cluster-only, For --update (incremental re-extraction), graphify reference: incremental update and cluster-only

### Community 242 - "Community 242"
Cohesion: 0.50
Nodes (3): sonar.exe, context7, sonarqube

### Community 243 - "Community 243"
Cohesion: 0.50
Nodes (3): CancellationToken, Error, Task

### Community 247 - "Community 247"
Cohesion: 0.67
Nodes (3): DateOnly, Guid, UpcomingDeadlineDto

## Knowledge Gaps
- **1147 isolated node(s):** `context7`, `sonar.exe`, `ErrorCodes`, `IsSuccess`, `Value` (+1142 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **22 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Result` connect `Community 8` to `Community 1`, `Community 130`, `Community 3`, `Community 4`, `Community 132`, `Community 5`, `Community 137`, `Community 9`, `Community 10`, `Community 13`, `Community 14`, `Community 16`, `Community 17`, `Community 18`, `Community 148`, `Community 20`, `Community 22`, `Community 23`, `Community 24`, `Community 25`, `Community 26`, `Community 27`, `Community 21`, `Community 29`, `Community 30`, `Community 31`, `Community 35`, `Community 38`, `Community 39`, `Community 42`, `Community 171`, `Community 172`, `Community 44`, `Community 46`, `Community 45`, `Community 176`, `Community 49`, `Community 51`, `Community 52`, `Community 54`, `Community 55`, `Community 56`, `Community 57`, `Community 60`, `Community 63`, `Community 64`, `Community 66`, `Community 70`, `Community 74`, `Community 77`, `Community 79`, `Community 81`, `Community 83`, `Community 84`, `Community 90`, `Community 93`, `Community 94`, `Community 98`, `Community 102`, `Community 104`, `Community 109`, `Community 112`, `Community 243`, `Community 117`, `Community 118`, `Community 122`, `Community 123`?**
  _High betweenness centrality (0.181) - this node is a cross-community bridge._
- **Why does `Rentier.Infrastructure.Persistence` connect `Community 11` to `Community 160`, `Community 161`, `Community 162`, `Community 191`, `Community 192`, `Community 168`, `Community 78`, `Community 209`, `Community 177`, `Community 178`, `Community 214`, `Community 154`, `Community 187`, `Community 188`, `Community 189`, `Community 190`, `Community 159`?**
  _High betweenness centrality (0.087) - this node is a cross-community bridge._
- **Why does `Rentier.Desktop.Resources` connect `Community 2` to `Community 32`, `Community 0`, `Community 250`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **What connects `context7`, `sonar.exe`, `ErrorCodes` to the rest of the system?**
  _1147 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.007547169811320755 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.05910364145658263 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.0479518540089303 - nodes in this community are weakly interconnected._