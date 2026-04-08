// contracts/IMailboxRepository.cs
// Full interface contract for Feature 004 — IMAP Mailbox Configuration
// Location: src/Rentier.Application/Repositories/IMailboxRepository.cs
//
// STATUS: This file ALREADY EXISTS at the path above (created by Feature 001 stub generation).
// The existing stub is COMPLETE and requires NO changes.
// This contract document is provided for reference and implementer guidance.

using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

/// <summary>
/// Repository contract for IMAP mailbox configuration persistence.
/// Defined in the Application layer; implemented in Infrastructure.
///
/// Invariants:
/// - All methods are async and accept an optional CancellationToken.
/// - GetAllAsync never returns null; returns an empty list when no mailboxes exist.
/// - AddAsync assigns persistence (EF tracks; no return value needed — Id is set by factory).
/// - UpdateAsync replaces all scalar fields and the Cursor; it does NOT touch passwords
///   (password lives in OS credential store, managed by ICredentialStore).
/// - DeleteAsync is idempotent: if the Id does not exist, it completes silently.
/// </summary>
public interface IMailboxRepository
{
    /// <summary>
    /// Returns the mailbox with the specified Id, or null if not found.
    /// </summary>
    Task<Mailbox?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all configured mailboxes. Empty list when none exist.
    /// </summary>
    Task<IReadOnlyList<Mailbox>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists a new mailbox record. The entity must have been created via Mailbox.Create(...)
    /// so that the Id is already assigned.
    /// </summary>
    Task AddAsync(Mailbox mailbox, CancellationToken ct = default);

    /// <summary>
    /// Replaces the stored mailbox record with the provided entity.
    /// Throws if the entity does not already exist (by Id).
    /// </summary>
    Task UpdateAsync(Mailbox mailbox, CancellationToken ct = default);

    /// <summary>
    /// Deletes the mailbox record for the specified Id.
    /// No-op (completes silently) if the Id does not exist.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────
// Handler wiring — how Application handlers consume this interface
// ─────────────────────────────────────────────────────────────
//
// AddMailboxCommandHandler:
//   1. Mailbox entity = Mailbox.Create(command.Host, command.Port, command.Username, command.InitialSyncDate)
//   2. if (!string.IsNullOrEmpty(command.Password))              ← CREDENTIAL FIRST (spec.md Edge Cases:
//        await _credentialStore.SaveCredentialAsync(               "credential must succeed before DB insert")
//                $"Rentier/Mailbox/{entity.Id}", command.Password, ct)
//   3. await _repository.AddAsync(entity, ct)                    ← DB insert only after credential succeeds
//   4. return Result<Guid, Error>.Success(entity.Id)
//
// UpdateMailboxCommandHandler:
//   1. existing = await _repository.GetByIdAsync(command.Id, ct)  → null → NotFound error
//   2. existing.UpdateDetails(command.Host, command.Port, command.Username, command.InitialSyncDate)
//      (mutate the tracked entity via domain method — preserves Cursor)
//   3. await _repository.UpdateAsync(existing, ct)
//   4. if (!string.IsNullOrEmpty(command.Password))
//        await _credentialStore.SaveCredentialAsync($"Rentier/Mailbox/{command.Id}", command.Password, ct)
//   5. return Result<VoidResult, Error>.Success(VoidResult.Value)
//
// DeleteMailboxCommandHandler:
//   1. await _credentialStore.DeleteCredentialAsync($"Rentier/Mailbox/{command.Id}", ct)
//      → swallow silently if key not found (credential may never have been saved)
//   2. await _repository.DeleteAsync(command.Id, ct)
//   3. return Result<VoidResult, Error>.Success(VoidResult.Value)
//
// GetMailboxesQueryHandler:
//   1. mailboxes = await _repository.GetAllAsync(ct)
//   2. dtos = mailboxes.Select(m => new MailboxDto(m.Id, m.Host, m.Port, m.Username,
//                m.InitialSyncDate, m.Cursor.LastSyncDate, m.Cursor.LastUid))
//              .ToList().AsReadOnly()
//   3. return Result<IReadOnlyList<MailboxDto>, Error>.Success(dtos)
