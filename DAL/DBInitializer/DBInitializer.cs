using BAL.model;
using DAL.Data;
using DAL.DBInitializer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Utility;

namespace DALProject.DBInitializer
{
    public class DBInitializer : IDBInitializer
    {
        private readonly UserManager<AppUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly BookFilghtsDbContext db;

        public DBInitializer(UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            BookFilghtsDbContext db)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.db = db;
        }

        public async Task Initialize()
        {
            // migration if they are not applied
            try
            {
                if (db.Database.GetPendingMigrations().Count() > 0)
                {
                    db.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB Initialization error: " + ex.Message);
            }

            // create roles if they are not applied

            if (!await roleManager.RoleExistsAsync(SD.Customer))
            {
                // Create CurrentRole
                await roleManager.CreateAsync(new IdentityRole(SD.Customer));
                await roleManager.CreateAsync(new IdentityRole(SD.Admin));

                // if roles are created , then we will create admin User as well
                var result = await userManager.CreateAsync(new AppUser
                {
                    UserName = "admin@gmail.com",
                    Email = "admin@gmail.com",
                    FirstName = "admin",
                    PhoneNumber = "1234567890",
                }, "Admin123*");


                if (result.Succeeded)
                {
                    AppUser? user = await userManager.FindByEmailAsync("admin@gmail.com");
                    if (user != null)
                    {
                        await userManager.AddToRoleAsync(user, SD.Admin);
                    }
                    else
                    {
                        // Log error or handle the case
                        Console.WriteLine("User not found after creation!");
                    }
                }
            }

            return;
        }
    }
}
