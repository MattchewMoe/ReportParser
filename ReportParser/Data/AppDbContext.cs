using System.Diagnostics;
using ReportPaser.Data;
using ReportParser.Entities;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.InkML;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ReportPaser.Data
{
    public class AppDbContext : DbContext
    {
       

        // New DbSet for ComparableSale
        public DbSet<ComparableSale> ComparableSales { get; set; }

       
        // New DbSet for ComparableSaleSearchable
        







        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);




            // Conversion for ImageUrls in ComparableSale
            modelBuilder.Entity<ComparableSale>()
                .Property(e => e.ImageUrls)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                );

            // Conversion for RenovationYears in ComparableSaleSearchable
            modelBuilder.Entity<ComparableSale>()
                .Property(e => e.RenovationYears)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(int.Parse).ToList()
                );

            // Value conversion for the List<string> in ComparableSale
            modelBuilder.Entity<ComparableSale>()
                .Property(e => e.ImageUrls)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                );

            modelBuilder
             .Entity<ComparableSale>()
             .Property(e => e.ImageUrls)
             .HasConversion(
                  v => string.Join(',', v),
                  v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            // User -> Organization







        }
    }
}