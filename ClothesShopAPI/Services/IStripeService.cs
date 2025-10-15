using ClothesShopAPI.DTOs;
using ClothesShopAPI.Models;

namespace ClothesShopAPI.Services
{
    public interface IStripeService
    {
        Task<PaymentIntentResponseDto> CreatePaymentIntentAsync(Order order);
        Task<bool> ConfirmPaymentAsync(string paymentIntentId);
        Task<bool> HandleWebhookAsync(string json, string signature);
        Task<string> GetPaymentStatusAsync(string paymentIntentId);
    }
}