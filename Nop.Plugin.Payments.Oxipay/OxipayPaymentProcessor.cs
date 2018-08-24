using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Tax;
using Nop.Services.Logging;
using Nop.Web.Framework;


namespace Nop.Plugin.Payments.Oxipay
{
    /// <summary>
    /// Oxipay payment processor
    /// </summary>
    public class OxipayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly IStoreContext _storeContext;
        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly ILogger _logger;
        private readonly OxipayPaymentSettings _oxipayPaymentSettings;

        #endregion

        #region Ctor

        public OxipayPaymentProcessor(IStoreContext storeContext,
            CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IPaymentService paymentService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            ILogger logger,
            OxipayPaymentSettings oxipayPaymentSettings)
        {
            this._storeContext = storeContext;
            this._currencySettings = currencySettings;
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._currencyService = currencyService;
            this._genericAttributeService = genericAttributeService;
            this._httpContextAccessor = httpContextAccessor;
            this._localizationService = localizationService;
            this._paymentService = paymentService;
            this._settingService = settingService;
            this._taxService = taxService;
            this._webHelper = webHelper;
            this._logger = logger;
            this._oxipayPaymentSettings = oxipayPaymentSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets Oxipay URL
        /// </summary>
        /// <returns></returns>
        private string GetOxipayRegion()
        {
            if (_oxipayPaymentSettings.Region == "New Zealand")            
                return ".oxipay.co.nz";
            
            return ".oxipay.com.au";
        }   

        /// <summary>
        /// Gets Oxipay URL
        /// </summary>
        /// <returns></returns>
        private string GetOxipayUrl()
        {   
            var regionDomain =  GetOxipayRegion();
            return _oxipayPaymentSettings.UseSandbox ?
                $"https://securesandbox{regionDomain}/Checkout?platform=Default" :
                $"https://secure{regionDomain}/Checkout?platform=Default";
        }        

        /// <summary>
        /// Gets Oxipay Refund URL
        /// </summary>
        /// <returns></returns>
        private string GetOxipayRefundUrl()
        {
            var regionDomain =  GetOxipayRegion();
            return _oxipayPaymentSettings.UseSandbox ?
                $"https://portalssandbox{regionDomain}/api/ExternalRefund/processrefund" :
                $"https://portals{regionDomain}/api/ExternalRefund/processrefund";
        }           
       

        /// <summary>
        /// Oxipay Callback
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <param name="values">Values</param>
        /// <returns>Result</returns>
        public bool OxipayCallback(string formString, out Dictionary<string, string> values)
        {            
            var req = (HttpWebRequest)WebRequest.Create(GetOxipayUrl());
            req.Method = WebRequestMethods.Http.Post;
            req.ContentType = MimeTypes.ApplicationXWwwFormUrlencoded;
            //now PayPal requires user-agent. otherwise, we can get 403 error
            req.UserAgent = _httpContextAccessor.HttpContext.Request.Headers[HeaderNames.UserAgent];            

            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in formString.Split('&'))
            {
                var line = l.Trim();
                var equalPox = line.IndexOf('=');
                if (equalPox >= 0)
                    values.Add(line.Substring(0, equalPox), line.Substring(equalPox + 1));
            }

            values.TryGetValue("x_reference", out string x_reference);
            values.TryGetValue("x_result", out string x_result);

            var formContent = $"x_reference={x_reference}&x_result={x_result}";
            req.ContentLength = formContent.Length;

            using (var sw = new StreamWriter(req.GetRequestStream(), Encoding.ASCII))
            {
                sw.Write(formContent);
            }

            string response;
            using (var sr = new StreamReader(req.GetResponse().GetResponseStream()))
            {
                response = WebUtility.UrlDecode(sr.ReadToEnd());
            }
            var success = response.Trim().Equals("VERIFIED", StringComparison.OrdinalIgnoreCase);

            return success;
        }
        /// <summary>
        /// Create request values for the request
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Created request values</returns>
        private IDictionary<string, string> CreateOxipayRequestPost(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //get store location
            var storeLocation = _webHelper.GetStoreLocation();
              //get store name
            //var storeContext = EngineContext.Current.Resolve<IStoreContext>();            
            //create query parameters
            return new Dictionary<string, string>
            {
                //Oxiapy merchant id
                ["x_account_id"] = _oxipayPaymentSettings.MerchantId,                                

                // Currency of the transaction
                ["x_currency"] = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode,

                //order identifier
                //["x_reference"] = postProcessPaymentRequest.Order.CustomOrderNumber,
                ["x_reference"] = postProcessPaymentRequest.Order.OrderGuid.ToString(),
                // ["custom"] = postProcessPaymentRequest.Order.OrderGuid.ToString(),
                //Country of where the merchant's store is located
                ["x_shop_country"] = postProcessPaymentRequest.Order.ShippingAddress?.Country?.TwoLetterIsoCode,
                // Store name
                ["x_shop_name"] = _storeContext.CurrentStore.Name,
                //callback, complete and cancel URL
                ["x_url_callback"] = $"{storeLocation}Plugins/PaymentOxipay/Callback",
                ["x_url_complete"] = $"{storeLocation}Plugins/PaymentOxipay/Success",
                ["x_url_cancel"] = $"{storeLocation}Plugins/PaymentOxipay/CancelOrder",                
                
                //test mode
                ["x_test"] = _oxipayPaymentSettings.UseSandbox ? "true" : "false",
                //customer information and shipping information
                ["x_customer_email"] = postProcessPaymentRequest.Order.ShippingAddress?.Email,
                ["x_customer_first_name"] = postProcessPaymentRequest.Order.ShippingAddress?.FirstName,
                ["x_customer_last_name"] = postProcessPaymentRequest.Order.ShippingAddress?.LastName,
                ["x_customer_shipping_address1"] = postProcessPaymentRequest.Order.ShippingAddress?.Address1,
                ["x_customer_shipping_address2"] = postProcessPaymentRequest.Order.ShippingAddress?.Address2,
                ["x_customer_shipping_city"] = postProcessPaymentRequest.Order.ShippingAddress?.City,
                ["x_customer_shipping_state"] = postProcessPaymentRequest.Order.ShippingAddress?.StateProvince?.Abbreviation,
                ["x_customer_shipping_country"] = postProcessPaymentRequest.Order.ShippingAddress?.Country?.TwoLetterIsoCode,
                ["x_customer_shipping_postcode"] = postProcessPaymentRequest.Order.ShippingAddress?.ZipPostalCode                
            };
        }    

