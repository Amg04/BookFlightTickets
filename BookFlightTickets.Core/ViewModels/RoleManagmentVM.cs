using BookFlightTickets.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BookFlightTickets.Core.ViewModels
{
    public class RoleManagmentVM
    {
        public AppUser ApplicationUser { get; set; } = new AppUser();
        [ValidateNever]
        public IEnumerable<SelectListItem> RoleList { get; set; } = Enumerable.Empty<SelectListItem>();
       }
}
