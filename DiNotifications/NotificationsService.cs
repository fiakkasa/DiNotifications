using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

using Microsoft.Extensions.Options;

namespace DiNotifications;

public sealed class NotificationsService : INotificationsService
{
    private readonly ILogger<NotificationsService> _logger;
    private readonly ISenderService _sender;
    private readonly ConcurrentQueue<DateTimeOffset> _rollingCallsQueue = new();
    private readonly Subject<Notification> _rxSubject = new();
    private readonly double _windowMilliseconds;
    private readonly IDisposable _rxSubscription;

    private bool _disposedValue;

    public NotificationsService(
        IOptionsMonitor<NotificationsConfig> options,
        ISenderService sender,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<NotificationsService> logger
    )
    {
        _logger = logger;
        _sender = sender;
        var config = options.CurrentValue;

        _windowMilliseconds = config.Window.TotalMilliseconds;
        _rxSubscription =
            _rxSubject
                .Window(config.Window)
                .SelectMany(window => window.ToList())
                .Where(list => list.Count > 0)
                .Subscribe(async list =>
                    await BatchSend(list, hostApplicationLifetime.ApplicationStopping)
                );
    }

    public async Task<OneOf<bool, Exception>> Send(
        string subject,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if(_disposedValue)
            {
                _logger.LogWarning("Operation cannot commence as the service is disposed.");
                return false;
            }

            var now = DateTimeOffset.UtcNow;

            if (ShouldSendImmediate(now, cancellationToken))
            {
                await _sender.Send(now, subject, body, cancellationToken);

                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();

            _rxSubject.OnNext(new Notification(now, subject, body));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while handling notification request with message: {Message}",
                ex.Message
            );

            return ex;
        }
    }

    private bool ShouldSendImmediate(
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        _rollingCallsQueue.Enqueue(now);

        while (_rollingCallsQueue.TryPeek(out var timestamp))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((now - timestamp).TotalMilliseconds <= _windowMilliseconds)
            {
                break;
            }

            _rollingCallsQueue.TryDequeue(out _);
        }

        return _rollingCallsQueue.Count == 1;
    }

    private async Task BatchSend(
        IList<Notification> list,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (list.Count == 1)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (timestamp, subject, body) = list[0];
                await _sender.Send(
                    timestamp,
                    subject,
                    body,
                    cancellationToken
                );

                return;
            }

            var firstTimestamp = list[0].Timestamp;
            var batchSubject = $"{list.Count} Notifications Received";
            var sb = new StringBuilder();

            foreach (var (timestamp, subject, body) in list)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (sb.Length > 0)
                {
                    sb.AppendLine("---");
                }

                sb
                    .Append('[').Append(timestamp.ToString("u")).Append("] ").AppendLine(subject)
                    .AppendLine(body);
            }

            await _sender.Send(
                firstTimestamp,
                batchSubject,
                sb.ToString(),
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed with message: {Message}",
                ex.Message
            );
        }
    }

#pragma warning disable IDE0060, S1172
    private void Dispose(bool disposing)
#pragma warning restore
    {
        if (_disposedValue)
        {
            return;
        }

        _rxSubscription.Dispose();
        _rxSubject.Dispose();
        _rollingCallsQueue.Clear();

        _disposedValue = true;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
