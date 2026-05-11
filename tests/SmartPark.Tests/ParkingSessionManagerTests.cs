using Moq;
using SmartPark.Core.Interfaces;
using SmartPark.Core.Models;
using SmartPark.Core.Services;

namespace SmartPark.Tests;

public class ParkingSessionManagerTests
{
    // ────────────────────────────────────────────────────────────
    //  SHARED SETUP — create test doubles and the system-under-test.
    //  Moq's Mock<T> creates test doubles that can act as:
    //    - Stubs: .Setup().Returns() — provide canned answers
    //    - Mocks: .Verify()         — assert interactions happened
    //  You can use a constructor, or duplicate this in each test.
    // ────────────────────────────────────────────────────────────

    private readonly Mock<IPaymentGateway> _paymentStub = new();
    private readonly Mock<INotificationService> _notificationStub = new();
    private readonly Mock<IMembershipService> _membershipStub = new();
    private readonly Mock<IParkingRepository> _repoStub = new();
    private readonly Mock<IDateTimeProvider> _dateTimeStub = new();
    private readonly ParkingFeeCalculator _feeCalculator = new();
    private readonly ParkingSessionManager _manager;

    public ParkingSessionManagerTests()
    {
        _manager = new ParkingSessionManager(
            _feeCalculator,
            _paymentStub.Object,
            _notificationStub.Object,
            _membershipStub.Object,
            _repoStub.Object,
            _dateTimeStub.Object);
    }

    // ────────────────────────────────────────────────────────────
    //  EXAMPLE TEST — shows stub setup + mock verification pattern.
    //  .Setup().Returns() = STUB behavior (canned answer)
    //  .Verify()          = MOCK behavior (interaction assertion)
    //  Delete or keep this; it does not count toward your grade.
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckInAsync_NewVehicle_LookUpMembership()
    {
        // Arrange — configure stubs (canned return values)
        _membershipStub.Setup(m => m.GetMembershipTier("PP-9999")).Returns(MembershipTier.Guest);
        _repoStub.Setup(r => r.GetActiveTicketByPlateAsync("PP-9999")).ReturnsAsync((ParkingTicket?)null);
        _dateTimeStub.Setup(d => d.Now).Returns(new DateTime(2026, 3, 16, 10, 0, 0));

        // Act
        var ticket = await _manager.CheckInAsync("PP-9999", VehicleType.Car);

        // Assert — verify as mock (was this interaction called?)
        _membershipStub.Verify(m => m.GetMembershipTier("PP-9999"), Times.Once);
        Assert.Equal("PP-9999", ticket.Vehicle.LicensePlate);
    }

