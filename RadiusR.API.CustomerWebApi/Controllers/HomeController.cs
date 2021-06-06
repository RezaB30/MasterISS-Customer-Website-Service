using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Mvc;

namespace RadiusR.API.CustomerWebApi.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            //using (var client = new HttpClient())
            //{
            //    client.BaseAddress = new Uri("https://localhost:44364");
            //    client.DefaultRequestHeaders.Accept.Clear();
            //    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            //    var request = new CustomerInfo()
            //    {
            //        Id = 1,
            //        Name = "Onur"
            //    };
            //    var response = client.PostAsJsonAsync("api/client/GetCustomer", request).Result;
            //    if (response.IsSuccessStatusCode)
            //    {
            //        return Json(JsonConvert.DeserializeObject<Customer>(response.Content.ReadAsStringAsync().Result), JsonRequestBehavior.AllowGet);
            //    }
            //    else
            //    {
            //        return null;
            //    }
            //}
        //    public static class ServiceUtilities<T>
        //{
        //    private static readonly string url = "http://10.184.3.63:9190/";
        //    public static T GetWebService(object request, string webServiceName)
        //    {
        //        using (var httpClient = new HttpClient())
        //        {
        //            httpClient.BaseAddress = new Uri(url);
        //            httpClient.DefaultRequestHeaders.Accept.Clear();
        //            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //            var reqUri = webServiceName.ToLower() == "getkeyfragment" ? $"api/Client/{webServiceName}?username={request}" : $"api/Client/{webServiceName}";
        //            var response = httpClient.PostAsJsonAsync(reqUri, request).Result;
        //            var result = response.Content.ReadAsStringAsync().Result;
        //            return JsonConvert.DeserializeObject<T>(result);
        //        }
        //    }
        //}
            return RedirectToAction("Index", "Help", new { area = "" });
        }
        public class Customer
        {
            public string FullName { get; set; }
            public string Age { get; set; }
        }
        public class CustomerInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
