# BookFlightTickets - Online Flight Ticket Booking System

A robust .NET 8.0 ASP.NET Core MVC Web Application designed for managing online flight ticket bookings. Built following Clean Architecture principles for maximum scalability, maintainability, and clean separation of concerns.

🌍 **[Live Application (Demo)](https://bookflights.runasp.net/)**

## 🚀 Features

- **Flight Booking Management**: Search, select, and book flight tickets seamlessly.
- **User & Identity Management**: Secure Registration, Login, and Identity management using ASP.NET Core Identity.
- **Third-Party Authentication**: Seamless login using Google Authentication.
- **High-Performance Caching**: Integration with Redis for caching frequent queries and improving overall response times.
- **Secure Payments Integration**: Built-in payment gateway processing powered by Stripe.
- **Dynamic PDF Generation**: Export tickets and booking confirmations as PDF documents using Rotativa.
- **Advanced Logging & Monitoring**: Structured logging integrated with Serilog (File and Seq sinks) for better observability and debugging.
- **Data Pagination**: Efficient data handling and presentation using X.PagedList.

## 🛠️ Technology Stack

- **Framework**: .NET 8.0 ASP.NET Core MVC
- **Architecture**: Clean Architecture (Core, Infrastructure, UI, Tests)
- **Data Access**: Entity Framework Core with SQL Server (and SQLite for lightweight dev/testing)
- **Authentication**: ASP.NET Core Identity + Google Authentication
- **Caching**: Redis (StackExchange.Redis)
- **Payments**: Stripe (`Stripe.net`)
- **PDF Generation**: Rotativa (`Rotativa.AspNetCore`)
- **Logging**: Serilog (Seq, File)
- **Utilities**: LinqKit for dynamic queries, X.PagedList for pagination

## 📁 Project Structure

```text
BookFlightTickets/ (Solution Root)
├── BookFlightTickets.Core/             # Domain Layer
│   ├── Domain Models/Entities          # Database Models
│   ├── Repository Interfaces           # Data Access Contracts
│   └── Services/DTOs                   # Business Logic & Data Transfer Objects
│
├── BookFlightTickets.Infrastructure/   # Infrastructure Layer
│   ├── Data/                           # DbContext & Entity Configurations
│   └── Repositories/                   # Concrete Repository Implementations
│
├── BookFlightTickets.UI/               # Presentation / MVC Layer
│   ├── Controllers/                    # MVC Controllers
│   ├── Views/                          # Razor Views for UI
│   ├── ViewComponents/                 # Reusable View Components
│   ├── wwwroot/                        # Static files (CSS, JS, Images)
│   ├── appsettings.json                # Configuration and connection strings
│   └── Program.cs                      # Application bootstrap, Services & Middleware setup
│
├── BookFlightTickets.ControllerTests/  # Unit Tests for Controllers
├── BookFlightTickets.ServiceTests/     # Unit Tests for Business Logic
└── BookFlightTickets.IntegrationTests/ # Integration Tests
```

## 📋 Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (LocalDB or full instance)
- Redis Server (Running locally or via Docker `docker run -d -p 6379:6379 redis`)
- Rotativa dependencies (Wkhtmltopdf executable configured for your OS)

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/Amg04/BookFlightTickets
cd BookFlightTickets
```

### 2. Configuration
The application uses `appsettings.json` and User Secrets/Environment Variables to store sensitive configuration. Make sure to configure the required settings before running the application:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "YOUR_SQL_SERVER_CONNECTION_STRING"
  },
  "Redis": {
    "Configuration": "localhost:6379"
  },
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    }
  },
  "Stripe": {
    "SecretKey": "YOUR_STRIPE_SECRET_KEY",
    "PublishableKey": "YOUR_STRIPE_PUBLISHABLE_KEY"
  }
}
```

### 3. Install Dependencies and Run

```bash
# Restore NuGet packages
dotnet restore

# Apply database migrations (Run from BookFlightTickets.UI)
cd BookFlightTickets.UI
dotnet ef database update --project ../BookFlightTickets.Infrastructure

# Run the application
dotnet run
```

## 💡 Key Architectural Highlights

- **Clean Architecture**: Strong enforcement of dependency inversion. The Core has no dependencies on Infrastructure or UI.
- **Testing Approach**: Separated test projects for Unit Tests (Services, Controllers) and Integration Tests to ensure high code quality and reliability.
- **Caching Strategy**: Redis is employed at the infrastructure level to reduce database round-trips for reference data and frequent user requests.

## Author

**Ahmed Ghalwash**
- Computer Science Graduate — Faculty of Computers and Information, Mansoura University
- .NET Developer
