# Clarify — 011 Filing Generation Pipeline

## Decisions

| # | Question | Decision |
|---|----------|----------|
| 1 | Where is ProcessReportsCommandHandler? | Application layer. Uses injected IReportRepository, IImporterRepository, IFilingRepository, IExchangeRateFetcher, IHolidayRepository. |
| 2 | How does rate provider delegate work? | Handler builds `Func<DateOnly, string, Task<ExchangeRate>>` that wraps `IExchangeRateFetcher.FetchRateAsync`. On Failure, throws to skip filing. Cross-rate: try currency direct; if not found, try USD NBS × IBKR embedded rate. |
| 3 | How does FilingDeadlineCalculator get holidays? | Handler loads `PublicHoliday` records via `IHolidayRepository.GetAllAsync()`, builds `HolidayConf`. |
| 4 | Duplicate income detection? | Check `IFilingRepository.ExistsByIncomeAsync(taxpayerProfileId, payingEntity, incomeDate, grossIncomeRsd)` before inserting. Skip if exists (idempotent re-runs). |
| 5 | Parse failure handling? | Set `report.Status = Error` (new enum value on ReportStatus). Do NOT create any Filing for that report. Log error message in result. |
| 6 | Exchange rate not found? | Skip that income record, log error, continue with next record. Report stays Init until all records processed; if any fail, report becomes Error. |
| 7 | TaxpayerProfileId on Filing? | From `Importer.TaxpayerProfileId`. If null, skip report with error. |
| 8 | Filing.TaxPeriod vs IncomeDate? | Use `IncomeDate` as `TaxPeriod` when constructing Filing. They are semantically equivalent at this stage. |
| 9 | ProcessReportsResult? | `record ProcessReportsResult(int FilingsCreated, int ReportsProcessed, int ReportsErrored, IReadOnlyList<string> Errors)` |
| 10 | Filing.IncomeDate vs Filing.TaxPeriod | Add `IncomeDate` as separate field on Filing entity. Keep TaxPeriod for backward compat. Set both = dividend/interest date. |
| 11 | Report.UpdateAsync exists? | Add to IReportRepository: `UpdateAsync(Report, CT)`. Needed to set Status=Processed/Error. |
| 12 | IFilingRepository.GetByReportIdAsync? | Add for completeness (used in dedup check too). |
| 13 | Interest WHT? | IBKR interest typically has no WHT. `WithholdingTaxRecord` matched by (date, entity). If none matched, whtAmount=0. |
