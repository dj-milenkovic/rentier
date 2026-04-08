using Rentier.Application.DTOs;

namespace Rentier.Application.Commands;

public sealed record SyncMailboxCommand(IProgress<SyncProgress>? Progress = null);
