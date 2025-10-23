﻿using AutoInventoryBackend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AutoInventoryBackend.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
        public DbSet<AuthAttemptLog> AuthAttemptLogs => Set<AuthAttemptLog>();
        public DbSet<RequestLog> RequestLogs => Set<RequestLog>();
        public DbSet<LoginAnomalyDetection> LoginAnomalyDetections => Set<LoginAnomalyDetection>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Vehicle>().HasQueryFilter(v => !v.IsDeleted);
            builder.Entity<LoginAnomalyDetection>()
                .HasIndex(d => new { d.WindowStartUtc, d.IpAddress })
                .IsUnique();
        }
    }
}
