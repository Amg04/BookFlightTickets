using BookFlightTickets.Core.Shared.Utility;
using Microsoft.AspNetCore.Mvc;

namespace BookFlightTickets.UI.Areas.Customer.Controllers
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
