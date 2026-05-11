using SmartPark.Core.Models;
using SmartPark.Core.Services;
using FsCheck;
using FsCheck.Xunit;

namespace SmartPark.Tests;

public class ParkingFeeCalculatorTests
{
    private readonly ParkingFeeCalculator _calculator = new();

    // ────────────────────────────────────────────────────────────
    //  EXAMPLE TEST — shows the naming convention and AAA pattern.
    //  Delete or keep this; it does not count toward your grade.
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateFee_ZeroDuration_ReturnsFree()
    {
        // Arrange
        var checkIn = new DateTime(2026, 3, 16, 10, 0, 0);  // Monday
        var checkOut = checkIn; // same time = 0 duration

        // Act
        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        // Assert
        Assert.Equal(0m, result.TotalFee);
    }

    #region Basic Fee Calculation
    // Test basic hourly rates for each vehicle type
    // Consider using [Theory] with [InlineData] for multiple scenarios
    #endregion
    [Fact]
    public void CalculateFee_Motorcycle_2Hours_Returns1000()
    {
        // Arrange
        var checkIn = new DateTime(2026, 3, 16, 10, 0, 0); // Monday
        var checkOut = checkIn.AddHours(2);
        var vehicle = VehicleType.Motorcycle;
        var tier = MembershipTier.Guest;

        // Act
        var result = _calculator.CalculateFee(vehicle, tier, checkIn, checkOut);

        // Assert
        Assert.Equal(1000m, result.TotalFee);
    }
    #region Grace Period
    // Test the free parking window and its boundaries
    #endregion
    [Fact]
    public void CalculateFee_GracePeriod_20Min_ReturnsFree()
    {
        // Arrange
        var checkIn = new DateTime(2026, 3, 16, 10, 0, 0);
        var checkOut = checkIn.AddMinutes(20); // Less than 30 mins

        // Act
        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        // Assert
        Assert.Equal(0m, result.TotalFee);
    }
    #region Duration Rounding
    // Test how partial hours are rounded for billing
    #endregion
    [Fact]
    public void CalculateFee_Rounding_31Min_Returns1HourFee()
    {
        // 31 minutes should count as 1 full hour after 30-min grace period
        var checkIn = new DateTime(2026, 3, 16, 10, 0, 0);
        var checkOut = checkIn.AddMinutes(31);

        var result = _calculator.CalculateFee(VehicleType.Car, MembershipTier.Guest, checkIn, checkOut);

        // Expected: 1,000 KHR (Car rate is 1000/hr)
        Assert.Equal(1000m, result.TotalFee);
    }
    #region Daily Cap
    // Test that fees respect maximum daily limits per vehicle type
    #endregion

    #region Overnight Fee
    // Test the flat fee applied for sessions that extend into late hours
    #endregion

    #region Weekend Surcharge
    // Test the percentage-based surcharge on specific days
    #endregion

    #region Holiday Surcharge
    // Test holiday pricing and its interaction with weekend pricing
    #endregion

    #region Membership Discounts
    // Test discount tiers and what amounts they apply to
    #endregion

    #region Lost Ticket
    // Test the penalty and how it interacts with other fee modifiers
    #endregion

    #region Edge Cases
    // Test invalid inputs and boundary conditions
    #endregion

    #region Property-Based Tests
    // Write at least 5 FsCheck properties that must hold for ALL valid inputs
    // You may need custom Arbitrary<T> for generating valid DateTime pairs
    #endregion
}
