using BookFlightTickets.Core.Domain.Entities;
using BookFlightTickets.Core.Shared.Utility;
using BookFlightTickets.Infrastructure.Data.DbContext;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookFlightTickets.Infrastructure.Data.DBInitializer
{
    public class DBInitializer : IDBInitializer
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly BookFilghtsDbContext _context;

        public DBInitializer(UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            BookFilghtsDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task InitializeAsync()
        {
            // Migrations
            if (_context.Database.GetPendingMigrations().Count() > 0)
            {
                _context.Database.Migrate();
            }

            // Roles & Admin User
            await CreateRolesAndAdminUser();
        }

        private async Task CreateRolesAndAdminUser()
        {
            if (!await _roleManager.RoleExistsAsync(SD.Admin))
            {
                await _roleManager.CreateAsync(new IdentityRole(SD.Admin));
                await _roleManager.CreateAsync(new IdentityRole(SD.Customer));

                var adminUser = new AppUser
                {
                    UserName = "admin@gmail.com",
                    Email = "admin@gmail.com",
                    FirstName = "admin",
                    PhoneNumber = "1234567890",
                    Role = SD.Admin,
                };

                var result = await _userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, SD.Admin);
                }
            }
        }
    }
}
