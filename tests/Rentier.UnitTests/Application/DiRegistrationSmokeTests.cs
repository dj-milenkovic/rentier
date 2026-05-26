using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Desktop.Services;
using Xunit;

namespace Rentier.UnitTests;

public class DiRegistrationSmokeTests
{
    [Fact]
    public void ServiceCollection_WithSubstituteStubs_ResolvesAllApplicationInterfaces()
    {
        var services = new ServiceCollection();

        services.AddSingleton(Substitute.For<IFilingRepository>());
        services.AddSingleton(Substitute.For<IReportRepository>());
        services.AddSingleton(Substitute.For<IMailboxRepository>());
        services.AddSingleton(Substitute.For<IImporterRepository>());
        services.AddSingleton(Substitute.For<ITaxpayerProfileRepository>());
        services.AddSingleton(Substitute.For<IExchangeRateCacheRepository>());
        services.AddSingleton(Substitute.For<ICredentialStore>());
        services.AddSingleton(Substitute.For<IMailboxSyncService>());
        services.AddSingleton(Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>());

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IFilingRepository>().Should().NotBeNull();
        provider.GetRequiredService<IReportRepository>().Should().NotBeNull();
        provider.GetRequiredService<IMailboxRepository>().Should().NotBeNull();
        provider.GetRequiredService<IImporterRepository>().Should().NotBeNull();
        provider.GetRequiredService<ITaxpayerProfileRepository>().Should().NotBeNull();
        provider.GetRequiredService<IExchangeRateCacheRepository>().Should().NotBeNull();
        provider.GetRequiredService<ICredentialStore>().Should().NotBeNull();
        provider.GetRequiredService<IMailboxSyncService>().Should().NotBeNull();
        provider.GetRequiredService<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>().Should().NotBeNull();
    }

    // ── T036: New language/preference services resolve correctly ──────────────

    [Fact]
    public void ServiceCollection_LocalizationAndPreferenceServices_AllResolveSuccessfully()
    {
        var services = new ServiceCollection();

        // Register stubs for dependencies
        services.AddSingleton(Substitute.For<IUserPreferenceRepository>());

        // Register the actual services under test
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddTransient<
            IQueryHandler<GetUserPreferenceQuery, Result<string?, Error>>,
            GetUserPreferenceQueryHandler>();
        services.AddTransient<
            ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>,
            SetUserPreferenceCommandHandler>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ILocalizationService>().Should().NotBeNull();
        provider.GetRequiredService<IUserPreferenceRepository>().Should().NotBeNull();
        provider.GetRequiredService<IQueryHandler<GetUserPreferenceQuery, Result<string?, Error>>>()
            .Should().NotBeNull();
        provider.GetRequiredService<ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>>>()
            .Should().NotBeNull();
    }

    [Fact]
    public void ServiceCollection_FilingHandlers_AllResolveSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IFilingRepository>());

        services.AddTransient<
            IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>,
            GetFilingsQueryHandler>();
        services.AddTransient<
            ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>,
            UpdateFilingStatusCommandHandler>();
        services.AddTransient<
            ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>,
            UpdatePaymentReferenceCommandHandler>();
        services.AddTransient<
            ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>,
            DeleteFilingCommandHandler>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>()
            .Should().NotBeNull();
        provider.GetRequiredService<ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>>()
            .Should().NotBeNull();
        provider.GetRequiredService<ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>>()
            .Should().NotBeNull();
        provider.GetRequiredService<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>>()
            .Should().NotBeNull();
    }

    [Fact]
    public void ServiceCollection_ReportHandlers_AllResolveSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IReportRepository>());
        services.AddSingleton(Substitute.For<IFilingRepository>());
        services.AddSingleton(Substitute.For<IImporterRepository>());
        services.AddSingleton(Substitute.For<IStatementParser>());
        services.AddSingleton(Substitute.For<ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>>());

        services.AddTransient<
            IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>,
            GetReportsQueryHandler>();
        services.AddTransient<
            ICommandHandler<ImportReportCommand, Result<Guid, Error>>,
            ImportReportCommandHandler>();
        services.AddTransient<
            ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>,
            DeleteReportCommandHandler>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>()
            .Should().NotBeNull();
        provider.GetRequiredService<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>()
            .Should().NotBeNull();
        provider.GetRequiredService<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>>()
            .Should().NotBeNull();
    }
}
