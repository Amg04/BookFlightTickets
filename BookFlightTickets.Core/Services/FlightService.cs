using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Domain.Enums;
using BookFlightTickets.Core.Domain.RepositoryContracts;
using BookFlightTickets.Core.Domain.ResultPattern;
using BookFlightTickets.Core.Domain.Specifications;
using BookFlightTickets.Core.ServiceContracts;
using BookFlightTickets.Core.ViewModels;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace BookFlightTickets.Core.Services
{
    public class FlightService : IFlightService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<FlightService> _logger;
        private readonly IRedisCacheService _cacheService;

        public FlightService(IUnitOfWork unitOfWork,
            ILogger<FlightService> logger,
            IRedisCacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _cacheService = cacheService;
        }

        #region GetFilteredFlights

        public async Task<Result<List<FlightViewModel>>> GetFilteredFlights(
            string searchBy, 
            string? searchString,
            DateTime? fromDate, 
            DateTime? toDate)
        {
            const string methodName = nameof(GetFilteredFlights);
            _logger.LogInformation("Starting {MethodName} with parameters: SearchBy='{SearchBy}', " +
                "SearchString='{SearchString}', FromDate={FromDate}, ToDate={ToDate}"
                , methodName, searchBy, searchString, fromDate, toDate);

            try
            {
                if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
                {
                    _logger.LogWarning("Validation failed in {MethodName}: From date {FromDate} cannot be after To date {ToDate}",
                        methodName, fromDate, toDate);
                    return Result<List<FlightViewModel>>.Failure(
                        new Error("VALIDATION_001", "From date cannot be after To date"));
                }

                string cacheKey = GenerateFilteredFlightsCacheKey(searchBy, searchString, fromDate, toDate);
                _logger.LogDebug("Generated cache key: {CacheKey}", cacheKey);

                var cachedResult = await _cacheService.GetAsync<Result<List<FlightViewModel>>>(cacheKey);
                if (cachedResult != null)
                {
                    return cachedResult;
                }

                var result = await QueryFlightsFromDatabase(searchBy, searchString, fromDate, toDate, methodName);
                await _cacheService.SetAsync(cacheKey, result, GetCacheExpiryForFilteredFlights(fromDate, toDate));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in {MethodName}: {ErrorMessage}", methodName, ex.Message);
                return Result<List<FlightViewModel>>.Failure(
                    new Error("FLIGHT_002", "An error occurred while filtering flights", ex.Message));
            }
        }

        private async Task<Result<List<FlightViewModel>>> QueryFlightsFromDatabase(
          string searchBy,
          string? searchString,
          DateTime? fromDate,
          DateTime? toDate,
          string methodName)
        {
            var predicate = PredicateBuilder.New<Flight>(true);

            if (fromDate.HasValue)
            {
                _logger.LogDebug("Adding FromDate filter: {FromDate}", fromDate.Value.Date);
                predicate = predicate.And(f => f.DepartureTime.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                _logger.LogDebug("Adding ToDate filter: {ToDate}", toDate.Value.Date);
                predicate = predicate.And(f => f.DepartureTime.Date <= toDate.Value.Date);
            }

            if (!string.IsNullOrEmpty(searchBy) && !string.IsNullOrEmpty(searchString))
            {
                _logger.LogDebug(
                    "Adding text search filter: SearchBy='{SearchBy}', SearchString='{SearchString}'",
                    searchBy, searchString);
                predicate = predicate.And(GetTextSearchPredicate(searchBy, searchString));
            }

            var spec = new BaseSpecification<Flight>(predicate);
            spec.Includes.Add(f => f.Airline);
            spec.Includes.Add(f => f.Airplane);
            spec.Includes.Add(f => f.DepartureAirport);
            spec.Includes.Add(f => f.ArrivalAirport);
            spec.ComplexIncludes.Add(q => q.Include(f => f.FlightSeats.Where(s => s.IsAvailable)));

            _logger.LogDebug("Executing database query with specification");
            var flights = await _unitOfWork.Repository<Flight>().GetAllWithSpecAsync(spec);

            if (flights == null || !flights.Any())
            {
                _logger.LogInformation("No flights found matching the criteria in {MethodName}", methodName);
                return Result<List<FlightViewModel>>.Failure(
                    new Error("FLIGHT_001", "No flights found matching the criteria"));
            }

            _logger.LogInformation(
                "Found {FlightCount} flights in {MethodName}",
                flights.Count(), methodName);

            var flightViewModels = flights.Select(f => (FlightViewModel)f).ToList();
            return Result<List<FlightViewModel>>.Success(flightViewModels);
        }

        private Expression<Func<Flight, bool>> GetTextSearchPredicate(string searchBy, string searchString)
        {
            _logger.LogDebug("Creating text search predicate: SearchBy='{SearchBy}', SearchString='{SearchString}'", searchBy, searchString);
            return searchBy switch
            {
                nameof(FlightViewModel.Airline.Name) =>
                    f => f.Airline != null && f.Airline.Name.Contains(searchString),

                "FromAirport" =>
                    f => f.DepartureAirport != null && f.DepartureAirport.Name.Contains(searchString),

                "ToAirport" =>
                    f => f.ArrivalAirport != null && f.ArrivalAirport.Name.Contains(searchString),

                nameof(FlightViewModel.BasePrice) =>
                    GetPricePredicate(searchString),

                nameof(FlightViewModel.AvailableSeatsCount) =>
                    GetAvailableSeatsPredicate(searchString),

                _ => f => true
            };
        }
        private Expression<Func<Flight, bool>> GetPricePredicate(string searchString)
        {
            if (decimal.TryParse(searchString, out decimal price))
            {
                _logger.LogDebug("Creating Price predicate: MaxPrice={Price}", price);
                return f => f.BasePrice <= price;
            }
            _logger.LogWarning("Failed to parse Price search string: '{SearchString}'", searchString);
            return f => true;
        }

        private Expression<Func<Flight, bool>> GetAvailableSeatsPredicate(string searchString)
        {
            if (int.TryParse(searchString, out int seats))
            {
                _logger.LogDebug("Creating available seats predicate: MinSeats={Seats}", seats);
                return f => f.FlightSeats.Count(s => s.IsAvailable) >= seats;
            }
            _logger.LogWarning("Failed to parse seats search string: '{SearchString}'", searchString);
            return f => true;
        }

        #endregion

        #region GetSortedFlightsAsync
        public async Task<Result<List<FlightViewModel>>> GetSortedFlightsAsync(
            List<FlightViewModel> allFlights,
            string sortBy, 
            SortOrderOptions sortOrder)
        {
            const string methodName = nameof(GetSortedFlightsAsync);
            _logger.LogInformation("Starting {MethodName} with parameters: SortBy='{SortBy}', SortOrder={SortOrder}, FlightCount={FlightCount}", methodName, sortBy, sortOrder, allFlights?.Count ?? 0);
            try
            {
                if (allFlights == null)
                {
                    _logger.LogWarning("Validation failed in {MethodName}: Flight list is null", methodName);
                    return Result<List<FlightViewModel>>.Failure( new Error("VALIDATION_001", "Flight list cannot be null"));
                }

                if (!allFlights.Any())
                {
                    _logger.LogInformation("Empty flight list provided to {MethodName}, returning empty result", methodName);
                    return Result<List<FlightViewModel>>.Success(new List<FlightViewModel>());
                }

                if (string.IsNullOrEmpty(sortBy))
                {
                    _logger.LogDebug("No sort criteria provided in {MethodName}, returning unsorted list", methodName);
                    return Result<List<FlightViewModel>>.Success(allFlights);
                }

                string cacheKey = GenerateSortedFlightsCacheKey(allFlights, sortBy, sortOrder);
                _logger.LogDebug("Generated sort cache key: {CacheKey}", cacheKey);

                var cachedSortedFlights = await _cacheService.GetAsync<List<FlightViewModel>>(cacheKey);
                if (cachedSortedFlights != null)
                {
                    _logger.LogDebug(
                        "Cache hit for sort key: {CacheKey}, returning {FlightCount} flights",
                        cacheKey, cachedSortedFlights.Count);
                    return Result<List<FlightViewModel>>.Success(cachedSortedFlights);
                }
                _logger.LogDebug("Cache miss for sort key: {CacheKey}, processing sorting", cacheKey);

                var result = await SortFlightsInDatabase(allFlights, sortBy, sortOrder, methodName);
                await CacheSortedFlightsResult(cacheKey, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in {MethodName}: {ErrorMessage}", methodName, ex.Message);
                return Result<List<FlightViewModel>>.Failure(
                    new Error("FLIGHT_003", "An error occurred while sorting flights", ex.Message));
            }
        }

        private async Task<Result<List<FlightViewModel>>> SortFlightsInDatabase(
           List<FlightViewModel> allFlights,
           string sortBy,
           SortOrderOptions sortOrder,
           string methodName)
        {
            var spec = new BaseSpecification<Flight>();
            var sortResult = ApplySorting(spec, sortBy, sortOrder);

            if (!sortResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Sorting failed in {MethodName}: {ErrorMessage}",
                    methodName, sortResult.Error?.Message);
                return Result<List<FlightViewModel>>.Success(allFlights);
            }

            spec.Includes.Add(f => f.Airline);
            spec.Includes.Add(f => f.Airplane);
            spec.Includes.Add(f => f.DepartureAirport);
            spec.Includes.Add(f => f.ArrivalAirport);
            spec.ComplexIncludes.Add(q => q.Include(f => f.FlightSeats.Where(s => s.IsAvailable)));

            var flightIds = allFlights.Select(f => f.Id).ToList();
            spec.Criteria = f => flightIds.Contains(f.Id);

            _logger.LogDebug(
                "Executing database query for sorting with {FlightCount} flight IDs",
                flightIds.Count);

            var flights = await _unitOfWork.Repository<Flight>().GetAllWithSpecAsync(spec);

            if (flights == null || !flights.Any())
            {
                _logger.LogInformation(
                    "No flights found after applying sort criteria in {MethodName}",
                    methodName);
                return Result<List<FlightViewModel>>.Success(new List<FlightViewModel>());
            }

            _logger.LogInformation(
                "Successfully sorted {FlightCount} flights in {MethodName}",
                flights.Count(), methodName);

            var sortedFlightViewModels = flights.Select(f => (FlightViewModel)f).ToList();
            return Result<List<FlightViewModel>>.Success(sortedFlightViewModels);
        }

        private Result<bool> ApplySorting(
          BaseSpecification<Flight> spec,
          string sortBy,
          SortOrderOptions sortOrder)
        {
            try
            {
                _logger.LogDebug("Applying sorting: SortBy='{SortBy}', SortOrder={SortOrder}", sortBy, sortOrder);
                var sortExpression = GetSortExpression(sortBy);

                if (sortExpression == null)
                {
                    _logger.LogWarning("Invalid sort field requested: {SortBy}", sortBy);
                    return Result<bool>.Failure(
                        new Error("SORT_001", $"Invalid sort field: {sortBy}"));
                }

                if (sortOrder == SortOrderOptions.ASC)
                {
                    spec.OrderByAsc(sortExpression);
                    _logger.LogDebug("Applied ascending order for sort field: {SortBy}", sortBy);
                }
                else
                {
                    spec.OrderByDesc(sortExpression);
                    _logger.LogDebug("Applied descending order for sort field: {SortBy}", sortBy);
                }
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while applying sorting for field '{SortBy}': {ErrorMessage}", sortBy, ex.Message);
                return Result<bool>.Failure(
                    new Error("SORT_002", "An error occurred while applying sorting", ex.Message));
            }
        }

        private Expression<Func<Flight, object>> GetSortExpression(string sortBy)
        {
            _logger.LogTrace("Getting sort expression for field: {SortBy}", sortBy);

            return sortBy switch
            {
                nameof(FlightViewModel.Airline.Name) => f => f.Airline.Name,
                "FromAirport" => f => f.DepartureAirport.Name,
                "ToAirport" => f => f.ArrivalAirport.Name,
                nameof(FlightViewModel.DepartureTime) => f => f.DepartureTime,
                nameof(FlightViewModel.ArrivalTime) => f => f.ArrivalTime,
                nameof(FlightViewModel.BasePrice) => f => f.BasePrice,
                nameof(FlightViewModel.AvailableSeatsCount) =>
                    f => f.FlightSeats.Count(s => s.IsAvailable),
                _ => null!
            };
        }

        #endregion

        #region Helper methods

        private string GenerateFilteredFlightsCacheKey( 
            string searchBy,
            string? searchString,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var keyParts = new List<string>
            {
                "flights",
                "filtered",
                $"searchBy:{searchBy?.ToLower() ?? "none"}",
                $"search:{searchString?.ToLower().Replace(" ", "_") ?? "none"}",
                $"from:{fromDate?.ToString("yyyyMMdd") ?? "none"}",
                $"to:{toDate?.ToString("yyyyMMdd") ?? "none"}"
            };

            return string.Join(":", keyParts);
        }

        private TimeSpan? GetCacheExpiryForFilteredFlights(DateTime? fromDate, DateTime? toDate)
        {
            var now = DateTime.UtcNow;
            
            if (fromDate.HasValue)
            {
                var daysUntilFromDate = (fromDate.Value.Date - now.Date).TotalDays;
                
                return daysUntilFromDate switch
                {
                    > 30 => TimeSpan.FromMinutes(30),  
                    > 7 => TimeSpan.FromMinutes(15),   
                    > 1 => TimeSpan.FromMinutes(5),    
                    _ => TimeSpan.FromMinutes(2)       
                };
            }
            
            return TimeSpan.FromMinutes(10);
        }

        private async Task CacheSortedFlightsResult(string cacheKey, Result<List<FlightViewModel>> result)
        {
            try
            {
                if (result.IsSuccess && result.Value != null && result.Value.Any())
                {
                    await _cacheService.SetAsync(
                        cacheKey,
                        result.Value,
                        TimeSpan.FromMinutes(5));

                    _logger.LogDebug(
                        "Cached sorted {FlightCount} flights for key: {CacheKey}",
                        result.Value.Count, cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache sorted result for key: {CacheKey}", cacheKey);
            }
        }

        private string GenerateSortedFlightsCacheKey(
           List<FlightViewModel> flights,
           string sortBy,
           SortOrderOptions sortOrder)
        {
            if (!flights.Any())
                return $"flights:sorted:empty:{sortBy}:{(int)sortOrder}";

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var flightIds = string.Join(",", flights.Select(f => f.Id).OrderBy(id => id));
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(flightIds));
            var hashString = Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").Substring(0, 16);

            return $"flights:sorted:{hashString}:{flights.Count}:{sortBy}:{(int)sortOrder}".ToLower();
        }

        #endregion

     
    }
}
