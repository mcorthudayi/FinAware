using FinAware.Bot.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAware.Bot.Data
{
    public class BotDbContext : DbContext
    {
        public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

        public DbSet<BotUserLink> UserLinks { get; set; }
    }
}