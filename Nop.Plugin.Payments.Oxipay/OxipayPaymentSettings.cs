using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Oxipay
{
    /// <summary>
    /// Represents settings of the Oxipay payment plugin
    /// </summary>
    public class OxipayPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use sandbox (testing environment)
        /// </summary>
        public bool UseSandbox { get; set; }

        /// <summary>
        /// Gets or sets a merchant id
        /// </summary>
        public string MerchantId { get; set; }

        /// <summary>
        /// Gets or sets merchant encryption key
        /// </summary>
        public string EncryptionKey { get; set; }

        /// <summary>
        /// Gets or sets Oxipay region (AU/NZ)
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Gets or sets minimum order total. Default value is 0.
        /// </summary>
        public decimal MinimumOrderTotal { get; set; }

        /// <summary>
        /// Gets or sets maximum order total. Default value is 0.
        /// </summary>
        public decimal MaximumOrderTotal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating online refunds are enabled
        /// </summary>
        public bool OnlineRefunds { get; set; }
    }
}
