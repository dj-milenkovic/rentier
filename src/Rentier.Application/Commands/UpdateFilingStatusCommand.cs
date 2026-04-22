using Rentier.Domain.Enums;

namespace Rentier.Application.Commands;

public sealed record UpdateFilingStatusCommand(Guid FilingId, FilingStatus NewStatus);
