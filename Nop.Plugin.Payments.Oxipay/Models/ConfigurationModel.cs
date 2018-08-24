using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.Oxipay.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        public ConfigurationModel()
        {
            Regions = new List<SelectListItem>();
            var australiaItem = new SelectListItem { Text = "Australia", Value ="Australia"};
            var newZealandItem = new SelectListItem { Text = "New Zealand", Value ="New Zealand"};
            Regions.Add(australiaItem);
            Regions.Add(newZealandItem);
        }

        [NopResourceDisplayName("Plugins.Payments.Oxipay.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Oxipay.Fields.MerchantId")]
        public string MerchantId { get; set; }
        public bool MerchantId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Oxipay.Fields.EncryptionKey")]
        public string EncryptionKey { get; set; }
        public bool EncryptionKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Oxipay.Fields.Region")]
        public string Region { get; set; }
        public bool Region_OverrideForStore { get; set; }
        public IList<SelectListItem> Regions { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Oxipay.Fields.MinimumOrderTotal")]
        public decimal MinimumOrderTotal { get; set; }
        public bool MinimumOrderTotal_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Oxipay.Fields.MaximumOrderTotal")]
        public decimal MaximumOrderTotal { get; set; }
        public bool MaximumOrderTotal_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Oxipay.Fields.OnlineRefunds")]
        public bool OnlineRefunds { get; set; }
        public bool OnlineRefunds_OverrideForStore { get; set; }
    }
}