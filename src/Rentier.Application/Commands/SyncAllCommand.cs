using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Commands;

public sealed record SyncAllCommand(
    SyncParameters Parameters);
