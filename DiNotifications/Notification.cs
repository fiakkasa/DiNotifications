namespace DiNotifications;

[ExcludeFromCodeCoverage]
public record Notification(DateTimeOffset Timestamp, string Subject, string Body);
