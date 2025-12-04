using System.ComponentModel.DataAnnotations;

namespace DiNotifications;

public class NotificationsConfigTest
{
    private static IList<ValidationResult> Validate(NotificationsConfig model)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(model, new(model), results, validateAllProperties: true);

        return results;
    }

    [Fact]
    public void Should_Validate_Successfully_With_Default_Config()
    {
        var cfg = new NotificationsConfig();
        var results = Validate(cfg);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(10_000)]
    [InlineData(60_000)]
    public void Should_Validate_Successfully_When_Window_Boundaries_Are_Valid(double milliseconds)
    {
        var cfg = new NotificationsConfig 
        { 
            ImmediateCallsThresholdWindow = TimeSpan.FromMilliseconds(milliseconds),
            BatchedCallsRetentionPeriod = TimeSpan.FromMilliseconds(milliseconds)
        };
        
        var results = Validate(cfg);

        Assert.Empty(results);
    }

    [Theory]
    [InlineData(499)]
    [InlineData(60_001)]
    public void Should_Not_Validate_Successfully_When_Window_Is_Invalid(double milliseconds)
    {
        var cfg = new NotificationsConfig 
        { 
            ImmediateCallsThresholdWindow = TimeSpan.FromMilliseconds(milliseconds), 
            BatchedCallsRetentionPeriod = TimeSpan.FromMilliseconds(milliseconds) 
        };

        var results = Validate(cfg);

        Assert.Equal(2, results.Count);
        Assert.Equal(
            "The specified period needs to be between 500 and 60000 milliseconds, inclusive.",
            results[0].ErrorMessage
        );
    }
}