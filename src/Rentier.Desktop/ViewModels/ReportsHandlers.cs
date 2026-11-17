using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Bundles the Application-layer handlers <see cref="ReportsViewModel"/> depends on.
/// Keeps the ViewModel constructor within the 7-parameter limit (Sonar S107) without
/// hiding any dependency: every member is resolved from DI.
/// </summary>
public sealed record ReportsHandlers(
    IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>> GetReports,
    ICommandHandler<ImportReportCommand, Result<Guid, Error>> ImportReport,
    ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>> DeleteReport,
    ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>> BulkDeleteReports);
