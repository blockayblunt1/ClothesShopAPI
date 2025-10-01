using Microsoft.EntityFrameworkCore;
using ClothesShopAPI.Models;

namespace ClothesShopAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Product entity for PostgreSQL
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd(); // PostgreSQL SERIAL auto-increment
                entity.Property(e => e.Price).HasPrecision(18, 2);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()"); // PostgreSQL current timestamp
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()"); // PostgreSQL current timestamp
                
                // PostgreSQL-specific configurations
                entity.ToTable("products"); // Use lowercase table name (PostgreSQL convention)
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.Price).HasColumnName("price");
                entity.Property(e => e.Image).HasColumnName("image");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            // Seed some initial data
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Classic White T-Shirt",
                    Description = "Comfortable cotton t-shirt perfect for everyday wear",
                    Price = 19.99m,
                    Image = "https://via.placeholder.com/300x400/FFFFFF/000000?text=White+T-Shirt",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Product
                {
                    Id = 2,
                    Name = "Denim Jeans",
                    Description = "High-quality denim jeans with a modern fit",
                    Price = 79.99m,
                    Image = "https://via.placeholder.com/300x400/4169E1/FFFFFF?text=Denim+Jeans",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Product
                {
                    Id = 3,
                    Name = "Summer Dress",
                    Description = "Light and breezy summer dress for warm days",
                    Price = 45.99m,
                    Image = "https://via.placeholder.com/300x400/FF69B4/FFFFFF?text=Summer+Dress",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}