using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;
using System.Web.Mvc;
using RadiusR.API.CustomerWebApi.Areas.HelpPage.ModelDescriptions;
using RadiusR.API.CustomerWebApi.Areas.HelpPage.Models;

namespace RadiusR.API.CustomerWebApi.Areas.HelpPage.Controllers
{
    /// <summary>
    /// The controller that will handle requests for the help page.
    /// </summary>
    public class HelpController : Controller
    {
        private const string ErrorViewName = "Error";

        public HelpController()
            : this(GlobalConfiguration.Configuration)
        {
        }

        public HelpController(HttpConfiguration config)
        {
            Configuration = config;
        }

        public HttpConfiguration Configuration { get; private set; }

        public ActionResult Index(string token)
        {
            //hash = 
            SHA256 algorithm = (SHA256)HashAlgorithm.Create(typeof(SHA256).Name);
            var calculatedHash = string.Join(string.Empty, algorithm.ComputeHash(Encoding.UTF8.GetBytes(Properties.Settings.Default.WebApiPageHash)).Select(b => b.ToString("x2")));
            if (token != calculatedHash && Session["token"] == null)
            {
                return Content(System.Net.HttpStatusCode.Unauthorized.ToString());
            }
            //
            Session["token"] = true;
            ViewBag.DocumentationProvider = Configuration.Services.GetDocumentationProvider();
            return View(Configuration.Services.GetApiExplorer().ApiDescriptions);
        }

        public ActionResult Api(string apiId)
        {
            if (Session["token"] == null)
            {
                return Content(System.Net.HttpStatusCode.Unauthorized.ToString());
            }
            if (!String.IsNullOrEmpty(apiId))
            {
                HelpPageApiModel apiModel = Configuration.GetHelpPageApiModel(apiId);
                if (apiModel != null)
                {
                    return View(apiModel);
                }
            }

            return View(ErrorViewName);
        }

        public ActionResult ResourceModel(string modelName)
        {
            if (!String.IsNullOrEmpty(modelName))
            {
                ModelDescriptionGenerator modelDescriptionGenerator = Configuration.GetModelDescriptionGenerator();
                ModelDescription modelDescription;
                if (modelDescriptionGenerator.GeneratedModels.TryGetValue(modelName, out modelDescription))
                {
                    return View(modelDescription);
                }
            }

            return View(ErrorViewName);
        }
    }
}