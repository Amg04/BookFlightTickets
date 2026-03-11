using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.ServiceContracts;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using Rotativa.AspNetCore;

namespace BookFlightTickets.Core.Services
{
    public class RotativaPdfService : IPdfService
    {
        public async Task<byte[]> GenerateBookingPdfAsync(Booking booking, ViewDataDictionary viewData, ActionContext actionContext)
        {
            var pdf = new ViewAsPdf("/Areas/Customer/Views/MyBookings/BookingPDF.cshtml", booking, viewData)
            {
                PageMargins = new Rotativa.AspNetCore.Options.Margins { Top = 20, Right = 20, Bottom = 20, Left = 20 },
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
            return await pdf.BuildFile(actionContext);
        }
    }
}
