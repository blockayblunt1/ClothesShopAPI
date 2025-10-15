using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClothesShopAPI.Data;
using ClothesShopAPI.Models;
using ClothesShopAPI.DTOs;

namespace ClothesShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirst("userId")?.Value ?? "0");
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrders()
        {
            var userId = GetUserId();

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var response = orders.Select(order => new OrderResponseDto
            {
                Id = order.Id,
                UserId = order.UserId,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                StripePaymentIntentId = order.StripePaymentIntentId,
                StripeClientSecret = order.StripeClientSecret,
                Items = order.OrderItems.Select(oi => new OrderItemResponseDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
                    ProductDescription = oi.Product.Description,
                    ProductImage = oi.Product.Image,
                    Quantity = oi.Quantity,
                    Price = oi.Price,
                    CreatedAt = oi.CreatedAt
                }).ToList()
            }).ToList();

            return Ok(response);
        }

        [HttpGet("{orderId}")]
        public async Task<ActionResult<OrderResponseDto>> GetOrder(int orderId)
        {
            var userId = GetUserId();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
            {
                return NotFound("Order not found");
            }

            var response = new OrderResponseDto
            {
                Id = order.Id,
                UserId = order.UserId,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                StripePaymentIntentId = order.StripePaymentIntentId,
                StripeClientSecret = order.StripeClientSecret,
                Items = order.OrderItems.Select(oi => new OrderItemResponseDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
                    ProductDescription = oi.Product.Description,
                    ProductImage = oi.Product.Image,
                    Quantity = oi.Quantity,
                    Price = oi.Price,
                    CreatedAt = oi.CreatedAt
                }).ToList()
            };

            return Ok(response);
        }

        [HttpPost("place")]
        public async Task<ActionResult<OrderResponseDto>> PlaceOrder(CreateOrderDto createOrderDto)
        {
            var userId = GetUserId();

            // Get user's cart with items
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.CartItems.Any())
            {
                return BadRequest("Cart is empty");
            }

            // Calculate total amount
            var totalAmount = cart.CartItems.Sum(ci => ci.Product.Price * ci.Quantity);

            // Create order
            var order = new Order
            {
                UserId = userId,
                TotalAmount = totalAmount,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Create order items from cart items
            var orderItems = cart.CartItems.Select(ci => new OrderItem
            {
                OrderId = order.Id,
                ProductId = ci.ProductId,
                Quantity = ci.Quantity,
                Price = ci.Product.Price, // Store current price
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.OrderItems.AddRange(orderItems);

            // Clear the cart
            _context.CartItems.RemoveRange(cart.CartItems);
            cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Return the created order
            return await GetOrder(order.Id);
        }

        [HttpPost("{orderId}/payment/simulate")]
        [Obsolete("Use Stripe payment integration via PaymentController instead")]
        public async Task<ActionResult<OrderResponseDto>> SimulatePayment(int orderId, PaymentSimulationDto paymentDto)
        {
            var userId = GetUserId();

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
            {
                return NotFound("Order not found");
            }

            if (order.Status != OrderStatus.Pending)
            {
                return BadRequest("Order cannot be paid. Current status: " + order.Status);
            }

            // Simulate payment processing
            if (paymentDto.PaymentSuccess)
            {
                order.Status = OrderStatus.Paid;
                order.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return await GetOrder(order.Id);
        }

        [HttpPatch("{orderId}/status")]
        public async Task<ActionResult<OrderResponseDto>> UpdateOrderStatus(int orderId, [FromBody] string status)
        {
            var userId = GetUserId();

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
            {
                return NotFound("Order not found");
            }

            if (Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            {
                order.Status = orderStatus;
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return await GetOrder(order.Id);
            }

            return BadRequest("Invalid order status");
        }
    }
}