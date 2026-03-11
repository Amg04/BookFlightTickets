using BookFlightTickets.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;

namespace BookFlightTickets.Core.ServiceContracts
{
    public interface IPdfService
    {
        Task<byte[]> GenerateBookingPdfAsync(Booking booking, ViewDataDictionary viewData, ActionContext actionContext);
    }
}