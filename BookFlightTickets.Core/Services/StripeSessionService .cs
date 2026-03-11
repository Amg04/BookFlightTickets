using BookFlightTickets.Core.ServiceContracts;
using Stripe.Checkout;

namespace BookFlightTickets.Core.Services
{
    public class StripeSessionService : IStripeSessionService
    {
        private readonly SessionService _sessionService = new();
        public Session Create(SessionCreateOptions options) => _sessionService.Create(options);
        public Task<Session> GetAsync(string sessionId) => _sessionService.GetAsync(sessionId);
    }
}
