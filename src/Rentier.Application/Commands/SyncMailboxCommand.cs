using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Commands;

public sealed record SyncMailboxCommand(SyncParameters Parameters);
