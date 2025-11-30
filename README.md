# DiNotifications

Simple example of handling messages based on volume received!

## Spinning up the service

- Set your preferences
  ```json
  {
    "NotificationsConfig": {
      "Window": "00:00:05",
      "BatchedItemsSeparator": "---"
    },
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "AllowedHosts": "*"
  }
  ```
- Run the project: `dotnet run --project ./DiNotifications/DiNotifications.csproj --urls http://localhost:5203`

## Try it out!

- Example: http://localhost:5203/message_for_notification
