using Rentier.Application.DTOs;

namespace Rentier.Application.Commands;

public sealed record ProcessReportsCommand(IProgress<SyncProgressEntry>? Progress = null);
