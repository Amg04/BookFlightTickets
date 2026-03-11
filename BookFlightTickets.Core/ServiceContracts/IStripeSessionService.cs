using Stripe.Checkout;

namespace BookFlightTickets.Core.ServiceContracts
{
    public interface IStripeSessionService
    {
        Session Create(SessionCreateOptions options);
        Task<Session> GetAsync(string sessionId);
    }
}
