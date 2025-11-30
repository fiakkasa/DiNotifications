namespace DiNotifications;

public interface ISenderService
{
    Task Send(DateTimeOffset timestamp, string subject, string body, CancellationToken cancellationToken = default);
}
