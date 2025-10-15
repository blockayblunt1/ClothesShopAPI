using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ClothesShopAPI.Data;
using ClothesShopAPI.Models;
using ClothesShopAPI.DTOs;

namespace ClothesShopAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirst("userId")?.Value ?? "0");
        }

        [HttpGet]
        public async Task<ActionResult<CartResponseDto>> GetCart()
        {
            var userId = GetUserId();

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                // Create cart if it doesn't exist
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
                
                // Return empty cart immediately
                return Ok(new CartResponseDto
                {
                    Id = cart.Id,
                    UserId = cart.UserId,
                    CreatedAt = cart.CreatedAt,
                    UpdatedAt = cart.UpdatedAt,
                    Items = new List<CartItemResponseDto>()
                });
            }

            var response = new CartResponseDto
            {
                Id = cart!.Id,
                UserId = cart.UserId,
                CreatedAt = cart.CreatedAt,
                UpdatedAt = cart.UpdatedAt,
                Items = cart.CartItems.Select(ci => new CartItemResponseDto
                {
                    Id = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    ProductDescription = ci.Product.Description,
                    Price = ci.Product.Price,
                    ProductImage = ci.Product.Image,
                    Quantity = ci.Quantity,
                    CreatedAt = ci.CreatedAt,
                    UpdatedAt = ci.UpdatedAt
                }).ToList()
            };

            return Ok(response);
        }

        [HttpPost("add")]
        public async Task<ActionResult<CartResponseDto>> AddToCart(AddToCartDto addToCartDto)
        {
            var userId = GetUserId();

            // Check if product exists
            var product = await _context.Products.FindAsync(addToCartDto.ProductId);
            if (product == null)
            {
                return NotFound("Product not found");
            }

            // Get or create user's cart
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            // Check if item already exists in cart
            var existingCartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == addToCartDto.ProductId);

            if (existingCartItem != null)
            {
                // Update quantity
                existingCartItem.Quantity += addToCartDto.Quantity;
                existingCartItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Add new item to cart
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = addToCartDto.ProductId,
                    Quantity = addToCartDto.Quantity,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.CartItems.Add(cartItem);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Return the updated cart directly instead of calling GetCart() to prevent recursion
            var updatedCart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cart.Id);

            var response = new CartResponseDto
            {
                Id = updatedCart!.Id,
                UserId = updatedCart.UserId,
                CreatedAt = updatedCart.CreatedAt,
                UpdatedAt = updatedCart.UpdatedAt,
                Items = updatedCart.CartItems.Select(ci => new CartItemResponseDto
                {
                    Id = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    ProductDescription = ci.Product.Description,
                    Price = ci.Product.Price,
                    ProductImage = ci.Product.Image,
                    Quantity = ci.Quantity,
                    CreatedAt = ci.CreatedAt,
                    UpdatedAt = ci.UpdatedAt
                }).ToList()
            };

            return Ok(response);
        }

        [HttpPut("items/{cartItemId}")]
        public async Task<ActionResult<CartResponseDto>> UpdateCartItem(int cartItemId, UpdateCartItemDto updateCartItemDto)
        {
            var userId = GetUserId();

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.Cart.UserId == userId);

            if (cartItem == null)
            {
                return NotFound("Cart item not found");
            }

            cartItem.Quantity = updateCartItemDto.Quantity;
            cartItem.UpdatedAt = DateTime.UtcNow;
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Get updated cart data
            var updatedCart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);

            var response = new CartResponseDto
            {
                Id = updatedCart!.Id,
                UserId = updatedCart.UserId,
                CreatedAt = updatedCart.CreatedAt,
                UpdatedAt = updatedCart.UpdatedAt,
                Items = updatedCart.CartItems.Select(ci => new CartItemResponseDto
                {
                    Id = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    ProductDescription = ci.Product.Description,
                    Price = ci.Product.Price,
                    ProductImage = ci.Product.Image,
                    Quantity = ci.Quantity,
                    CreatedAt = ci.CreatedAt,
                    UpdatedAt = ci.UpdatedAt
                }).ToList()
            };

            return Ok(response);
        }

        [HttpDelete("items/{cartItemId}")]
        public async Task<ActionResult<CartResponseDto>> RemoveFromCart(int cartItemId)
        {
            var userId = GetUserId();

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.Cart.UserId == userId);

            if (cartItem == null)
            {
                return NotFound("Cart item not found");
            }

            _context.CartItems.Remove(cartItem);
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Get updated cart data
            var updatedCart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);

            var response = new CartResponseDto
            {
                Id = updatedCart!.Id,
                UserId = updatedCart.UserId,
                CreatedAt = updatedCart.CreatedAt,
                UpdatedAt = updatedCart.UpdatedAt,
                Items = updatedCart.CartItems.Select(ci => new CartItemResponseDto
                {
                    Id = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    ProductDescription = ci.Product.Description,
                    Price = ci.Product.Price,
                    ProductImage = ci.Product.Image,
                    Quantity = ci.Quantity,
                    CreatedAt = ci.CreatedAt,
                    UpdatedAt = ci.UpdatedAt
                }).ToList()
            };

            return Ok(response);
        }

        [HttpDelete("clear")]
        public async Task<ActionResult<CartResponseDto>> ClearCart()
        {
            var userId = GetUserId();

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                return NotFound("Cart not found");
            }

            _context.CartItems.RemoveRange(cart.CartItems);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Return empty cart
            return Ok(new CartResponseDto
            {
                Id = cart.Id,
                UserId = cart.UserId,
                CreatedAt = cart.CreatedAt,
                UpdatedAt = cart.UpdatedAt,
                Items = new List<CartItemResponseDto>()
            });
        }
    }
}