using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

public class SyncMailboxCommandHandlerTests
{
    private static Importer MakeImporterWithMailbox(Guid mailboxId)
    {
        var importer = Importer.Create("Test Importer");
        importer.UpdateDetails("Test Importer", ReportType.IbkrCsv,
            Guid.NewGuid(), mailboxId, "", "", "", "");
        return importer;
    }

    private static Mailbox MakeMailbox(Guid id)
    {
        return Mailbox.Create("imap.example.com", 993, "user@test.com");
    }

    [Fact]
    public async Task HandleAsync_NoImportersWithMailbox_ReturnsZeroCreated()
    {
        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Importer>());

        var handler = new SyncMailboxCommandHandler(
            importerRepo,
            Substitute.For<IMailboxRepository>(),
            Substitute.For<IMailboxSyncService>());

        var result = await handler.HandleAsync(new SyncMailboxCommand(SyncParameters.Default), progress: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(0);
        result.Value.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_TwoMailboxes_CallsSyncTwice()
    {
        var mailboxId1 = Guid.NewGuid();
        var mailboxId2 = Guid.NewGuid();

        var importer1 = Importer.Create("Importer1");
        importer1.UpdateDetails("Importer1", ReportType.IbkrCsv, null, mailboxId1, "", "", "", "");
        var importer2 = Importer.Create("Importer2");
        importer2.UpdateDetails("Importer2", ReportType.IbkrCsv, null, mailboxId2, "", "", "", "");

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { importer1, importer2 });

        var mailbox1 = Mailbox.Create("imap1.test.com", 993, "u1@test.com");
        var mailbox2 = Mailbox.Create("imap2.test.com", 993, "u2@test.com");

        var mailboxRepo = Substitute.For<IMailboxRepository>();
        mailboxRepo.GetByIdAsync(mailboxId1, Arg.Any<CancellationToken>()).Returns(mailbox1);
        mailboxRepo.GetByIdAsync(mailboxId2, Arg.Any<CancellationToken>()).Returns(mailbox2);

        var syncService = Substitute.For<IMailboxSyncService>();
        syncService.SyncAsync(Arg.Any<Mailbox>(), Arg.Any<IReadOnlyList<Importer>>(),
                Arg.Any<SyncParameters>(), Arg.Any<IProgress<SyncProgressEntry>?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(new SyncResult(1, [])));

        var handler = new SyncMailboxCommandHandler(importerRepo, mailboxRepo, syncService);

        await handler.HandleAsync(new SyncMailboxCommand(SyncParameters.Default), progress: null);

        await syncService.Received(2).SyncAsync(
            Arg.Any<Mailbox>(), Arg.Any<IReadOnlyList<Importer>>(),
            Arg.Any<SyncParameters>(), Arg.Any<IProgress<SyncProgressEntry>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MissingMailbox_AddsErrorToResult()
    {
        var mailboxId = Guid.NewGuid();
        var importer = Importer.Create("Importer");
        importer.UpdateDetails("Importer", ReportType.IbkrCsv, null, mailboxId, "", "", "", "");

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { importer });

        var mailboxRepo = Substitute.For<IMailboxRepository>();
        mailboxRepo.GetByIdAsync(mailboxId, Arg.Any<CancellationToken>())
            .Returns((Mailbox?)null);

        var handler = new SyncMailboxCommandHandler(
            importerRepo, mailboxRepo, Substitute.For<IMailboxSyncService>());

        var result = await handler.HandleAsync(new SyncMailboxCommand(SyncParameters.Default), progress: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Errors.Should().HaveCount(1);
        result.Value.Errors[0].Should().Contain(mailboxId.ToString());
    }

    [Fact]
    public async Task HandleAsync_SyncServiceFailure_AggregatesError()
    {
        var mailboxId = Guid.NewGuid();
        var importer = Importer.Create("Importer");
        importer.UpdateDetails("Importer", ReportType.IbkrCsv, null, mailboxId, "", "", "", "");

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { importer });

        var mailbox = Mailbox.Create("imap.test.com", 993, "u@test.com");
        var mailboxRepo = Substitute.For<IMailboxRepository>();
        mailboxRepo.GetByIdAsync(mailboxId, Arg.Any<CancellationToken>()).Returns(mailbox);

        var syncService = Substitute.For<IMailboxSyncService>();
        syncService.SyncAsync(Arg.Any<Mailbox>(), Arg.Any<IReadOnlyList<Importer>>(),
                Arg.Any<SyncParameters>(), Arg.Any<IProgress<SyncProgressEntry>?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Failure(Error.Infrastructure("IMAP error")));

        var handler = new SyncMailboxCommandHandler(importerRepo, mailboxRepo, syncService);

        var result = await handler.HandleAsync(new SyncMailboxCommand(SyncParameters.Default), progress: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Errors.Should().Contain("IMAP error");
    }

    [Fact]
    public async Task HandleAsync_PartialSuccess_ReturnsMixedResult()
    {
        var mailboxId = Guid.NewGuid();
        var importer = Importer.Create("Importer");
        importer.UpdateDetails("Importer", ReportType.IbkrCsv, null, mailboxId, "", "", "", "");

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { importer });

        var mailbox = Mailbox.Create("imap.test.com", 993, "u@test.com");
        var mailboxRepo = Substitute.For<IMailboxRepository>();
        mailboxRepo.GetByIdAsync(mailboxId, Arg.Any<CancellationToken>()).Returns(mailbox);

        var syncService = Substitute.For<IMailboxSyncService>();
        syncService.SyncAsync(Arg.Any<Mailbox>(), Arg.Any<IReadOnlyList<Importer>>(),
                Arg.Any<SyncParameters>(), Arg.Any<IProgress<SyncProgressEntry>?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(
                new SyncResult(3, ["attachment parse error"])));

        var handler = new SyncMailboxCommandHandler(importerRepo, mailboxRepo, syncService);

        var result = await handler.HandleAsync(new SyncMailboxCommand(SyncParameters.Default), progress: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsCreated.Should().Be(3);
        result.Value.Errors.Should().Contain("attachment parse error");
    }

    [Fact]
    public async Task HandleAsync_PassesProgressToSyncService()
    {
        var mailboxId = Guid.NewGuid();
        var importer = Importer.Create("Importer");
        importer.UpdateDetails("Importer", ReportType.IbkrCsv, null, mailboxId, "", "", "", "");

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { importer });

        var mailbox = Mailbox.Create("imap.test.com", 993, "u@test.com");
        var mailboxRepo = Substitute.For<IMailboxRepository>();
        mailboxRepo.GetByIdAsync(mailboxId, Arg.Any<CancellationToken>()).Returns(mailbox);

        var syncService = Substitute.For<IMailboxSyncService>();
        syncService.SyncAsync(Arg.Any<Mailbox>(), Arg.Any<IReadOnlyList<Importer>>(),
                Arg.Any<SyncParameters>(), Arg.Any<IProgress<SyncProgressEntry>?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(new SyncResult(0, [])));

        var handler = new SyncMailboxCommandHandler(importerRepo, mailboxRepo, syncService);
        var progress = Substitute.For<IProgress<SyncProgressEntry>>();

        await handler.HandleAsync(new SyncMailboxCommand(SyncParameters.Default), progress, CancellationToken.None);

        await syncService.Received(1).SyncAsync(
            Arg.Any<Mailbox>(),
            Arg.Any<IReadOnlyList<Importer>>(),
            Arg.Any<SyncParameters>(),
            progress,
            Arg.Any<CancellationToken>());
    }
}
