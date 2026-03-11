using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Enums;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.ResultPattern;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using BookFlightTickets.UI.Areas.Customer.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using X.PagedList;

namespace BookFlightTickets.ControllerTests.Customer
{
    public class FlightControllerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IFlightService> _flightServiceMock;
        private readonly Mock<ILogger<FlightController>> _loggerMock;
        private readonly Mock<IGenericRepository<Flight>> _flightRepoMock;
        private readonly FlightController _controller;
        private readonly ITempDataDictionary _tempData;

        public FlightControllerTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _flightServiceMock = new Mock<IFlightService>();
            _loggerMock = new Mock<ILogger<FlightController>>();
            _flightRepoMock = new Mock<IGenericRepository<Flight>>();

            _unitOfWorkMock.Setup(u => u.Repository<Flight>()).Returns(_flightRepoMock.Object);

            _controller = new FlightController(
                _unitOfWorkMock.Object,
                _flightServiceMock.Object,
                _loggerMock.Object
            );

            // Setup TempData
            _tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = _tempData;
        }

        #region Index (GET)

        [Fact]
        public async Task Index_ValidRequest_ReturnsViewWithPagedFlights()
        {
            // Arrange
            var filter = new FlightFilterViewModel
            {
                Page = 1,
                PageSize = 10,
                SearchBy = "Airline",
                SearchString = "Delta",
                SortBy = "DepartureTime",
                SortOrder = SortOrderOptions.ASC
            };

            var flights = new List<FlightViewModel>
            {
                new FlightViewModel { Id = 1, Airline = new AirlineViewModel { Name = "Delta" } },
                new FlightViewModel { Id = 2, Airline = new AirlineViewModel { Name = "Delta" } }
            };

            _flightServiceMock.Setup(s => s.GetFilteredFlights(
                    filter.SearchBy, filter.SearchString, filter.FromDate, filter.ToDate))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            _flightServiceMock.Setup(s => s.GetSortedFlightsAsync(
                    flights, filter.SortBy, filter.SortOrder))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            // Act
            var result = await _controller.Index(filter);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IPagedList<FlightViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count);
            Assert.Equal(1, model.PageNumber);
            Assert.Equal(10, model.PageSize);

            // Verify ViewBag values
            Assert.NotNull(_controller.ViewBag.SearchFields);
            Assert.NotNull(_controller.ViewBag.SortableHeaders);
            Assert.Equal(filter.SearchBy, _controller.ViewBag.CurrentSearchBy);
            Assert.Equal(filter.SearchString, _controller.ViewBag.CurrentSearchString);
            Assert.Equal(filter.SortBy, _controller.ViewBag.CurrentSortBy);
            Assert.Equal(filter.SortOrder.ToString(), _controller.ViewBag.CurrentSortOrder);
        }

        [Fact]
        public async Task Index_InvalidDateRange_ReturnsViewWithErrorAndEmptyList()
        {
            // Arrange
            var filter = new FlightFilterViewModel
            {
                FromDate = DateTime.Today.AddDays(5),
                ToDate = DateTime.Today,
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _controller.Index(filter);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IPagedList<FlightViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("From date cannot be after To date", _controller.TempData["error"]);

            _flightServiceMock.Verify(s => s.GetFilteredFlights(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Never);
        }

        [Fact]
        public async Task Index_FilterServiceFails_ReturnsViewWithErrorAndEmptyList()
        {
            // Arrange
            var filter = new FlightFilterViewModel { Page = 1, PageSize = 10 };
            var errorMessage = "Filtering failed";
            _flightServiceMock.Setup(s => s.GetFilteredFlights(
                    filter.SearchBy, filter.SearchString, filter.FromDate, filter.ToDate))
                .ReturnsAsync(Result<List<FlightViewModel>>.Failure(new Error("Filter", errorMessage)));

            // Act
            var result = await _controller.Index(filter);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IPagedList<FlightViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal(errorMessage, _controller.TempData["error"]);
        }

        [Fact]
        public async Task Index_FilterServiceReturnsNull_ReturnsViewWithWarningAndError()
        {
            // Arrange
            var filter = new FlightFilterViewModel { Page = 1, PageSize = 10 };
            _flightServiceMock.Setup(s => s.GetFilteredFlights(
                    filter.SearchBy, filter.SearchString, filter.FromDate, filter.ToDate))
                .ReturnsAsync(Result<List<FlightViewModel>>
                .Failure(new Error("Flights.Null", "Failed to retrieve flight data.")));

            // Act
            var result = await _controller.Index(filter);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IPagedList<FlightViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("Failed to retrieve flight data.", _controller.TempData["error"]);
        }

        [Fact]
        public async Task Index_SortingServiceFails_ReturnsUnsortedFlightsWithError()
        {
            // Arrange
            var filter = new FlightFilterViewModel
            {
                Page = 1,
                PageSize = 10,
                SortBy = "Price"
            };

            var flights = new List<FlightViewModel>
            {
                new FlightViewModel { Id = 1, BasePrice = 200 },
                new FlightViewModel { Id = 2, BasePrice = 100 }
            };

            _flightServiceMock.Setup(s => s.GetFilteredFlights(
                    filter.SearchBy, filter.SearchString, filter.FromDate, filter.ToDate))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            _flightServiceMock.Setup(s => s.GetSortedFlightsAsync(
                    flights, filter.SortBy, filter.SortOrder))
                .ReturnsAsync(Result<List<FlightViewModel>>.Failure(new Error("Sort", "Sorting failed")));

            // Act
            var result = await _controller.Index(filter);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IPagedList<FlightViewModel>>(viewResult.Model);
            Assert.Equal(2, model.Count); // unsorted but still returned
            Assert.Equal("Sorting failed", _controller.TempData["error"]);
        }

        [Fact]
        public async Task Index_Exception_ReturnsViewWithErrorAndEmptyList()
        {
            // Arrange
            var filter = new FlightFilterViewModel();
            _flightServiceMock.Setup(s => s.GetFilteredFlights(
                    filter.SearchBy, filter.SearchString, filter.FromDate, filter.ToDate))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.Index(filter);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IPagedList<FlightViewModel>>(viewResult.Model);
            Assert.Empty(model);
            Assert.Equal("An unexpected error occurred while searching for flights.", _controller.ViewBag.ErrorMessage);
        }

        [Fact]
        public async Task Index_Pagination_DefaultsToPage1PageSize10()
        {
            // Arrange
            var filter = new FlightFilterViewModel(); // no page/pageSize set
            var flights = new List<FlightViewModel> { new FlightViewModel(), new FlightViewModel() };

            _flightServiceMock.Setup(s => s.GetFilteredFlights(
                    filter.SearchBy, filter.SearchString, filter.FromDate, filter.ToDate))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            _flightServiceMock.Setup(s => s.GetSortedFlightsAsync(
                    flights, filter.SortBy, filter.SortOrder))
                .ReturnsAsync(Result<List<FlightViewModel>>.Success(flights));

            // Act
            var result = await _controller.Index(filter);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IPagedList<FlightViewModel>>(viewResult.Model);
            Assert.Equal(1, model.PageNumber);
            Assert.Equal(10, model.PageSize);
            Assert.Equal(1, _controller.ViewBag.CurrentPage);
            Assert.Equal(10, _controller.ViewBag.PageSize);
        }

        #endregion

        #region Details (GET)

        [Fact]
        public async Task Details_FlightExists_ReturnsViewWithFlightViewModel()
        {
            // Arrange
            int flightId = 1;
            var flightEntity = new Flight
            {
                Id = flightId,
                Airline = new Airline(),
                Airplane = new Airplane(),
                DepartureAirport = new Airport(),
                ArrivalAirport = new Airport(),
                FlightSeats = new List<FlightSeat>()
            };

            _flightRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()))
                .ReturnsAsync(flightEntity);

            // Act
            var result = await _controller.Details(flightId);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<FlightViewModel>(viewResult.Model);
            Assert.Equal(flightId, model.Id);
        }

        [Fact]
        public async Task Details_FlightNotFound_ReturnsNotFound()
        {
            // Arrange
            int flightId = 99;
            _flightRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()))
                .ReturnsAsync((Flight)null);

            // Act
            var result = await _controller.Details(flightId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_Exception_RedirectsToIndexWithError()
        {
            // Arrange
            int flightId = 1;
            _flightRepoMock.Setup(r => r.GetEntityWithSpecAsync(It.IsAny<ISpecification<Flight>>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.Details(flightId);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(FlightController.Index), redirectResult.ActionName);
            Assert.Equal("An error occurred while loading flight details.", _controller.TempData["error"]);
        }

        #endregion
    }
}