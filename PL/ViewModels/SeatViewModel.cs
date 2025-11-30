using DAL.models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PL.ViewModels
{
    public class SeatViewModel
    {
        public int Id { get; set; }
        [Required]
        [Display(Name = "Airplane Model")]
        public int AirplaneId { get; set; }

        [ValidateNever]
        public Airplane Airplane { get; set; } = null!;

        [Required(ErrorMessage = "Row is required")]
        [StringLength(5, ErrorMessage = "Row can be at most 5 characters")]
        public string Row { get; set; }

        [Required(ErrorMessage = "Number is required")]
        [Range(1, 1000, ErrorMessage = "Number must be between 1 and 1000")]
        public short Number { get; set; }

        [Required(ErrorMessage = "Class is required")]
        public SeatClass Class { get; set; }
        [Required]
        public int Price { get; set; }
        [Display(Name = "Is Available")]
        public bool IsAvailable { get; set; } = true;

        #region Mapping

        public static explicit operator SeatViewModel(Seat model)
        {
            return new SeatViewModel
            {
                Id = model.Id,
                AirplaneId = model.AirplaneId,
                Airplane = model.Airplane,
                Row = model.Row,
                Number = model.Number,
                Class = model.Class,
            };
        }

        public static explicit operator Seat(SeatViewModel ViewModel)
        {
            return new Seat
            {
                Id = ViewModel.Id,
                AirplaneId = ViewModel.AirplaneId,
                Airplane = ViewModel.Airplane,
                Row = ViewModel.Row,
                Number = ViewModel.Number,
                Class = ViewModel.Class,
            };
        }

        #endregion

    }
}
