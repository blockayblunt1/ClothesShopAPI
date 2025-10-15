using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClothesShopAPI.Data;
using ClothesShopAPI.DTOs;
using ClothesShopAPI.Services;

namespace ClothesShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IStripeService _stripeService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(IStripeService stripeService, ApplicationDbContext context, ILogger<PaymentController> logger)
        {
            _stripeService = stripeService;
            _context = context;
            _logger = logger;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirst("userId")?.Value ?? "0");
        }

        [HttpPost("create-payment-intent")]
        [Authorize]
        public async Task<ActionResult<PaymentIntentResponseDto>> CreatePaymentIntent(CreatePaymentIntentDto dto)
        {
            try
            {
                var userId = GetUserId();

                // Get the order and verify it belongs to the user
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.UserId == userId);

                if (order == null)
                {
                    return NotFound("Order not found");
                }

                if (order.Status != Models.OrderStatus.Pending)
                {
                    return BadRequest("Order is not in pending status");
                }

                // Check if payment intent already exists for this order
                if (!string.IsNullOrEmpty(order.StripePaymentIntentId))
                {
                    var existingStatus = await _stripeService.GetPaymentStatusAsync(order.StripePaymentIntentId);
                    if (existingStatus == "succeeded")
                    {
                        return BadRequest("Payment has already been completed for this order");
                    }
                    
                    // Return existing payment intent if it's still valid
                    if (existingStatus != "canceled" && !string.IsNullOrEmpty(order.StripeClientSecret))
                    {
                        return Ok(new PaymentIntentResponseDto
                        {
                            PaymentIntentId = order.StripePaymentIntentId,
                            ClientSecret = order.StripeClientSecret,
                            Amount = order.TotalAmount,
                            Currency = "usd",
                            Status = existingStatus
                        });
                    }
                }

                var paymentIntent = await _stripeService.CreatePaymentIntentAsync(order);
                return Ok(paymentIntent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create payment intent for order {OrderId}", dto.OrderId);
                return StatusCode(500, "Failed to create payment intent");
            }
        }

        [HttpPost("confirm")]
        [Authorize]
        public async Task<ActionResult> ConfirmPayment(ConfirmPaymentDto dto)
        {
            try
            {
                var userId = GetUserId();

                // Find the order associated with this payment intent
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.StripePaymentIntentId == dto.PaymentIntentId && o.UserId == userId);

                if (order == null)
                {
                    return NotFound("Order not found for this payment intent");
                }

                var isConfirmed = await _stripeService.ConfirmPaymentAsync(dto.PaymentIntentId);

                if (isConfirmed)
                {
                    order.Status = Models.OrderStatus.Paid;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return Ok(new { message = "Payment confirmed successfully" });
                }

                return BadRequest("Payment confirmation failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm payment for PaymentIntent {PaymentIntentId}", dto.PaymentIntentId);
                return StatusCode(500, "Failed to confirm payment");
            }
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<ActionResult> HandleStripeWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

                if (string.IsNullOrEmpty(signature))
                {
                    _logger.LogWarning("Stripe webhook received without signature");
                    return BadRequest("No signature provided");
                }

                var handled = await _stripeService.HandleWebhookAsync(json, signature);

                if (handled)
                {
                    return Ok();
                }

                return BadRequest("Webhook processing failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe webhook processing failed");
                return StatusCode(500, "Webhook processing failed");
            }
        }

        [HttpGet("status/{paymentIntentId}")]
        [Authorize]
        public async Task<ActionResult<object>> GetPaymentStatus(string paymentIntentId)
        {
            try
            {
                var userId = GetUserId();

                // Verify the payment intent belongs to the user
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.StripePaymentIntentId == paymentIntentId && o.UserId == userId);

                if (order == null)
                {
                    return NotFound("Payment intent not found");
                }

                var status = await _stripeService.GetPaymentStatusAsync(paymentIntentId);

                return Ok(new 
                { 
                    paymentIntentId = paymentIntentId,
                    status = status,
                    orderId = order.Id,
                    orderStatus = order.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payment status for {PaymentIntentId}", paymentIntentId);
                return StatusCode(500, "Failed to get payment status");
            }
        }

        [HttpGet("config")]
        [AllowAnonymous]
        public ActionResult GetStripeConfig()
        {
            var publishableKey = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["Stripe:PublishableKey"];

            return Ok(new { publishableKey });
        }
    }
}