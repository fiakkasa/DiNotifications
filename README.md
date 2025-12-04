# DiNotifications

Simple example of handling messages based on volume received!

## Spinning up the service

- Set your preferences
  ```json
  {
    "NotificationsConfig": {
      "ImmediateCallsThresholdWindow": "00:00:01",
      "BatchedCallsRetentionPeriod": "00:00:03",
      "MaxImmediateCalls": 3,
      "BatchedItemsSeparator": "---",
      "MaxBatchedItems": 500
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
