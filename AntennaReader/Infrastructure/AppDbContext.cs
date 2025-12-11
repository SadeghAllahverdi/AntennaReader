using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AntennaReader.Models;
using Microsoft.EntityFrameworkCore;

namespace AntennaReader.Infrastructure
{
    /// <summary>
    /// Database context for the AntennaReader application.
    /// </summary>
    public class AppDbContext: DbContext
    {
        // Tabels
        public DbSet<AntennaDiagram> AntennaDiagrams { get; set; } = null!;
        public DbSet<AntennaMeasurement> AntennaMeasurements { get; set; } = null!;

        // initialize sqlite database
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite($"Data Source={AppPaths.DBPath}");
            }
        }
        // relationships
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AntennaDiagram>()
                .HasMany(d => d.Measurements)
                .WithOne(m => m.Diagram)
                .HasForeignKey(m => m.AntennaDiagramId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AntennaMeasurement>()
                .HasIndex(m => new { m.AntennaDiagramId, m.Angle})
                .IsUnique();
        }
    }
}
