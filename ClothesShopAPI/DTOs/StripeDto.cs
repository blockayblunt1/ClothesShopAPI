using System.ComponentModel.DataAnnotations;

namespace ClothesShopAPI.DTOs
{
    public class CreatePaymentIntentDto
    {
        [Required]
        public int OrderId { get; set; }
    }

    public class PaymentIntentResponseDto
    {
        public string PaymentIntentId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "usd";
        public string Status { get; set; } = string.Empty;
    }

    public class ConfirmPaymentDto
    {
        [Required]
        public string PaymentIntentId { get; set; } = string.Empty;
    }

    public class StripeWebhookDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public StripeWebhookDataDto Data { get; set; } = new();
    }

    public class StripeWebhookDataDto
    {
        public StripePaymentIntentDto Object { get; set; } = new();
    }

    public class StripePaymentIntentDto
    {
        public string Id { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}