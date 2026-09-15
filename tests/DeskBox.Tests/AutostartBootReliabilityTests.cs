using System.Text;
using DeskBox.Services;

namespace DeskBox.Tests;

/// <summary>
/// Pins the autostart boot-reliability fixes: schtasks pipe decoding (user
/// names and install paths with non-ASCII characters must survive the
/// register→verify round trip), the bounded restart policy on the logon task,
/// and the one-time default not being consumed by a failed attempt.
/// </summary>
public sealed class AutostartBootReliabilityTests
{
    private const string ExecutablePath =
        @"C:\Program Files\DeskBox\DeskBox.exe";

    // "auto-start user" in Chinese: a non-ASCII account name is the exact
    // shape that came back as mojibake from schtasks /Query /XML pipes.
    private const string NonAsciiAccount = "DESKTOP-ABC\\\u81ea\u542f\u7528\u6237";

    [Fact]
    public void DecodeSchtasksOutput_ReadsUtf8PipeBytesAsText()
    {
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(
            $"<?xml version=\"1.0\" encoding=\"UTF-16\"?><Task>{NonAsciiAccount}</Task>");

        string decoded = DirectStartupTaskBackend.DecodeSchtasksOutput(
            Encoding.Latin1.GetString(utf8Bytes));

        Assert.Contains(NonAsciiAccount, decoded, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeSchtasksOutput_ReadsUtf16OutputWithBom()
    {
        byte[] utf16Bytes = Encoding.Unicode.GetBytes(
            $"<Task>{NonAsciiAccount}</Task>");
        byte[] withBom = [0xFF, 0xFE, .. utf16Bytes];

        string decoded = DirectStartupTaskBackend.DecodeSchtasksOutput(
            Encoding.Latin1.GetString(withBom));

        Assert.Contains(NonAsciiAccount, decoded, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeSchtasksOutput_AcceptsPureAscii()
    {
        const string ascii = "<?xml version=\"1.0\"?><Task>SIMON\\simon</Task>";

        string decoded = DirectStartupTaskBackend.DecodeSchtasksOutput(
            Encoding.Latin1.GetString(Encoding.ASCII.GetBytes(ascii)));

        Assert.Equal(ascii, decoded);
    }

    [Fact]
    public void DecodeSchtasksOutput_NonUtf8BytesFallBackToTheActiveCodePage()
    {
        // 0xFF is invalid in UTF-8 on every host and outside the GBK lead-byte
        // range, so the decoder must fall back to the ANSI code page instead
        // of throwing; the surrounding ASCII survives on every code page.
        byte[] ansiOnly = [0x41, 0xFF, 0x42];

        string decoded = DirectStartupTaskBackend.DecodeSchtasksOutput(
            Encoding.Latin1.GetString(ansiOnly));

        Assert.Equal(3, decoded.Length);
        Assert.StartsWith("A", decoded, StringComparison.Ordinal);
        Assert.EndsWith("B", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public void SchtasksRunner_CapturesBytesAndPinsTheWorkingDirectory()
    {
        string backend = File.ReadAllText(
            TestPaths.FromRepository("src/DeskBox/Services/DirectStartupTaskBackend.cs"));

        // The pipe must be captured byte-preserving so the decoder can probe:
        // schtasks transcodes redirected output to the active console code
        // page while the XML declaration claims UTF-16.
        Assert.Contains(
            "StandardOutputEncoding = Encoding.Latin1",
            backend,
            StringComparison.Ordinal);
        Assert.Contains(
            "StandardErrorEncoding = Encoding.Latin1",
            backend,
            StringComparison.Ordinal);
        // The inherited working directory can be an installer temp folder that
        // was already deleted, which fails the child launch outright.
        Assert.Contains(
            "WorkingDirectory = AppContext.BaseDirectory",
            backend,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TaskXml_RegistersABoundedRestartPolicy()
    {
        string xml = DirectStartupTaskBackend.BuildTaskXml(
            ExecutablePath,
            "S-1-5-21-1000-1001-1002-1003");

        Assert.Contains(
            "<Interval>PT1M</Interval>",
            xml,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Count>3</Count>",
            xml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IsPreferred_RequiresTheRestartPolicySoLegacyTasksUpgrade()
    {
        // IsPreferred also matches the task name against the current user's
        // SID, so the positive case must use the real identity.
        using System.Security.Principal.WindowsIdentity identity =
            System.Security.Principal.WindowsIdentity.GetCurrent();
        string userSid = identity.User?.Value ?? string.Empty;

        string xml = DirectStartupTaskBackend.BuildTaskXml(
            ExecutablePath,
            userSid);
        var document = System.Xml.Linq.XDocument.Parse(xml);
        System.Xml.Linq.XNamespace ns = document.Root!.Name.Namespace;
        document.Root!
            .Element(ns + "Settings")!
            .Element(ns + "RestartOnFailure")!
            .Remove();

        DirectStartupTaskRegistration legacyRegistration =
            DirectStartupTaskBackend.ParseTaskXml(document.ToString());

        Assert.False(new DirectStartupTaskBackend().IsPreferred(
            legacyRegistration,
            ExecutablePath));

        DirectStartupTaskRegistration currentRegistration =
            DirectStartupTaskBackend.ParseTaskXml(xml);
        Assert.True(new DirectStartupTaskBackend().IsPreferred(
            currentRegistration,
            ExecutablePath));
    }

    [Fact]
    public void ShouldMarkApplied_KeepsTheDefaultRetriableOnlyAfterFailure()
    {
        Assert.False(AutoStartDefaultPolicy.ShouldMarkApplied(
            StartupRegistrationState.BlockedOrFailed));

        Assert.True(AutoStartDefaultPolicy.ShouldMarkApplied(
            StartupRegistrationState.Enabled));
        Assert.True(AutoStartDefaultPolicy.ShouldMarkApplied(
            StartupRegistrationState.Pending));
        Assert.True(AutoStartDefaultPolicy.ShouldMarkApplied(
            StartupRegistrationState.NotRegistered));
        Assert.True(AutoStartDefaultPolicy.ShouldMarkApplied(
            StartupRegistrationState.DisabledByUser));
        Assert.True(AutoStartDefaultPolicy.ShouldMarkApplied(
            StartupRegistrationState.DisabledByTaskScheduler));
        Assert.True(AutoStartDefaultPolicy.ShouldMarkApplied(
            StartupRegistrationState.PathMismatch));
    }

    [Fact]
    public void ApplyDefaultOnce_DoesNotConsumeTheDefaultOnFailure()
    {
        string app = File.ReadAllText(
            TestPaths.FromRepository("src/DeskBox/App.xaml.cs"));

        Assert.Contains(
            "if (AutoStartDefaultPolicy.ShouldMarkApplied(effective))",
            app,
            StringComparison.Ordinal);
    }
}
