using Rentier.Domain.Entities;

namespace Rentier.Application.Commands;

public sealed record UpdateFilingStatusCommand(Guid FilingId, FilingStatus NewStatus);
