namespace DiNotifications;

public interface INotificationsService : IDisposable
{
    Task<OneOf<bool, Exception>>  Send(string subject, string body, CancellationToken cancellationToken = default);
}
