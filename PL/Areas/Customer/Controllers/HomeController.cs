using Microsoft.AspNetCore.Mvc;
using Utility;

namespace PL.Areas.Customer.Controllers
{
    [Area(SD.Customer)]
    public class HomeController : Controller
    {

        #region Index

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        #endregion

    }
}
