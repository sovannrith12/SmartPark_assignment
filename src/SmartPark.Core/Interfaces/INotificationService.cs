    namespace SmartPark.Core.Interfaces;

    /// <summary>
    /// Sends notifications (SMS/email) to customers.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Sends a parking receipt to the given phone number.
        /// </summary>
        Task SendReceiptAsync(string phoneNumber, string receiptContent);
    }