       /// <summary>
        /// Create Oxipay refund request values
        /// </summary>
        /// <param name="refundPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Created request values</returns>
        private IDictionary<string, string> CreateOxipayRefundRequest(RefundPaymentRequest refundPaymentRequest)
        {
            var oxipayPurchaseNumber = "";
            //get store location
            var storeLocation = _webHelper.GetStoreLocation();
            var noteString = ""; 
            var order = refundPaymentRequest.Order;
            var orderNotes = order.OrderNotes;            
            //round order total
            var roundedRefundAmount = Math.Round(refundPaymentRequest.AmountToRefund, 2);
            
            foreach(var x in orderNotes)
            {
                if( x.Note.Contains("Oxipay") )
               {
                    noteString = x.Note.ToString();                    
                    oxipayPurchaseNumber = noteString.Replace("Oxipay order ID: ", "");
                    break;       
                }
            }

            //create query parameters
            return new Dictionary<string, string>
            {
                //Oxipay merchant id
                ["x_merchant_number"] = _oxipayPaymentSettings.MerchantId,  
                ["x_purchase_number"] = oxipayPurchaseNumber,
                ["x_amount"] = roundedRefundAmount.ToString("0.00", CultureInfo.InvariantCulture),
                //Reason 
                ["x_reason"] = "Refund " + oxipayPurchaseNumber + ":" + roundedRefundAmount.ToString("0.00", CultureInfo.InvariantCulture)
            };
        }            

        /// <summary>
        /// Add order total to the request query parameters
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        private void AddOrderTotalParameters(IDictionary<string, string> parameters, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //round order total
            var roundedOrderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);

            parameters.Add("x_amount", roundedOrderTotal.ToString("0.00", CultureInfo.InvariantCulture));

