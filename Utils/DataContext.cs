using Gateway.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Utils
{
    class DataContext(DbContextOptions<DataContext> options) : IdentityDbContext<User>(options) {}
}
