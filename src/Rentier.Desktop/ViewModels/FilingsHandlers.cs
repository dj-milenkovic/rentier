using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Bundles the Application-layer handlers <see cref="FilingsViewModel"/> depends on.
/// Keeps the ViewModel constructor within the 7-parameter limit (Sonar S107) without
/// hiding any dependency: every member is resolved from DI exactly as before.
/// </summary>
public sealed record FilingsHandlers(
    IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> GetFilings,
    ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>> UpdateStatus,
    ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>> UpdateReference,
    ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>> DeleteFiling,
    ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>> ExportFiling,
    ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>> BulkDeleteFilings);
