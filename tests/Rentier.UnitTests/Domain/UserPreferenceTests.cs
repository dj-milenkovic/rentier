using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests.Domain;

public class UserPreferenceTests
{
    // ── Valid construction ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidKeyValue_SetsProperties()
    {
        var pref = new UserPreference("Language", "en");

        pref.Key.Should().Be("Language");
        pref.Value.Should().Be("en");
    }

    [Fact]
    public void Constructor_TrimsKey_WhenWhitespaceWrapped()
    {
        var pref = new UserPreference("  Language  ", "en");

        pref.Key.Should().Be("Language");
    }

    // ── Key constraints ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_EmptyKey_ThrowsDomainException()
    {
        Action act = () => _ = new UserPreference("", "en");

        act.Should().Throw<DomainException>()
            .WithMessage("*key*");
    }

    [Fact]
    public void Constructor_WhitespaceKey_ThrowsDomainException()
    {
        Action act = () => _ = new UserPreference("   ", "en");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_KeyExceeds100Chars_ThrowsDomainException()
    {
        var longKey = new string('x', 101);
        Action act = () => _ = new UserPreference(longKey, "en");

        act.Should().Throw<DomainException>()
            .WithMessage("*100*");
    }

    [Fact]
    public void Constructor_Key100Chars_Succeeds()
    {
        var key = new string('x', 100);
        var pref = new UserPreference(key, "en");

        pref.Key.Should().HaveLength(100);
    }

    // ── Value constraints ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullValue_ThrowsArgumentNullException()
    {
        Action act = () => _ = new UserPreference("Language", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValueExceeds500Chars_ThrowsDomainException()
    {
        var longValue = new string('x', 501);
        Action act = () => _ = new UserPreference("Language", longValue);

        act.Should().Throw<DomainException>()
            .WithMessage("*500*");
    }

    [Fact]
    public void Constructor_Value500Chars_Succeeds()
    {
        var value = new string('x', 500);
        var pref = new UserPreference("Language", value);

        pref.Value.Should().HaveLength(500);
    }

    [Fact]
    public void Constructor_EmptyValue_Succeeds()
    {
        var pref = new UserPreference("Language", "");

        pref.Value.Should().BeEmpty();
    }

    // ── UpdateValue ───────────────────────────────────────────────────────────

    [Fact]
    public void UpdateValue_ValidValue_UpdatesValue()
    {
        var pref = new UserPreference("Language", "en");
        pref.UpdateValue("sr-Latn");

        pref.Value.Should().Be("sr-Latn");
    }

    [Fact]
    public void UpdateValue_NullValue_ThrowsArgumentNullException()
    {
        var pref = new UserPreference("Language", "en");
        Action act = () => pref.UpdateValue(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateValue_ValueExceeds500Chars_ThrowsDomainException()
    {
        var pref = new UserPreference("Language", "en");
        var longValue = new string('x', 501);
        Action act = () => pref.UpdateValue(longValue);

        act.Should().Throw<DomainException>()
            .WithMessage("*500*");
    }
}
