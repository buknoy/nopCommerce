using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Oxipay.Components
{
    [ViewComponent(Name = "PaymentOxipay")]
    public class PaymentOxipayViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.Oxipay/Views/PaymentInfo.cshtml");
        }
    }
}
