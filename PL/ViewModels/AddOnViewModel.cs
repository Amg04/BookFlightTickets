using DAL.models;
using System.ComponentModel.DataAnnotations;

namespace PL.ViewModels
{
    public class AddOnViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, ErrorMessage = "Name cannot be more than 50 characters")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Price is required")]
        [Range(1, 10000, ErrorMessage = "Price must be between 1 and 10000")]
        public short price { get; set; }

        #region Mapping

        public static explicit operator AddOnViewModel(AddOn model)
        {
            return new AddOnViewModel
            {
                Id = model.Id,
                Name = model.Name,
                price = model.price,
            };
        }

        public static explicit operator AddOn(AddOnViewModel ViewModel)
        {
            return new AddOn
            {
                Id = ViewModel.Id,
                Name = ViewModel.Name,
                price = ViewModel.price,
            };
        }

        #endregion

    }
}
