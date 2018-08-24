using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Oxipay
{
    public partial class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routeBuilder">Route builder</param>
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //Success
            routeBuilder.MapRoute("Plugin.Payments.Oxipay.Success", "Plugins/PaymentOxipay/Success",
                 new { controller = "PaymentOxipay", action = "Success" });

            //Callback
            routeBuilder.MapRoute("Plugin.Payments.Oxipay.Callback", "Plugins/PaymentOxipay/Callback",
                 new { controller = "PaymentOxipay", action = "Callback" });

            //Cancel
            routeBuilder.MapRoute("Plugin.Payments.Oxipay.CancelOrder", "Plugins/PaymentOxipay/CancelOrder",
                 new { controller = "PaymentOxipay", action = "CancelOrder" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority
        {
            get { return -1; }
        }
    }
}
