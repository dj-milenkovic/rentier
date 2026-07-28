using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using FluentAssertions;
using NSubstitute;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Resources;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

public class ManualFilingViewModelTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly DateOnly TestDate = new(2024, 6, 17);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TaxpayerProfileDto MakeProfileDto()
        => new TaxpayerProfileDto(ProfileId, "1234567890123", "Test User", "Test Address", "00001", null, null);

    private static ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>
        MockCalculateHandler(ManualFilingPreviewDto? dto = null)
    {
        var h = Substitute.For<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>>();
        h.HandleAsync(Arg.Any<CalculateManualFilingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ManualFilingPreviewDto, Error>.Success(
                dto ?? MakePreviewDto()));
        return h;
    }

    private static ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>
        MockCreateHandler(bool success = true)
    {
        var h = Substitute.For<ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>>();
        h.HandleAsync(Arg.Any<CreateManualFilingCommand>(), Arg.Any<CancellationToken>())
            .Returns(success
                ? Result<Guid, Error>.Success(Guid.NewGuid())
                : Result<Guid, Error>.Failure(new Error("FILING_CREATE_DUPLICATE", "A filing with the same details already exists")));
        return h;
    }

    private static IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>
        MockProfileQueryHandler(TaxpayerProfileDto? dto = null)
    {
        var h = Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
        h.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaxpayerProfileDto?, Error>.Success(dto ?? MakeProfileDto()));
        return h;
    }

    private static ManualFilingPreviewDto MakePreviewDto()
        => new ManualFilingPreviewDto(
            GrossIncomeRsd: 10850.00m,
            WhtPaidRsd: 1627.50m,
            GrossTaxPayableRsd: 1627.50m,
            TaxPayableRsd: 0.00m,
            FilingDeadline: new DateOnly(2024, 7, 17),
            ExchangeRateValue: 108.50m,
            ExchangeRateSourceDate: TestDate,
            ExchangeRateSourceType: ExchangeRateSourceType.Exact);

    private static ManualFilingViewModel CreateVm(
        ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>? calcHandler = null,
        ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>? createHandler = null,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>? profileQueryHandler = null,
        Action? navigateBack = null)
        => new ManualFilingViewModel(
            calcHandler ?? MockCalculateHandler(),
            createHandler ?? MockCreateHandler(),
            profileQueryHandler ?? MockProfileQueryHandler(),
            navigateBack ?? (() => { }),
            ImmediateSequencer.Instance);

    private static ManualFilingViewModel CreateFilledVm(
        ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>? calcHandler = null,
        ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>? createHandler = null,
        IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>? profileQueryHandler = null,
        Action? navigateBack = null)
    {
        var vm = CreateVm(calcHandler, createHandler, profileQueryHandler, navigateBack);
        vm.Ticker = "AAPL";
        vm.IncomeDate = new DateTimeOffset(TestDate.ToDateTime(TimeOnly.MinValue));
        vm.GrossAmountText = "100.00";
        vm.NetReceivedText = "85.00";
        return vm;
    }

    // ── Initial State (US4/US5) ───────────────────────────────────────────────

    [Fact]
    public void Constructor_InitialState_PreviewIsNull()
    {
        var vm = CreateVm();
        vm.Preview.Should().BeNull();
    }

    [Fact]
    public void Constructor_InitialState_ErrorMessageIsNull()
    {
        var vm = CreateVm();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Constructor_InitialState_IsLoadingIsFalse()
    {
        var vm = CreateVm();
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitialState_SaveCommandCannotExecute()
    {
        var vm = CreateVm();
        bool canSave = false;
        vm.SaveCommand.CanExecute.Subscribe(x => canSave = x);

        canSave.Should().BeFalse();
    }

    [Fact]
    public void Constructor_EmptyTicker_CalculateCommandCannotExecute()
    {
        var vm = CreateVm();
        bool canCalc = true;
        vm.CalculateCommand.CanExecute.Subscribe(x => canCalc = x);
        // Ticker is empty by default
        canCalc.Should().BeFalse();
    }

    // ── Calculate → Preview (US1/US4) ─────────────────────────────────────────

    [Fact]
    public async Task CalculateCommand_Execute_SetsPreviewOnSuccess()
    {
        var vm = CreateFilledVm();

        await vm.CalculateCommand.Execute();

        vm.Preview.Should().NotBeNull();
    }

    [Fact]
    public async Task CalculateCommand_Execute_EnablesSaveCommand()
    {
        var vm = CreateFilledVm();
        bool canSave = false;

        await vm.CalculateCommand.Execute();
        vm.SaveCommand.CanExecute.Subscribe(x => canSave = x);

        canSave.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateCommand_Execute_PreviewHasAllFields()
    {
        var dto = MakePreviewDto();
        var vm = CreateFilledVm(calcHandler: MockCalculateHandler(dto));

        await vm.CalculateCommand.Execute();

        vm.Preview.Should().NotBeNull();
        vm.Preview!.GrossIncomeRsd.Should().Be(dto.GrossIncomeRsd);
        vm.Preview.WhtPaidRsd.Should().Be(dto.WhtPaidRsd);
        vm.Preview.GrossTaxPayableRsd.Should().Be(dto.GrossTaxPayableRsd);
        vm.Preview.TaxPayableRsd.Should().Be(dto.TaxPayableRsd);
        vm.Preview.FilingDeadline.Should().Be(dto.FilingDeadline);
        vm.Preview.ExchangeRateValue.Should().Be(dto.ExchangeRateValue);
        vm.Preview.ExchangeRateSourceDate.Should().Be(dto.ExchangeRateSourceDate);
        vm.Preview.ExchangeRateSourceType.Should().Be(dto.ExchangeRateSourceType);
    }

    [Fact]
    public async Task CalculateCommand_Execute_SetsIsLoadingDuringExecution()
    {
        var loadingStates = new List<bool>();
        var tcs = new TaskCompletionSource<Result<ManualFilingPreviewDto, Error>>();
        var calcHandler = Substitute.For<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>>();
        calcHandler.HandleAsync(Arg.Any<CalculateManualFilingCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => tcs.Task);

        var vm = CreateFilledVm(calcHandler: calcHandler);
        vm.WhenAnyValue(x => x.IsLoading).Subscribe(v => loadingStates.Add(v));

        var execTask = vm.CalculateCommand.Execute().ToTask(TestContext.Current.CancellationToken);
        tcs.SetResult(Result<ManualFilingPreviewDto, Error>.Success(MakePreviewDto()));
        await execTask;

        loadingStates.Should().Contain(true);
        vm.IsLoading.Should().BeFalse();
    }

    // ── Calculate → Error (US3) ──────────────────────────────────────────────

    [Fact]
    public async Task CalculateCommand_HandlerReturnsError_SetsErrorMessage()
    {
        var calcHandler = Substitute.For<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>>();
        calcHandler.HandleAsync(Arg.Any<CalculateManualFilingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ManualFilingPreviewDto, Error>.Failure(new Error("RATE_NOT_FOUND", "No rate available")));

        var vm = CreateFilledVm(calcHandler: calcHandler);

        await vm.CalculateCommand.Execute();

        vm.ErrorMessage.Should().Be("No rate available");
        vm.Preview.Should().BeNull();
    }

    // ── Save Flow (US1) ───────────────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_Execute_CallsNavigateBack()
    {
        var navigateCalled = false;
        var vm = CreateFilledVm(navigateBack: () => navigateCalled = true);

        await vm.CalculateCommand.Execute();
        await vm.SaveCommand.Execute();

        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task SaveCommand_Execute_CallsCreateHandler()
    {
        var createHandler = MockCreateHandler(success: true);
        var vm = CreateFilledVm(createHandler: createHandler);

        await vm.CalculateCommand.Execute();
        await vm.SaveCommand.Execute();

        await createHandler.Received(1).HandleAsync(
            Arg.Any<CreateManualFilingCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_DuplicateError_SetsErrorMessageAndNoNavigation()
    {
        var navigateCalled = false;
        var createHandler = MockCreateHandler(success: false);
        var vm = CreateFilledVm(
            createHandler: createHandler,
            navigateBack: () => navigateCalled = true);

        await vm.CalculateCommand.Execute();
        await vm.SaveCommand.Execute();

        vm.ErrorMessage.Should().Be("A filing with the same details already exists");
        navigateCalled.Should().BeFalse();
    }

    // ── Preview Clearing on Input Change (US4) ────────────────────────────────

    [Fact]
    public async Task TickerChange_AfterCalculate_ClearsPreview()
    {
        var vm = CreateFilledVm();
        await vm.CalculateCommand.Execute();
        vm.Preview.Should().NotBeNull();

        vm.Ticker = "MSFT";

        vm.Preview.Should().BeNull();
    }

    [Fact]
    public async Task GrossAmountChange_AfterCalculate_ClearsPreview()
    {
        var vm = CreateFilledVm();
        await vm.CalculateCommand.Execute();

        vm.GrossAmountText = "200.00";

        vm.Preview.Should().BeNull();
    }

    [Fact]
    public async Task IncomeDateChange_AfterCalculate_ClearsPreview()
    {
        var vm = CreateFilledVm();
        await vm.CalculateCommand.Execute();

        vm.IncomeDate = new DateTimeOffset(new DateTime(2024, 7, 1));

        vm.Preview.Should().BeNull();
    }

    [Fact]
    public async Task CurrencyChange_AfterCalculate_ClearsPreview()
    {
        var vm = CreateFilledVm();
        await vm.CalculateCommand.Execute();

        vm.SelectedCurrency = "EUR";

        vm.Preview.Should().BeNull();
    }

    [Fact]
    public async Task NetReceivedChange_AfterCalculate_ClearsPreview()
    {
        var vm = CreateFilledVm();
        await vm.CalculateCommand.Execute();

        vm.NetReceivedText = "90.00";

        vm.Preview.Should().BeNull();
    }

    [Fact]
    public async Task IncomeTypeChange_AfterCalculate_ClearsPreview()
    {
        var vm = CreateFilledVm();
        await vm.CalculateCommand.Execute();

        vm.SelectedIncomeType = IncomeType.Interest;

        vm.Preview.Should().BeNull();
    }

    // ── Preview clears → SaveCommand disabled (US4) ───────────────────────────

    [Fact]
    public async Task TickerChange_AfterCalculate_DisablesSaveCommand()
    {
        var vm = CreateFilledVm();
        bool canSave = false;

        await vm.CalculateCommand.Execute();
        vm.Ticker = "MSFT";
        vm.SaveCommand.CanExecute.Subscribe(x => canSave = x);

        canSave.Should().BeFalse();
    }

    // ── Cancel Navigation (US5) ───────────────────────────────────────────────

    [Fact]
    public async Task CancelCommand_Execute_InvokesNavigateBack()
    {
        var navigateCalled = false;
        var vm = CreateVm(navigateBack: () => navigateCalled = true);

        await vm.CancelCommand.Execute();

        navigateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CancelCommand_AlwaysEnabled_EvenWithEmptyForm()
    {
        var vm = CreateVm();
        bool canRun = false;
        vm.CancelCommand.CanExecute.Subscribe(x => canRun = x);

        canRun.Should().BeTrue();
    }

    [Fact]
    public async Task CancelCommand_Execute_DoesNotCallCreateHandler()
    {
        var createHandler = MockCreateHandler();
        var vm = CreateVm(createHandler: createHandler);

        await vm.CancelCommand.Execute();

        await createHandler.DidNotReceive()
            .HandleAsync(Arg.Any<CreateManualFilingCommand>(), Arg.Any<CancellationToken>());
    }

    // ── Missing Profile (US3) ─────────────────────────────────────────────────

    [Fact]
    public void WhenActivated_NoProfile_SetsErrorMessage()
    {
        var profileHandler = Substitute.For<IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>>();
        profileHandler.HandleAsync(Arg.Any<GetTaxpayerProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<TaxpayerProfileDto?, Error>.Success(null));

        var vm = CreateVm(profileQueryHandler: profileHandler);

        // With ImmediateScheduler, the WhenActivated observable fires synchronously
        using var _ = vm.Activator.Activate();
        vm.ErrorMessage.Should().NotBeNull();
    }

    // ── Payment Notes (field 3.3 export) ──────────────────────────────────────

    [Fact]
    public async Task SaveCommand_WithPaymentNotesText_SendsTrimmedPaymentNotesToHandler()
    {
        CreateManualFilingCommand? capturedCmd = null;
        var createHandler = Substitute.For<ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>>();
        createHandler.HandleAsync(Arg.Do<CreateManualFilingCommand>(c => capturedCmd = c), Arg.Any<CancellationToken>())
            .Returns(Result<Guid, Error>.Success(Guid.NewGuid()));

        var vm = CreateFilledVm(createHandler: createHandler);
        vm.PaymentNotesText = "  Isplata na brokerski racun  ";

        await vm.CalculateCommand.Execute();
        await vm.SaveCommand.Execute();

        capturedCmd.Should().NotBeNull();
        capturedCmd!.PaymentNotes.Should().Be("Isplata na brokerski racun");
    }

    [Fact]
    public async Task SaveCommand_WithBlankPaymentNotesText_SendsNullPaymentNotesToHandler()
    {
        CreateManualFilingCommand? capturedCmd = null;
        var createHandler = Substitute.For<ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>>();
        createHandler.HandleAsync(Arg.Do<CreateManualFilingCommand>(c => capturedCmd = c), Arg.Any<CancellationToken>())
            .Returns(Result<Guid, Error>.Success(Guid.NewGuid()));

        var vm = CreateFilledVm(createHandler: createHandler);

        await vm.CalculateCommand.Execute();
        await vm.SaveCommand.Execute();

        capturedCmd.Should().NotBeNull();
        capturedCmd!.PaymentNotes.Should().BeNull();
    }

    [Fact]
    public void PaymentNotesTextChange_MarksFormDirty()
    {
        var vm = CreateVm();

        vm.PaymentNotesText = "Isplata na brokerski racun";

        vm.IsDirty.Should().BeTrue();
    }

    // ── Rate Source Display (US4) ─────────────────────────────────────────────

    [Fact]
    public async Task RateSourceDisplay_ExactRateSourcePreview_ReturnsExactFormattedText()
    {
        var dto = MakePreviewDto();
        var vm = CreateFilledVm(calcHandler: MockCalculateHandler(dto));

        await vm.CalculateCommand.Execute();

        vm.RateSourceDisplay.Should().Be(
            string.Format(Strings.ManualFiling_RateSource_Exact, TestDate.ToString("yyyy-MM-dd")));
    }

    [Fact]
    public async Task RateSourceDisplay_FallbackRateSourcePreview_ReturnsFallbackFormattedText()
    {
        var dto = MakePreviewDto() with { ExchangeRateSourceType = ExchangeRateSourceType.Fallback };
        var vm = CreateFilledVm(calcHandler: MockCalculateHandler(dto));

        await vm.CalculateCommand.Execute();

        vm.RateSourceDisplay.Should().Be(
            string.Format(Strings.ManualFiling_RateSource_Fallback, TestDate.ToString("yyyy-MM-dd")));
    }

    // ── No-WHT path through ViewModel (US2) ──────────────────────────────────

    [Fact]
    public async Task CalculateCommand_EmptyNetReceived_SendsNullNetReceivedToHandler()
    {
        CalculateManualFilingCommand? capturedCmd = null;
        var calcHandler = Substitute.For<ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>>();
        calcHandler.HandleAsync(Arg.Do<CalculateManualFilingCommand>(c => capturedCmd = c), Arg.Any<CancellationToken>())
            .Returns(Result<ManualFilingPreviewDto, Error>.Success(MakePreviewDto()));

        var vm = CreateFilledVm(calcHandler: calcHandler);
        vm.NetReceivedText = "";

        await vm.CalculateCommand.Execute();

        capturedCmd.Should().NotBeNull();
        capturedCmd!.NetReceived.Should().BeNull();
    }
}
