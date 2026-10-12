using WinSense.App.ViewModels;
using WinSense.Core.Keyboard;

namespace WinSense.Core.Tests;

public class NumLockFlagsTests
{
    [Theory]
    [InlineData("2", true)]
    [InlineData("2147483650", true)]  // 0x80000002, what Windows writes with Fast Startup
    [InlineData("2147483648", false)] // 0x80000000
    [InlineData("0", false)]
    [InlineData(null, false)]
    [InlineData("lixo", false)]
    public void HasNumLock(string? value, bool expected) =>
        Assert.Equal(expected, NumLockAtStartup.HasNumLock(value));

    [Fact]
    public void Enable_KeepsFastStartupFlag() =>
        Assert.Equal("2147483650", NumLockAtStartup.WithNumLock("2147483648", true));

    [Fact]
    public void Disable_KeepsOtherLocks() =>
        Assert.Equal("1", NumLockAtStartup.WithNumLock("3", false)); // Caps Lock (1) stays

    [Fact]
    public void Enable_FromMissingValue() =>
        Assert.Equal("2", NumLockAtStartup.WithNumLock(null, true));
}

public class KeyboardViewModelTests
{
    private sealed class FakeNumLock(bool session, bool? lockScreen = null) : INumLockAtStartup
    {
        public NumLockState State { get; private set; } = new(session, lockScreen ?? session);
        public bool Fail { get; init; }
        public int Writes { get; private set; }

        public NumLockState Read() => State;

        public void SetEnabled(bool enabled)
        {
            if (Fail)
                throw new UnauthorizedAccessException("sem permissão");
            Writes++;
            State = new NumLockState(enabled, enabled);
        }
    }

    private static KeyboardViewModel Create(INumLockAtStartup numLock) => new(numLock);

    [Fact]
    public async Task Load_ShowsCurrentNumLock()
    {
        var vm = Create(new FakeNumLock(true));

        await vm.LoadAsync();

        Assert.True(vm.IsNumLockOnSelected);
        Assert.False(vm.IsNumLockOffSelected);
        Assert.True(vm.CanChangeNumLock);
    }

    [Fact]
    public async Task Mixed_SelectsSessionValueAndWarnsAboutLockScreen()
    {
        // The author's machine: on after signing in, off on the lock screen.
        var vm = Create(new FakeNumLock(session: true, lockScreen: false));

        await vm.LoadAsync();

        Assert.True(vm.IsNumLockOnSelected);
        Assert.True(vm.IsNumLockMixed);
        Assert.Contains("tela de bloqueio", vm.NumLockStatus);
        Assert.Contains("desligado", vm.NumLockStatus);
    }

    [Fact]
    public async Task MatchLockScreen_AppliesSessionValueToBoth()
    {
        var numLock = new FakeNumLock(session: true, lockScreen: false);
        var vm = Create(numLock);
        await vm.LoadAsync();

        await vm.MatchLockScreenCommand.ExecuteAsync(null);

        Assert.Equal(new NumLockState(true, true), numLock.State);
        Assert.False(vm.IsNumLockMixed);
        Assert.True(vm.IsNumLockOnSelected);
    }

    [Fact]
    public async Task Set_WritesAndSelects()
    {
        var numLock = new FakeNumLock(false);
        var vm = Create(numLock);
        await vm.LoadAsync();

        await vm.SetNumLockAsync(true);

        Assert.Equal(new NumLockState(true, true), numLock.State);
        Assert.True(vm.IsNumLockOnSelected);
    }

    [Fact]
    public async Task Set_SameAsCurrent_DoesNotWrite()
    {
        var numLock = new FakeNumLock(true);
        var vm = Create(numLock);
        await vm.LoadAsync();

        await vm.SetNumLockAsync(true);

        Assert.Equal(0, numLock.Writes);
    }

    [Fact]
    public async Task SetFailure_KeepsStateAndReports()
    {
        var vm = Create(new FakeNumLock(false) { Fail = true });
        await vm.LoadAsync();

        await vm.SetNumLockAsync(true);

        Assert.True(vm.IsNumLockOffSelected);
        Assert.Contains("sem permissão", vm.NumLockStatus);
    }
}
