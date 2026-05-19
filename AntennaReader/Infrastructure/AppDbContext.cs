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
    public class AppDbContext : DbContext
    {
        // Tables
        public DbSet<AntennaDiagram> AntennaDiagrams { get; set; } = null!;
        public DbSet<AntennaMeasurement> AntennaMeasurements { get; set; } = null!;
        public DbSet<AntennaInterpolatedMeasurement> AntennaInterpolatedMeasurements { get; set; } = null!;
        public DbSet<DrawingCanvasSetting> DrawingCanvasSettings { get; set; } = null!;

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
                .HasIndex(m => new { m.AntennaDiagramId, m.Angle })
                .IsUnique();

            modelBuilder.Entity<AntennaDiagram>()
                .HasMany(d => d.InterpolatedMeasurements)
                .WithOne(m => m.Diagram)
                .HasForeignKey(m => m.AntennaDiagramId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AntennaInterpolatedMeasurement>()
                .HasIndex(m => new { m.AntennaDiagramId, m.Angle })
                .IsUnique();
        }

        public void SaveDrawingCanvasSetting(string name, DrawingCanvasSetting setting)
        {
            DrawingCanvasSetting? existingSetting = DrawingCanvasSettings.FirstOrDefault(s => s.Name == name);
            if (existingSetting == null)
            {
                setting.Name = name;
                setting.LastModified = DateTime.Now;
                DrawingCanvasSettings.Add(setting);
            }
            else
            {
                existingSetting.IsLogScale = setting.IsLogScale;
                existingSetting.lowerBound = setting.lowerBound;
                existingSetting.upperBound = setting.upperBound;
                existingSetting.ContourStep = setting.ContourStep;
                existingSetting.CsvExportPrecision = setting.CsvExportPrecision;
                existingSetting.PATExportPrecision = setting.PATExportPrecision;
                existingSetting.LastModified = DateTime.Now;
            }
            SaveChanges();
        }

        public void DeleteDrawingCanvasSetting(string name)
        {
            DrawingCanvasSetting? existingSetting = DrawingCanvasSettings.FirstOrDefault(s => s.Name == name);
            if (existingSetting != null)
            {
                DrawingCanvasSettings.Remove(existingSetting);
                SaveChanges();
            }
        }

        public List<DrawingCanvasSetting> GetAllDCSettings()
        { 
            return DrawingCanvasSettings.OrderBy(s => s.Name).ToList();
        }
    }
}