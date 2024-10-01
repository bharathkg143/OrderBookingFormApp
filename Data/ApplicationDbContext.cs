using Microsoft.EntityFrameworkCore;
using OrderBookingFormApp.Models;

namespace OrderBookingFormApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }

        public DbSet<SalesBookedData> SalesBookedDatas { get; set; }
        public DbSet<GeneratedOTP> GeneratedOTPs { get; set; }
        public DbSet<SMSInteraction> SMSInteractions { get; set; }
        public DbSet<ConfirmSalesBooking> ConfirmSalesBookings { get; set; }

    }
}