            //save order total that actually sent to Oxipay
            _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, OxipayHelper.OrderTotalSentToOxipay, roundedOrderTotal);
        }

        /// <summary>
        /// Genreta signature
        /// </summary>
        /// <param name="parameters">Encryption Key</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        private string GenerateHMAC(IDictionary<string, string> parameters)
        {
            var apiKey = _oxipayPaymentSettings.EncryptionKey;

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey)))
            {
                string payloadSignature = 
                parameters
                    .Where(kvp=> kvp.Key.StartsWith("x_"))
                    .OrderBy(kvp=>kvp.Key)
                    .Select(kvp => $"{kvp.Key}{kvp.Value}")
                    .Aggregate((current, next) => $"{current}{next}");

                var rawHmac = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadSignature));

                return BitConverter.ToString(rawHmac).Replace("-", string.Empty).ToLower();
            }
            
        }


        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //create common query parameters for the request
            var queryParameters = CreateOxipayRequestPost(postProcessPaymentRequest);
            AddOrderTotalParameters(queryParameters, postProcessPaymentRequest);
            var hmacSignature = GenerateHMAC(queryParameters);

            var order = postProcessPaymentRequest.Order;
            var gatewayUrl = new Uri(GetOxipayUrl());

            var remotePostHelper = new RemotePost
            {
                Url = gatewayUrl.ToString(),
                Method = "POST",
                FormName = "OxipayForm"
            };

            //queryParameters           
            foreach (KeyValuePair<string,string> record in queryParameters)
            {                
                remotePostHelper.Add(record.Key, record.Value);
            };
            // Add HMAC Signature to the request
            remotePostHelper.Add("x_signature", hmacSignature);
            _logger.InsertLog(LogLevel.Warning, remotePostHelper.Url, remotePostHelper.Method);
            remotePostHelper.Post();
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //Hide Oxipay as a payment option if total order amount is less than oxipay min amount and greater than oxipay max amount
            decimal cartTotal = 0;
            decimal price = 0;
            decimal qty = 0;
            foreach (var x in cart) {
                qty = x.Quantity;
                price = x.Product.Price * qty;
                cartTotal = cartTotal + price;
            }
            
            //Bypass if limits are not set
            if (_oxipayPaymentSettings.MinimumOrderTotal == 0 && _oxipayPaymentSettings.MaximumOrderTotal == 0)
            {
                return false;
            } 
            else
            {                           
                //Check min limit only if max limit is not set
                if (_oxipayPaymentSettings.MinimumOrderTotal >= cartTotal && _oxipayPaymentSettings.MaximumOrderTotal == 0)     
                {
                    return true;
                }
                //Check max limit only if min limit is not set
                if (_oxipayPaymentSettings.MaximumOrderTotal <= cartTotal && _oxipayPaymentSettings.MinimumOrderTotal == 0)     
                {
                    return true;
                }                
                if (_oxipayPaymentSettings.MinimumOrderTotal >= cartTotal || _oxipayPaymentSettings.MaximumOrderTotal <= cartTotal)    
                {
                    return true;
                }
                return false;                
            }
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return _paymentService.CalculateAdditionalFee(cart, 0, false);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {           

             //create common query parameters for the request             
            var refundParameters = CreateOxipayRefundRequest(refundPaymentRequest);
            var hmacSignature = GenerateHMAC(refundParameters);
            refundParameters.Add("signature",hmacSignature);
            
            var jsonRefundContent = JsonConvert.SerializeObject(refundParameters, new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented
            });

            //create post data
            var postData = Encoding.Default.GetBytes(jsonRefundContent);

            //create web request
            var serviceUrl = GetOxipayRefundUrl();
            var request = (HttpWebRequest)WebRequest.Create(serviceUrl);
            request.Method = WebRequestMethods.Http.Post;
            request.Accept = "*/*";
            request.ContentType = "application/json";
            request.ContentLength = postData.Length;
            //request.UserAgent = SquarePaymentDefaults.UserAgent;

            //post request
            using (var stream = request.GetRequestStream())
            {
                stream.Write(postData, 0, postData.Length);
            }

            //get response            
            var httpResponse = (HttpWebResponse)request.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {                             
                if (httpResponse.StatusCode == HttpStatusCode.NoContent)
                {
                    //successfully refunded
                    return new RefundPaymentResult
                    {
                        NewPaymentStatus = refundPaymentRequest.IsPartialRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded
                    };
                }
                else
                {
                    return new RefundPaymentResult { Errors = new[] { "Refund error" } };
                }
            }
            
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentOxipay/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentOxipay";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new OxipayPaymentSettings
            {
                UseSandbox = true,
                OnlineRefunds = true
            });

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MinimumOrderTotal", "Minimum Order Total");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MinimumOrderTotal.Hint", "Minimum order amount to enable Oxipay as payment.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MaximumOrderTotal", "Maximum Order Total");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MaximumOrderTotal.Hint", "Maximum order amount to enable Oxipay as payment.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MerchantId", "Merchant ID");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MerchantId.Hint", "Specify your Oxipay merchant id.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.Region", "Oxipay region");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.Region.Hint", "Points to the correct Oxipay region gateway.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.EncryptionKey", "API key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.EncryptionKey.Hint", "Specify merchant API key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.RedirectionTip", "You will be redirected to Oxipay portal to complete the order.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.UseSandbox", "Use Sandbox");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.OnlineRefunds", "Online Refunds");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Fields.OnlineRefunds.Hint", "Check to enable Oxipay online refunds.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.Instructions", "<p><b>If you're using this gateway ensure that your primary store currency is supported by Oxipay.</b><br /><br />To obtain your API key, please email <br /><a href=\"mailto:pit@oxipay.com.au\" target=\"_blank\">pit@oxipay.com.au</a>.<br /></p>");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.PaymentMethodDescription", "You will be redirected to Oxipay site to complete the payment");
            //_localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.Oxipay.RoundingWarning", "It looks like you have \"ShoppingCartSettings.RoundPricesDuringCalculation\" setting disabled. Keep in mind that this can lead to a discrepancy of the order total amount, as Oxipay only rounds to two decimals.");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<OxipayPaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MinimumOrderTotal");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MinimumOrderTotal.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MaximumOrderTotal");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MaximumOrderTotal.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MerchantId");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.MerchantId.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.Region");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.Region.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.EncryptionKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.EncryptionKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.UseSandbox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.UseSandbox.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.OnlineRefunds");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Fields.OnlineRefunds.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.Instructions");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.PaymentMethodDescription");
            //_localizationService.DeletePluginLocaleResource("Plugins.Payments.Oxipay.RoundingWarning");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return _oxipayPaymentSettings.OnlineRefunds ? true : false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return _oxipayPaymentSettings.OnlineRefunds ? true : false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PayPal site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.Oxipay.PaymentMethodDescription"); }
        }

        #endregion
    }
}