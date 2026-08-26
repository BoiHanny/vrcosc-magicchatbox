using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public sealed class WeatherLocationFailureTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"results":[]}"""),
            });
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> func) => func();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
        public bool CheckAccess() => true;
        public void BeginInvoke(Action action) => action();
        public void Shutdown() { }
    }

    private sealed class FixedClock : ITimeFormattingService
    {
        public string GetFormattedCurrentTime() => "13:37";
    }

    private sealed class DeniedConsent : IPrivacyConsentService
    {
        public bool IsApproved(PrivacyHook hook) => false;
        public ConsentState GetState(PrivacyHook hook) => ConsentState.Denied;
        public void Approve(PrivacyHook hook) { }
        public void Deny(PrivacyHook hook) { }
        public void Reset(PrivacyHook hook) { }
        public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks)
            => Array.Empty<PrivacyHook>();
        public event EventHandler<ConsentChangedEventArgs> ConsentChanged { add { } remove { } }
    }

    private sealed class RecordingToast : IToastService
    {
        public TaskCompletionSource<string> Message { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ObservableCollection<ToastItemViewModel> Toasts { get; } = new();

        public void Show(
            string title,
            string message,
            ToastType type = ToastType.Info,
            ToastAction? action = null,
            int durationMs = 5000,
            string? key = null)
            => Message.TrySetResult(message);

        public void Dismiss(ToastItemViewModel item) { }
    }

    [Fact]
    public async Task Unknown_cities_surface_a_user_facing_error()
    {
        var settings = new WeatherSettings
        {
            ShowWeatherInTime = true,
            WeatherLocationMode = WeatherLocationMode.CustomCity,
            WeatherLocationCity = "definitely-not-a-real-city",
        };
        using var handler = new StubHandler();
        var toast = new RecordingToast();
        var service = new WeatherService(
            new StubHttpClientFactory(handler),
            new StubSettingsProvider<WeatherSettings>(settings),
            new StubSettingsProvider<TimeSettings>(new TimeSettings()),
            new IntegrationDisplayState(),
            new StubSettingsProvider<ComponentStatsSettings>(new ComponentStatsSettings()),
            new ImmediateDispatcher(),
            new FixedClock(),
            new DeniedConsent(),
            toast);

        service.TriggerManualRefresh();
        string message = await toast.Message.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Contains("city", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("location", message, StringComparison.OrdinalIgnoreCase);
    }
}
