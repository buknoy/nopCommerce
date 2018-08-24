using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payments.Oxipay
{
    /// <summary>
    /// Represents Oxipay helper
    /// </summary>
    public class OxipayHelper
    {
        #region Properties

        /// <summary>
        /// Get nopCommerce partner code
        /// </summary>
        public static string NopCommercePartnerCode => "nopCommerce_SP";

        /// <summary>
        /// Get the generic attribute name that is used to store an order total that actually sent to PayPal (used to PDT order total validation)
        /// </summary>
        public static string OrderTotalSentToOxipay => "OrderTotalSentToOxipay";

        #endregion

        #region Methods

        /// <summary>
        /// Gets a payment status
        /// </summary>
        /// <param name="paymentStatus">PayPal payment status</param>
        /// <param name="pendingReason">PayPal pending reason</param>
        /// <returns>Payment status</returns>
        public static PaymentStatus GetPaymentStatus(string paymentStatus, string pendingReason)
        {
            var result = PaymentStatus.Pending;

            if (paymentStatus == null)
                paymentStatus = string.Empty;

            if (pendingReason == null)
                pendingReason = string.Empty;

            switch (paymentStatus.ToLowerInvariant())
            {
                case "pending":                        
                    result = PaymentStatus.Pending;
                    break;                                        
                case "completed":                
                    result = PaymentStatus.Paid;
                    break;                
                case "declined":                
                case "failed":                
                    result = PaymentStatus.Voided;
                    break;
                case "refunded":
                case "reversed":
                    result = PaymentStatus.Refunded;
                    break;
                default:
                    break;
            }
            return result;
        }

        #endregion
    }
}