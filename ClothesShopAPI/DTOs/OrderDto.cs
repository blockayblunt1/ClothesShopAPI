using ClothesShopAPI.Models;

namespace ClothesShopAPI.DTOs
{
    public class CreateOrderDto
    {
        // Order will be created from the user's cart
    }

    public class OrderResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public List<OrderItemResponseDto> Items { get; set; } = new List<OrderItemResponseDto>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Stripe payment fields
        public string? StripePaymentIntentId { get; set; }
        public string? StripeClientSecret { get; set; }
    }

    public class OrderItemResponseDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductDescription { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; } // Price at time of order
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentSimulationDto
    {
        public int OrderId { get; set; }
        public bool PaymentSuccess { get; set; } = true;
    }
}