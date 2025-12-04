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
    private readonly NotificationsConfig _config;
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
        _config = options.CurrentValue;

        _rxSubscription =
            _rxSubject
                .Window(_config.BatchedCallsRetentionPeriod)
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
            if (_disposedValue)
            {
                _logger.LogWarning("Operation cannot commence as the service is disposed.");
                return false;
            }

            var now = DateTimeOffset.UtcNow;

            if (ShouldSendImmediate(now, cancellationToken))
            {
                return await _sender.Send(now, subject, body, cancellationToken);
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

            if ((now - timestamp).TotalMilliseconds <= _config.ImmediateCallsThresholdWindow.TotalMilliseconds)
            {
                break;
            }

            _rollingCallsQueue.TryDequeue(out _);
        }

        return _rollingCallsQueue.Count <= _config.MaxImmediateCalls;
    }

    private async Task BatchSend(
        IList<Notification> list,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (list is [Notification item])
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (timestamp, subject, body) = item;

                await _sender.Send(
                    timestamp,
                    subject,
                    body,
                    cancellationToken
                );

                return;
            }

            var batchSubject = $"{list.Count} Notifications Received";
            var sb = new StringBuilder();

            var collection = list.AsEnumerable();
            var partialProcessing = 
                _config.MaxBatchedItems > 0 
                && list.Count >= _config.MaxBatchedItems;

            if (partialProcessing)
            {
                collection = list.Take(_config.MaxBatchedItems);
                _logger.LogWarning(
                    "Only {MaxBatchedItems} out of {BatchedItemsCount} will be processed.",
                    _config.MaxBatchedItems,
                    list.Count
                );
            }

            foreach (var (timestamp, subject, body) in collection)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (sb.Length > 0)
                {
                    sb.AppendLine(_config.BatchedItemsSeparator);
                }

                sb
                    .Append('[').Append(timestamp.ToString("u")).Append("] ").AppendLine(subject)
                    .AppendLine(body);
            }

            if (partialProcessing)
            {
                sb
                    .AppendLine(_config.BatchedItemsSeparator)
                    .Append("Note: Only ")
                        .Append(_config.MaxBatchedItems)
                        .Append(" out of ")
                        .Append(list.Count)
                        .AppendLine(" were processed.")
                    .AppendLine("Check the logs for more details.");
            }

            await _sender.Send(
                list[0].Timestamp,
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
