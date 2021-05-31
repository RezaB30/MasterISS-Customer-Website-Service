using System.Web;
using System.Web.Mvc;

namespace RadiusR.API.CustomerWebApi
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
