// Prerequisites:
// 1. Rentier.Desktop.exe must be built and available at the path specified by RENTIER_EXE_PATH env var
// 2. A test SQLite database must be configured via RENTIER_TEST_DB_PATH env var
// 3. These tests require a Windows runner with a display
// 4. Run only via explicit CI trigger or manually: dotnet test --filter "Category=E2E"
//
// Setup: Set environment variables before running:
//   set RENTIER_EXE_PATH=path\to\Rentier.Desktop.exe
//   set RENTIER_TEST_DB_PATH=path\to\test.db

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FluentAssertions;
using Xunit;

namespace Rentier.E2E.Tests;

/// <summary>
/// End-to-end tests that launch the real Rentier desktop application.
/// ALL tests in this class are skipped by default — they require a running Windows environment
/// with Rentier.Desktop.exe built and an RENTIER_EXE_PATH environment variable set.
/// Run with: dotnet test --filter "Category=E2E"
/// </summary>
[Trait("Category", "E2E")]
public class ImportCsvAndViewFilingsE2ETest : IDisposable
{
    private Application? _app;
    private UIA3Automation? _automation;

    // All tests require RENTIER_EXE_PATH environment variable
    private static string? ExePath =>
        Environment.GetEnvironmentVariable("RENTIER_EXE_PATH");

    [Fact(Skip = "Requires running Rentier.Desktop.exe — set RENTIER_EXE_PATH env var and remove Skip to run")]
    public void LaunchApp_MainWindowOpens_TitleContainsRentier()
    {
        // Prerequisites: RENTIER_EXE_PATH must point to a built Rentier.Desktop.exe
        ExePath.Should().NotBeNullOrEmpty("RENTIER_EXE_PATH environment variable must be set");

        // Arrange
        _app = Application.Launch(ExePath!);
        _automation = new UIA3Automation();

        // Act — wait for main window (up to 10 seconds)
        var window = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));

        // Assert
        window.Should().NotBeNull();
        // window is guarded by NotBeNull() above; ! suppresses the nullable warning.
        window!.Title.Should().Contain("Rentier");

        // Cleanup
        window.Close();
    }

    [Fact(Skip = "Requires running Rentier.Desktop.exe — set RENTIER_EXE_PATH env var and remove Skip to run")]
    public void ImportCsvFlow_ValidCsvFile_ShowsFilingsInGrid()
    {
        // Prerequisites: RENTIER_EXE_PATH must point to a built Rentier.Desktop.exe
        // Expected flow:
        // 1. Launch app with test DB
        // 2. Find and click the Import button (AutomationId: "ImportButton")
        // 3. Select a test CSV file from test fixtures
        // 4. Wait for filings grid to populate
        // 5. Assert at least one row in the DataGrid (AutomationId: "FilingsGrid")
        // 6. Close app

        // TODO: Implement when E2E infrastructure is set up
        // See testing-strategy.md §12 for implementation guidance
        // Uses FlaUI.UIA3 — find controls by AutomationId set in XAML

        throw new NotImplementedException("E2E test skeleton — implement when infrastructure is ready");
    }

    public void Dispose()
    {
        try { _app?.Close(); } catch { /* ignore cleanup errors */ }
        _automation?.Dispose();
        _app?.Dispose();
    }
}
