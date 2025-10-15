using Stripe;
using ClothesShopAPI.DTOs;
using ClothesShopAPI.Models;
using ClothesShopAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothesShopAPI.Services
{
    public class StripeService : IStripeService
    {
        private readonly string _secretKey;
        private readonly string _webhookSecret;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StripeService> _logger;

        public StripeService(IConfiguration configuration, ApplicationDbContext context, ILogger<StripeService> logger)
        {
            _secretKey = configuration["Stripe:SecretKey"] ?? throw new ArgumentNullException("Stripe SecretKey not configured");
            _webhookSecret = configuration["Stripe:WebhookSecret"] ?? string.Empty;
            _context = context;
            _logger = logger;
            
            StripeConfiguration.ApiKey = _secretKey;
        }

        public async Task<PaymentIntentResponseDto> CreatePaymentIntentAsync(Order order)
        {
            try
            {
                var service = new PaymentIntentService();
                
                // Convert decimal to cents (Stripe uses cents)
                var amountInCents = (long)(order.TotalAmount * 100);

                var options = new PaymentIntentCreateOptions
                {
                    Amount = amountInCents,
                    Currency = "usd",
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true,
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "order_id", order.Id.ToString() },
                        { "user_id", order.UserId.ToString() }
                    }
                };

                var paymentIntent = await service.CreateAsync(options);

                // Update order with Stripe payment intent details
                order.StripePaymentIntentId = paymentIntent.Id;
                order.StripeClientSecret = paymentIntent.ClientSecret;
                order.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                return new PaymentIntentResponseDto
                {
                    PaymentIntentId = paymentIntent.Id,
                    ClientSecret = paymentIntent.ClientSecret,
                    Amount = order.TotalAmount,
                    Currency = "usd",
                    Status = paymentIntent.Status
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe payment intent creation failed for order {OrderId}", order.Id);
                throw new InvalidOperationException($"Payment intent creation failed: {ex.Message}", ex);
            }
        }

        public async Task<bool> ConfirmPaymentAsync(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);
                
                return paymentIntent.Status == "succeeded";
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Failed to confirm payment for PaymentIntent {PaymentIntentId}", paymentIntentId);
                return false;
            }
        }

        public async Task<bool> HandleWebhookAsync(string json, string signature)
        {
            try
            {
                if (string.IsNullOrEmpty(_webhookSecret))
                {
                    _logger.LogWarning("Webhook secret not configured, skipping signature verification");
                }

                Event? stripeEvent = null;

                if (!string.IsNullOrEmpty(_webhookSecret))
                {
                    stripeEvent = EventUtility.ConstructEvent(json, signature, _webhookSecret);
                }
                else
                {
                    stripeEvent = Event.FromJson(json);
                }

                if (stripeEvent.Type == EventTypes.PaymentIntentSucceeded)
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    if (paymentIntent != null)
                    {
                        await UpdateOrderStatusFromPaymentIntent(paymentIntent, OrderStatus.Paid);
                    }
                }
                else if (stripeEvent.Type == EventTypes.PaymentIntentPaymentFailed)
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    if (paymentIntent != null)
                    {
                        await UpdateOrderStatusFromPaymentIntent(paymentIntent, OrderStatus.Cancelled);
                    }
                }

                return true;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook processing failed");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook processing failed");
                return false;
            }
        }

        public async Task<string> GetPaymentStatusAsync(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);
                return paymentIntent.Status;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Failed to get payment status for PaymentIntent {PaymentIntentId}", paymentIntentId);
                return "unknown";
            }
        }

        private async Task UpdateOrderStatusFromPaymentIntent(PaymentIntent paymentIntent, OrderStatus status)
        {
            if (paymentIntent.Metadata.TryGetValue("order_id", out var orderIdStr) && 
                int.TryParse(orderIdStr, out var orderId))
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (order != null)
                {
                    order.Status = status;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Updated order {OrderId} status to {Status} from Stripe webhook", orderId, status);
                }
            }
        }
    }
}