    #region CheckIn — Happy Path
    // Test successful vehicle check-in and verify correct interactions
    #endregion
    [Fact]
    public async Task CheckInAsync_ValidVehicle_SavesToRepository()
    {
        var plate = "PLATE-123";
        _membershipStub.Setup(m => m.GetMembershipTier(plate)).Returns(MembershipTier.Guest);
        _repoStub.Setup(r => r.GetActiveTicketByPlateAsync(plate)).ReturnsAsync((ParkingTicket?)null);

        await _manager.CheckInAsync(plate, VehicleType.Car);

        // Verify the ticket was actually sent to the database to be saved
        _repoStub.Verify(r => r.SaveTicketAsync(It.IsAny<ParkingTicket>()), Times.Once);
    }
    #region CheckIn — Validation
    // Test check-in error scenarios and verify side effects
    #endregion
    [Fact]
    public async Task CheckInAsync_DuplicateVehicle_ThrowsInvalidOperationException()
    {
        var plate = "DUPE-123";
        // Stub: pretend the car is already inside
        _repoStub.Setup(r => r.GetActiveTicketByPlateAsync(plate)).ReturnsAsync(new ParkingTicket());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.CheckInAsync(plate, VehicleType.Car));
    }
    #region CheckOut — Happy Path
    // Test successful check-out with payment and notification
    #endregion
    [Fact]
    public async Task CheckOutAsync_SuccessfulPayment_UpdatesTicketAndNotifies()
    {
        var ticketId = "TKT-001";
        // Fixing 'Id' to match your model (likely empty or different name)
        // and setting CheckInTime so it's 'Active'
        var ticket = new ParkingTicket
        {
            CheckInTime = DateTime.Now.AddHours(-1),
            Vehicle = new Vehicle { Type = VehicleType.Car }
        };

        _repoStub.Setup(r => r.GetTicketByIdAsync(ticketId)).ReturnsAsync(ticket);
        _paymentStub.Setup(p => p.ProcessPaymentAsync(ticketId, It.IsAny<decimal>())).ReturnsAsync(true);
        _dateTimeStub.Setup(d => d.Now).Returns(DateTime.Now);

        await _manager.CheckOutAsync(ticketId, "012345678");

        _repoStub.Verify(r => r.UpdateTicketAsync(ticket), Times.Once);
        _notificationStub.Verify(n => n.SendReceiptAsync("012345678", It.IsAny<string>()), Times.Once);
    }
    #region CheckOut — Payment Failure
    // Test behavior when the payment step fails
    #endregion
    [Fact]
    public async Task CheckOutAsync_PaymentFails_DoesNotUpdateRepository()
    {
        var ticketId = "TKT-FAIL";
        var ticket = new ParkingTicket
        {
            CheckInTime = DateTime.Now.AddHours(-1),
            Vehicle = new Vehicle { Type = VehicleType.Car }
        };

        _repoStub.Setup(r => r.GetTicketByIdAsync(ticketId)).ReturnsAsync(ticket);
        _paymentStub.Setup(p => p.ProcessPaymentAsync(ticketId, It.IsAny<decimal>())).ReturnsAsync(false);
        _dateTimeStub.Setup(d => d.Now).Returns(DateTime.Now);

        await Assert.ThrowsAsync<Exception>(() => _manager.CheckOutAsync(ticketId, "012345678"));

        _repoStub.Verify(r => r.UpdateTicketAsync(It.IsAny<ParkingTicket>()), Times.Never);
    }
    #region CheckOut — Notification Failure
    // Test what happens when sending the receipt fails
    #endregion
    [Fact]
    public async Task CheckOutAsync_NotificationFails_StillCompletesSuccessfully()
    {
        var ticketId = "TKT-NOTIFY-FAIL";
        var ticket = new ParkingTicket
        {
            CheckInTime = DateTime.Now.AddHours(-1),
            Vehicle = new Vehicle { Type = VehicleType.Car }
        };

        _repoStub.Setup(r => r.GetTicketByIdAsync(ticketId)).ReturnsAsync(ticket);
        _paymentStub.Setup(p => p.ProcessPaymentAsync(ticketId, It.IsAny<decimal>())).ReturnsAsync(true);
        _dateTimeStub.Setup(d => d.Now).Returns(DateTime.Now);

        _notificationStub.Setup(n => n.SendReceiptAsync(It.IsAny<string>(), It.IsAny<string>()))
                         .ThrowsAsync(new Exception("SMS Provider Down"));

        var result = await _manager.CheckOutAsync(ticketId, "012345678");

        Assert.NotNull(result);
        _repoStub.Verify(r => r.UpdateTicketAsync(ticket), Times.Once);
    }
    #region CheckOut — Validation
    // Test check-out error scenarios for missing or invalid tickets
    #endregion
    [Fact]
    public async Task CheckOutAsync_MissingTicket_ThrowsKeyNotFoundException()
    {
        _repoStub.Setup(r => r.GetTicketByIdAsync("MISSING")).ReturnsAsync((ParkingTicket?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _manager.CheckOutAsync("MISSING", "000"));
    }
    #region Verify Interaction Order
    // Verify that dependencies are called in the correct sequence
    #endregion
    [Fact]
    public async Task CheckOutAsync_ShouldFollowStrictSequence()
    {
        // Arrange
        var ticketId = "SEQ-123";
        var now = new DateTime(2026, 5, 11, 12, 0, 0); // Fixed "Now" time
        var checkIn = now.AddHours(-1); // 11:00 AM

        var ticket = new ParkingTicket
        {
            CheckInTime = checkIn,
            Vehicle = new Vehicle { Type = VehicleType.Car, Membership = MembershipTier.Guest }
        };

        // We MUST tell the stub what time it is now so it's not earlier than check-in
        _dateTimeStub.Setup(d => d.Now).Returns(now);

        var sequence = new MockSequence();

        // We set up the mocks in the EXACT order they must be called
        _repoStub.InSequence(sequence).Setup(r => r.GetTicketByIdAsync(ticketId)).ReturnsAsync(ticket);
        _paymentStub.InSequence(sequence).Setup(p => p.ProcessPaymentAsync(ticketId, It.IsAny<decimal>())).ReturnsAsync(true);
        _repoStub.InSequence(sequence).Setup(r => r.UpdateTicketAsync(ticket)).Returns(Task.CompletedTask);
        _notificationStub.InSequence(sequence).Setup(n => n.SendReceiptAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        await _manager.CheckOutAsync(ticketId, "012345678");

        // Assert
        // The test passes if no sequence or argument exceptions are thrown.
    }
}
