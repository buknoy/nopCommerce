using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Oxipay.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Oxipay.Controllers
{
    public class PaymentOxipayController : BasePaymentController
    {
        #region Fields

        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPermissionService _permissionService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly ShoppingCartSettings _shoppingCartSettings;

        #endregion

        #region Ctor

        public PaymentOxipayController(IWorkContext workContext,
            ISettingService settingService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IPermissionService permissionService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IStoreContext storeContext,
            ILogger logger,
            IWebHelper webHelper,
            ShoppingCartSettings shoppingCartSettings)
        {
            this._workContext = workContext;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._permissionService = permissionService;
            this._genericAttributeService = genericAttributeService;
            this._localizationService = localizationService;
            this._storeContext = storeContext;
            this._logger = logger;
            this._webHelper = webHelper;
            this._shoppingCartSettings = shoppingCartSettings;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var oxipayPaymentSettings = _settingService.LoadSetting<OxipayPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = oxipayPaymentSettings.UseSandbox,
                MerchantId = oxipayPaymentSettings.MerchantId,
                EncryptionKey = oxipayPaymentSettings.EncryptionKey,
                Region = oxipayPaymentSettings.Region,
                MinimumOrderTotal = oxipayPaymentSettings.MinimumOrderTotal,
                MaximumOrderTotal = oxipayPaymentSettings.MaximumOrderTotal,
                OnlineRefunds = oxipayPaymentSettings.OnlineRefunds,
                ActiveStoreScopeConfiguration = storeScope
            };
            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(oxipayPaymentSettings, x => x.UseSandbox, storeScope);
                model.MerchantId_OverrideForStore = _settingService.SettingExists(oxipayPaymentSettings, x => x.MerchantId, storeScope);
                model.EncryptionKey_OverrideForStore = _settingService.SettingExists(oxipayPaymentSettings, x => x.EncryptionKey, storeScope);
                model.Region_OverrideForStore = _settingService.SettingExists(oxipayPaymentSettings, x => x.Region, storeScope);
                model.MinimumOrderTotal_OverrideForStore = _settingService.SettingExists(oxipayPaymentSettings, x => x.MinimumOrderTotal, storeScope);
                model.MaximumOrderTotal_OverrideForStore = _settingService.SettingExists(oxipayPaymentSettings, x => x.MaximumOrderTotal, storeScope);
                model.OnlineRefunds_OverrideForStore = _settingService.SettingExists(oxipayPaymentSettings, x => x.OnlineRefunds, storeScope);
            }

            return View("~/Plugins/Payments.Oxipay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var oxipayPaymentSettings = _settingService.LoadSetting<OxipayPaymentSettings>(storeScope);

            //save settings
            oxipayPaymentSettings.UseSandbox = model.UseSandbox;
            oxipayPaymentSettings.MerchantId = model.MerchantId;
            oxipayPaymentSettings.EncryptionKey = model.EncryptionKey;
            oxipayPaymentSettings.Region = model.Region;
            oxipayPaymentSettings.MinimumOrderTotal = model.MinimumOrderTotal;
            oxipayPaymentSettings.MaximumOrderTotal = model.MaximumOrderTotal;
            oxipayPaymentSettings.OnlineRefunds = model.OnlineRefunds;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(oxipayPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(oxipayPaymentSettings, x => x.MerchantId, model.MerchantId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(oxipayPaymentSettings, x => x.EncryptionKey, model.EncryptionKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(oxipayPaymentSettings, x => x.Region, model.Region_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(oxipayPaymentSettings, x => x.MinimumOrderTotal, model.MinimumOrderTotal_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(oxipayPaymentSettings, x => x.MaximumOrderTotal, model.MaximumOrderTotal_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(oxipayPaymentSettings, x => x.OnlineRefunds, model.OnlineRefunds_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }
        

        public IActionResult Success()
        {
            var orderId = _webHelper.QueryString<string>("x_reference");
            var oxipayStatus = _webHelper.QueryString<string>("x_result");       
            var oxipayOrderId = _webHelper.QueryString<string>("x_gateway_reference");     
            var newPaymentStatus = OxipayHelper.GetPaymentStatus(oxipayStatus, null);
            Order order = null;      
                        
            if (Guid.TryParse(orderId, out Guid orderGuid))
            {
                order = _orderService.GetOrderByGuid(orderGuid);
            }        
            if (order != null) {
                order.OrderNotes.Add(new OrderNote
                {
                    Note = "Oxipay order ID: " + oxipayOrderId,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                _orderService.UpdateOrder(order);           
            }    
            switch (newPaymentStatus)
            {
                case PaymentStatus.Pending:
                    {}
                    break;
                case PaymentStatus.Paid:
                    {                        
            
                        //valid
                        if (_orderProcessingService.CanMarkOrderAsPaid(order))
                        {                                
                            _orderProcessingService.MarkOrderAsPaid(order);                            
                        }            
                        break;
                    }
                case PaymentStatus.Voided:
                    {
                        if (_orderProcessingService.CanVoidOffline(order))
                        {
                            _orderProcessingService.VoidOffline(order);
                        }
                    }
                    break;
                default:
                    break;

            }
            if (order == null)
                return RedirectToAction("Index", "Home", new {area = string.Empty});

            return RedirectToRoute("CheckoutCompleted", new {orderId = order.Id});
        }

        public IActionResult Callback()
        {
            byte[] parameters;
            using (var stream = new MemoryStream())
            {
                this.Request.Body.CopyTo(stream);
                parameters = stream.ToArray();
            }
            var strRequest = Encoding.ASCII.GetString(parameters);            

            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Oxipay") as OxipayPaymentProcessor;
            if (processor == null ||
                !_paymentService.IsPaymentMethodActive(processor) || !processor.PluginDescriptor.Installed)
                throw new NopException("Oxipay module cannot be loaded");

            if (processor.OxipayCallback(strRequest, out Dictionary<string, string> values))
            {
                values.TryGetValue("x_result", out string x_result);
                values.TryGetValue("x_gateway_reference", out string x_gateway_reference);
                values.TryGetValue("x_account_id", out string x_account_id);
                values.TryGetValue("x_reference", out string x_reference);
                values.TryGetValue("x_amount", out string x_amount);

                var newPaymentStatus = OxipayHelper.GetPaymentStatus(x_result, null);
                Order order = null;   

                if (Guid.TryParse(x_reference, out Guid orderGuid))
                {
                    order = _orderService.GetOrderByGuid(orderGuid);
                }
                switch (newPaymentStatus)
                {
                    case PaymentStatus.Pending:
                        {}
                        break;
                    case PaymentStatus.Paid:
                        {
                            if (order != null) {
                                order.OrderNotes.Add(new OrderNote
                                {
                                    Note = x_gateway_reference,
                                    DisplayToCustomer = false,
                                    CreatedOnUtc = DateTime.UtcNow
                                });
                                _orderService.UpdateOrder(order);           
                            }
                            break;
                        }
                    case PaymentStatus.Voided:
                        { }
                        break;
                    default:
                        break;
                }

            }
            //nothing should be rendered to visitor
            return Content("");

        }
        public IActionResult CancelOrder()
        {
            var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
            Order cancelOrder = null;
            if (order != null)
            {
                cancelOrder = _orderService.GetOrderByGuid(order.OrderGuid);
                /*if (Guid.TryParse(order.Id, out Guid orderGuid))
                {
                    cancelOrder = _orderService.GetOrderByGuid(orderGuid);
                }*/
                if (cancelOrder != null)
                {
                    if (_orderProcessingService.CanCancelOrder(cancelOrder))
                    {
                        _orderProcessingService.CancelOrder(cancelOrder, false);
                    }
                }
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });
            }

            return RedirectToRoute("HomePage");
        }

        #endregion
    }
}