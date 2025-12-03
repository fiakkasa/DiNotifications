namespace DiNotifications;

public interface ISenderService
{
    Task<OneOf<bool, Exception>> Send(DateTimeOffset timestamp, string subject, string body, CancellationToken cancellationToken = default);
}
