using DiNotifications;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var services = builder.Services;

services
    .AddOptions<NotificationsConfig>()
    .Bind(config.GetSection(nameof(NotificationsConfig)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.AddSingleton<ISenderService, SenderService>();
services.AddSingleton<INotificationsService, NotificationsService>();

var app = builder.Build();

app.MapGet("/", () => "Hello from DiNotification!");
app.MapGet(
    "/{message}", 
    async (INotificationsService service, string message, CancellationToken cancellationToken = default) => 
        (await service.Send("Hello!", message, cancellationToken))
            .Match(
                result => Results.Ok(result),
                ex => Results.Problem(ex.Message)
            )
);

app.Run();
