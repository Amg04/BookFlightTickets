using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.DataAnnotations;
using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Enums;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.ResultPattern;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.Services;
using BookFlightTickets.Core.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookFlightTickets.ServiceTests
{
    public class FlightServiceTests
    {
        private readonly IFixture _fixture;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IRedisCacheService> _cacheServiceMock;
        private readonly ILogger<FlightService> _logger;
        private readonly FlightService _sut;

        private readonly Dictionary<Type, object> _repositoryMocks = new();

        public FlightServiceTests()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            _fixture.Customize<FlightViewModel>(c => c
                .With(vm => vm.BasePrice, () => _fixture.Create<decimal>())
            );
            for (int i = _fixture.Customizations.Count - 1; i >= 0; i--)
            {
                if (_fixture.Customizations[i] is RangeAttributeRelay ||
                    _fixture.Customizations[i] is NumericRangedRequestRelay)
                {
                    _fixture.Customizations.RemoveAt(i);
                }
            }
            _fixture.Customize<FlightViewModel>(c => c
                .With(vm => vm.BasePrice, () => _fixture.Create<decimal>())
            );

            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _cacheServiceMock = new Mock<IRedisCacheService>();
            _logger = NullLogger<FlightService>.Instance;

            SetupRepositoryMock<Flight>();
            SetupRepositoryMock<Airline>();
            SetupRepositoryMock<Airport>();
            SetupRepositoryMock<FlightSeat>();

            _sut = new FlightService(
                _unitOfWorkMock.Object,
                _logger,
                _cacheServiceMock.Object);
        }

        private Mock<IGenericRepository<TEntity>> GetRepositoryMock<TEntity>() where TEntity : BaseClass
        {
            if (_repositoryMocks.TryGetValue(typeof(TEntity), out var mock))
                return (Mock<IGenericRepository<TEntity>>)mock;

            var newMock = new Mock<IGenericRepository<TEntity>>();
            _repositoryMocks[typeof(TEntity)] = newMock;
            _unitOfWorkMock.Setup(u => u.Repository<TEntity>()).Returns(newMock.Object);
            return newMock;
        }

        private void SetupRepositoryMock<TEntity>() where TEntity : BaseClass => GetRepositoryMock<TEntity>();

        #region GetFilteredFlights

        public class GetFilteredFlightsTests : FlightServiceTests
        {
            [Fact]
            public async Task Should_ReturnSuccess_WithFlights_WhenCriteriaMatch()
            {
                // Arrange
                var searchBy = nameof(FlightViewModel.Airline.Name);
                var searchString = "EgyptAir";
                var fromDate = DateTime.UtcNow.AddDays(1);
                var toDate = DateTime.UtcNow.AddDays(7);

                var flights = _fixture.Build<Flight>()
                    .With(f => f.Airline, _fixture.Build<Airline>().With(a => a.Name, searchString).Create())
                    .With(f => f.DepartureAirport, _fixture.Create<Airport>())
                    .With(f => f.ArrivalAirport, _fixture.Create<Airport>())
                    .With(f => f.Airplane, _fixture.Create<Airplane>())
                    .CreateMany(3).ToList();

                var flightSeats = _fixture.Build<FlightSeat>()
                    .With(s => s.IsAvailable, true)
                    .CreateMany(5).ToList();
                foreach (var f in flights)
                    f.FlightSeats = flightSeats;

                var cacheKey = $"flights:filtered:searchBy:{searchBy.ToLower()}:search:{searchString.ToLower()
                        .Replace(" ", "_")}:from:{fromDate:yyyyMMdd}:to:{toDate:yyyyMMdd}";

                _cacheServiceMock.Setup(c => c.GetAsync<Result<List<FlightViewModel>>>(cacheKey))
                    .ReturnsAsync((Result<List<FlightViewModel>>?)null);

                var flightRepoMock = GetRepositoryMock<Flight>();
                flightRepoMock
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<Flight>>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(flights);

                _cacheServiceMock
                    .Setup(c => c.SetAsync(cacheKey, It.IsAny<Result<List<FlightViewModel>>>(), It.IsAny<TimeSpan>()))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _sut.GetFilteredFlights(searchBy, searchString, fromDate, toDate);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().HaveCount(3);
                result.Value.Should().AllSatisfy(vm => vm.Airline.Name.Should().Be(searchString));
            }

            [Fact]
            public async Task Should_ReturnFromCache_WhenCacheHit()
            {
                // Arrange
                var searchBy = nameof(FlightViewModel.Airline.Name);
                var searchString = "EgyptAir";
                var fromDate = DateTime.UtcNow.AddDays(1);
                var toDate = DateTime.UtcNow.AddDays(7);
                var cacheKey = $"flights:filtered:searchBy:{searchBy.ToLower()}:search:{searchString.ToLower()
                    .Replace(" ", "_")}:from:{fromDate:yyyyMMdd}:to:{toDate:yyyyMMdd}";

                var cachedFlights = _fixture.CreateMany<FlightViewModel>(2).ToList();
                var cachedResult = Result<List<FlightViewModel>>.Success(cachedFlights);

                _cacheServiceMock
                    .Setup(c => c.GetAsync<Result<List<FlightViewModel>>>(cacheKey))
                    .ReturnsAsync(cachedResult);

                // Act
                var result = await _sut.GetFilteredFlights(searchBy, searchString, fromDate, toDate);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().BeEquivalentTo(cachedFlights);
                GetRepositoryMock<Flight>().Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Flight>>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenFromDateAfterToDate()
            {
                // Arrange
                var fromDate = DateTime.UtcNow.AddDays(5);
                var toDate = DateTime.UtcNow.AddDays(1);

                // Act
                var result = await _sut.GetFilteredFlights(null, null, fromDate, toDate);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("VALIDATION_001");
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenNoFlightsFound()
            {
                // Arrange
                var searchBy = nameof(FlightViewModel.Airline.Name);
                var searchString = "NonExistentAirline";
                var fromDate = DateTime.UtcNow.AddDays(1);
                var toDate = DateTime.UtcNow.AddDays(7);

                var cacheKey = $"flights:filtered:searchBy:{searchBy.ToLower()}:search:{searchString.ToLower()
                    .Replace(" ", "_")}:from:{fromDate:yyyyMMdd}:to:{toDate:yyyyMMdd}";

                _cacheServiceMock.Setup(c => c.GetAsync<Result<List<FlightViewModel>>>(cacheKey))
                    .ReturnsAsync((Result<List<FlightViewModel>>?)null);

                var flightRepoMock = GetRepositoryMock<Flight>();
                flightRepoMock
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<Flight>>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Flight>());

                // Act
                var result = await _sut.GetFilteredFlights(searchBy, searchString, fromDate, toDate);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("FLIGHT_001");
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenExceptionOccurs()
            {
                // Arrange
                var fromDate = DateTime.UtcNow.AddDays(1);
                var toDate = DateTime.UtcNow.AddDays(7);

                var cacheKey = $"flights:filtered:searchBy:none:search:none:from:{fromDate:yyyyMMdd}:to:{toDate:yyyyMMdd}";

                _cacheServiceMock .Setup(c => c.GetAsync<Result<List<FlightViewModel>>>(cacheKey))
                    .ReturnsAsync((Result<List<FlightViewModel>>?)null);

                var flightRepoMock = GetRepositoryMock<Flight>();
                flightRepoMock
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<Flight>>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception("Database error"));

                // Act
                var result = await _sut.GetFilteredFlights(null, null, fromDate, toDate);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("FLIGHT_002");
            }
        }

        #endregion

        #region GetSortedFlightsAsync

        public class GetSortedFlightsAsyncTests : FlightServiceTests
        {
            [Fact]
            public async Task Should_ReturnSortedAscending_WhenSortOrderAsc()
            {
                // Arrange
                var flights = _fixture.CreateMany<Flight>(3).ToList();
                var allFlights = flights.Select(f => (FlightViewModel)f).ToList();

                var sortBy = nameof(FlightViewModel.DepartureTime);
                var sortOrder = SortOrderOptions.ASC;

                var cacheKey = GenerateSortedFlightsCacheKey(allFlights, sortBy, sortOrder);

                _cacheServiceMock.Setup(c => c.GetAsync<List<FlightViewModel>>(cacheKey))
                    .ReturnsAsync((List<FlightViewModel>?)null);

                var flightRepoMock = GetRepositoryMock<Flight>();
                flightRepoMock
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<Flight>>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(flights);

                _cacheServiceMock
                    .Setup(c => c.SetAsync(cacheKey, It.IsAny<List<FlightViewModel>>(), It.IsAny<TimeSpan>()))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _sut.GetSortedFlightsAsync(allFlights, sortBy, sortOrder);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().HaveCount(3);

                flightRepoMock.Verify(r => r.GetAllWithSpecAsync(
                    It.Is<ISpecification<Flight>>(spec =>
                        spec.OrderBy != null && spec.OrderByDescending == null),
                    It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task Should_ReturnSortedDescending_WhenSortOrderDesc()
            {
                // Arrange
                var flights = _fixture.CreateMany<Flight>(3).ToList();
                var allFlights = flights.Select(f => (FlightViewModel)f).ToList();
                var sortBy = nameof(FlightViewModel.DepartureTime);
                var sortOrder = SortOrderOptions.DESC;

                var cacheKey = GenerateSortedFlightsCacheKey(allFlights, sortBy, sortOrder);

                _cacheServiceMock.Setup(c => c.GetAsync<List<FlightViewModel>>(cacheKey))
                    .ReturnsAsync((List<FlightViewModel>?)null);

                var flightRepoMock = GetRepositoryMock<Flight>();
                flightRepoMock
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<Flight>>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(flights);

                _cacheServiceMock
                    .Setup(c => c.SetAsync(cacheKey, It.IsAny<List<FlightViewModel>>(), It.IsAny<TimeSpan>()))
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _sut.GetSortedFlightsAsync(allFlights, sortBy, sortOrder);

                // Assert
                result.IsSuccess.Should().BeTrue();
                flightRepoMock.Verify(r => r.GetAllWithSpecAsync(
                    It.Is<ISpecification<Flight>>(spec =>
                        spec.OrderByDescending != null && spec.OrderBy == null),
                    It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task Should_ReturnFromCache_WhenCacheHit()
            {
                // Arrange
                var allFlights = _fixture.CreateMany<FlightViewModel>(3).ToList();
                var sortBy = nameof(FlightViewModel.DepartureTime);
                var sortOrder = SortOrderOptions.ASC;
                var cacheKey = GenerateSortedFlightsCacheKey(allFlights, sortBy, sortOrder);
                var cachedSorted = _fixture.CreateMany<FlightViewModel>(3).ToList();

                _cacheServiceMock
                    .Setup(c => c.GetAsync<List<FlightViewModel>>(cacheKey))
                    .ReturnsAsync(cachedSorted);

                // Act
                var result = await _sut.GetSortedFlightsAsync(allFlights, sortBy, sortOrder);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().BeEquivalentTo(cachedSorted);
                GetRepositoryMock<Flight>().Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Flight>>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenAllFlightsIsNull()
            {
                // Act
                var result = await _sut.GetSortedFlightsAsync(null!, "Any", SortOrderOptions.ASC);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("VALIDATION_001");
            }

            [Fact]
            public async Task Should_ReturnSuccess_WithEmptyList_WhenAllFlightsEmpty()
            {
                // Arrange
                var allFlights = new List<FlightViewModel>();

                // Act
                var result = await _sut.GetSortedFlightsAsync(allFlights, "Any", SortOrderOptions.ASC);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().BeEmpty();
            }

            [Fact]
            public async Task Should_ReturnSuccess_WithUnsortedList_WhenSortByIsEmpty()
            {
                // Arrange
                var allFlights = _fixture.CreateMany<FlightViewModel>(3).ToList();

                // Act
                var result = await _sut.GetSortedFlightsAsync(allFlights, "", SortOrderOptions.ASC);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().BeEquivalentTo(allFlights);
                GetRepositoryMock<Flight>().Verify(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<Flight>>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task Should_ReturnSuccess_WithOriginalList_WhenSortFieldInvalid()
            {
                // Arrange
                var allFlights = _fixture.CreateMany<FlightViewModel>(3).ToList();
                var invalidSortBy = "InvalidField";

                var cacheKey = GenerateSortedFlightsCacheKey(allFlights, invalidSortBy, SortOrderOptions.ASC);

                _cacheServiceMock.Setup(c => c.GetAsync<List<FlightViewModel>>(cacheKey))
                    .ReturnsAsync((List<FlightViewModel>?)null);

                // Act
                var result = await _sut.GetSortedFlightsAsync(allFlights, invalidSortBy, SortOrderOptions.ASC);

                // Assert
                result.IsSuccess.Should().BeTrue();
                result.Value.Should().BeEquivalentTo(allFlights);
            }

            [Fact]
            public async Task Should_ReturnFailure_WhenExceptionOccurs()
            {
                // Arrange
                var allFlights = _fixture.CreateMany<FlightViewModel>(3).ToList();
                var sortBy = nameof(FlightViewModel.DepartureTime);
                var sortOrder = SortOrderOptions.ASC;

                var cacheKey = GenerateSortedFlightsCacheKey(allFlights, sortBy, sortOrder);

                _cacheServiceMock.Setup(c => c.GetAsync<List<FlightViewModel>>(cacheKey))
                    .ReturnsAsync((List<FlightViewModel>?)null);

                var flightRepoMock = GetRepositoryMock<Flight>();
                flightRepoMock
                    .Setup(r => r.GetAllWithSpecAsync(
                        It.IsAny<ISpecification<Flight>>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception("Database error"));

                // Act
                var result = await _sut.GetSortedFlightsAsync(allFlights, sortBy, sortOrder);

                // Assert
                result.IsSuccess.Should().BeFalse();
                result.Error.Code.Should().Be("FLIGHT_003");
            }

            private string GenerateSortedFlightsCacheKey(List<FlightViewModel> flights, string sortBy, SortOrderOptions sortOrder)
            {
                if (!flights.Any())
                    return $"flights:sorted:empty:{sortBy}:{(int)sortOrder}";

                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var flightIds = string.Join(",", flights.Select(f => f.Id).OrderBy(id => id));
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(flightIds));
                var hashString = Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-")[..16];

                return $"flights:sorted:{hashString}:{flights.Count}:{sortBy}:{(int)sortOrder}".ToLower();
            }
        }

        #endregion
    }
}
