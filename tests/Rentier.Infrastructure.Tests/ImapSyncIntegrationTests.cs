using Xunit;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Live")]
public class ImapSyncIntegrationTests
{
    [Fact(Skip = "Requires live IMAP server")]
    public async Task SyncAsync_LiveImapServer_ImportsMails()
    {
        // This test requires a live IMAP server configured with credentials.
        // Run manually after configuring a test mailbox.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires live IMAP server")]
    public async Task SyncAsync_LiveImapServer_UpdatesCursorAfterSync()
    {
        await Task.CompletedTask;
    }
}
