﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Data;
using OrderManagement.Models;
using OrderManagement.Models.DTO;
using OrderManagement.Repositories;
using AutoMapper;
using OrderManagement.Common;
using OrderManagement.ExternalServices;


namespace OrderManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderManagementController : ControllerBase
    {

        private readonly AppDbContext dbContext;
        private readonly IOrderRepository orderRepository;
        private readonly IOrderDetailsRepository orderDetailsRepository;
        private readonly IMapper _mapper;
        public OrderManagementController(AppDbContext appDbContext, IOrderRepository orderRepo, IOrderDetailsRepository orderDetailsRepo, IMapper mapper)
        {
            this.dbContext = appDbContext;
            this.orderRepository = orderRepo;
            this.orderDetailsRepository = orderDetailsRepo;
            _mapper = mapper;
        }

        //[HttpGet]
        //public async Task<IActionResult> GetAllOrders()
        //{
        //    try
        //    {
        //        var orderModel = await orderRepository.GetAllOrdersAsync();

        //        if (orderModel == null || !orderModel.Any())
        //        {
        //            return NotFound(new
        //            {
        //                Message = "No orders found.",
        //                ErrorMessage = "No data available."
        //            });
        //        }

        //        // Use AutoMapper to map the list of Product entities to ProductDTOs.
        //        var productDTOs = _mapper.Map<List<OrderDTO>>(orderModel);

        //        return Ok(new
        //        {
        //            Message = "Orders retrieved successfully.",
        //            Products = productDTOs,
        //            ErrorMessage = string.Empty
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new
        //        {
        //            Message = "An error occurred while retrieving the products.",
        //            ErrorMessage = ex.Message
        //        });
        //    }
        //}


        //[HttpGet]
        //[Route("{id}")]
        //public async Task<IActionResult> GetOrderById(Guid id)
        //{
        //    try
        //    {
        //        var orderById = await orderRepository.GetOrderByIdAsync(id);

        //        if (orderById == null)
        //        {
        //            return NotFound(new
        //            {
        //                Message = "Order not found.",
        //                ProductCode = string.Empty,
        //                ErrorMessage = "Invalid Order ID."
        //            });
        //        }

        //        // Use AutoMapper to map the Product entity to ProductDTO.
        //        var productDto = _mapper.Map<OrderDTO>(orderById);

        //        return Ok(new
        //        {
        //            Message = "Product retrieved successfully.",
        //            Product = productDto,
        //            ErrorMessage = string.Empty
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new
        //        {
        //            Message = "An error occurred while retrieving the product.",
        //            ProductCode = string.Empty,
        //            ErrorMessage = ex.Message
        //        });
        //    }
        //}


        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDTO orderRequestDto)
        {

            if (orderRequestDto == null || orderRequestDto.OrderDetails == null || orderRequestDto.OrderDetails.Count == 0)
            {
                return BadRequest(new { Message = "Invalid order data.", ErrorMessage = "Order details are missing." });
            }

            try
            {
                var totalPrice = CalculateTotalCost(orderRequestDto.OrderDetails);  // Add logic to calculate total price
                //var shippingCost = CalculateShippingCost(totalPrice);  // Calculate shipping cost

                // Map the incoming CreateProductDTO to a Product entity.
                var orderModel = _mapper.Map<CreateOrderDTO, Order>(orderRequestDto);
                orderModel.OrderStatus = (int)OrderStatus.New;
                orderModel.DeliveryPersonnelID = null;
                orderModel.PaymentMode = (int)PaymentMode.Cash;
                orderModel.TotalPrice = totalPrice;
                //orderModel.ShippingCost = shippingCost;
                orderModel.CreatedOn = DateTime.UtcNow;
                orderModel.IsActive = true;

                // Use Repository to create Product
                orderModel = await orderRepository.CreateOrderAsync(orderModel);

                //Loop through the list of OrderDetails from the request
                foreach (var detailDto in orderRequestDto.OrderDetails)
                {
                    // Map CreateOrderDetailRequestDto to OrderDetail
                    var orderDetail = _mapper.Map<OrderDetails>(detailDto);
                    
                    // Set the OrderID and other fields for the OrderDetail
                    orderDetail.OrderID = orderModel.OrderID;
                    orderDetail.CreatedBy = orderModel.CreatedBy;
                     orderDetail.CreatedOn = DateTime.UtcNow;
                    orderDetail.IsActive = true;

                    // Save each OrderDetail one by one to the database
                    await orderDetailsRepository.CreateOrderDetailsAsync(orderDetail);
                }

                var response = new
                {
                    Message = "Order created successfully.",
                    OrderId = orderModel.OrderID,
                    ErrorMessage = ""
                };
                return Ok(new { id = orderModel.OrderID, response });
            }

            catch (Exception ex)
            {
                var response = new
                {
                    Message = "Order creation failed.",
                    OrderId = string.Empty,
                    ErrorMessage = ex.Message + Environment.NewLine + ex.InnerException
                };

                return StatusCode(500, response);
            }
        }

        private decimal CalculateShippingCost(decimal totalPrice)
        {
            decimal shippingCost = totalPrice * 0.03m;
            return shippingCost;
        }

        private decimal CalculateTotalCost(List<CreateOrderDetailsDTO> items)
        {
            decimal totalPrice = 0;
            foreach (var item in items)
            {

                // Calculate total price (price * quantity)
                totalPrice += item.ProductPrice * item.Quantity;
            }
            return totalPrice;
        }

        [HttpPut("UpdateOrder")]
        public async Task<IActionResult> UpdateOrder([FromBody] UpdateOrderDTO updateOrderDto)
        {
            if (updateOrderDto == null || updateOrderDto.OrderID == Guid.Empty || string.IsNullOrEmpty(updateOrderDto.OrderStatus))
            {
                return BadRequest(new { Message = "Invalid order update data.", ErrorMessage = "Order ID or status is missing." });
            }

            try
            {
                var existingOrder = await orderRepository.GetOrderByIdAsync(updateOrderDto.OrderID);

                if (existingOrder == null)
                {
                    return NotFound(new { Message = "Order not found.", ErrorMessage = "Invalid Order ID." });
                }

                // Use AutoMapper to update the existing order
                _mapper.Map(updateOrderDto, existingOrder);

                var updatedOrder = await orderRepository.UpdateOrderAsync(existingOrder);

                if (updatedOrder == null)
                {
                    return StatusCode(500, new { Message = "Failed to update order.", OrderId = existingOrder.OrderID, ErrorMessage = "Internal server error." });
                }

                return Ok(new
                {
                    Message = "Order updated successfully.",
                    OrderId = updatedOrder.OrderID,
                    ErrorMessage = string.Empty
                });

                //return Ok(new { Message = "Order updated successfully.", OrderID = updatedOrder.OrderID });
            }
            catch (Exception ex)
            {
                var response = new
                {
                    Message = "An error occurred while updating the order.",
                    OrderId = updateOrderDto.OrderID,
                    ErrorMessage = ex.Message + Environment.NewLine + ex.InnerException
                };

                return StatusCode(500, response);

            }
        }

        [HttpPut("AcceptOrder")]
        public async Task<IActionResult> AcceptOrder([FromBody] AcceptOrderDTO acceptOrderDto, [FromServices] IProductServiceClient productServiceClient)
        {
            if (acceptOrderDto == null || acceptOrderDto.OrderID == Guid.Empty || string.IsNullOrEmpty(acceptOrderDto.OrderStatus))
            {
                return BadRequest(new { Message = "Invalid request.", ErrorMessage = "OrderID and OrderStatus are required." });
            }

            try
            {
                var existingOrder = await orderRepository.GetOrderByIdAsync(acceptOrderDto.OrderID);
                if (existingOrder == null)
                {
                    return NotFound(new { Message = "Order not found.", ErrorMessage = "Invalid Order ID." });
                }

                if (existingOrder.OrderStatus != (int)OrderStatus.New)
                {
                    return BadRequest(new { Message = "Order cannot be accepted.", ErrorMessage = "Only 'New' orders can be accepted." });
                }

                _mapper.Map(acceptOrderDto, existingOrder);

                var orderDetails = await orderRepository.GetOrderDetailsByOrderIdAsync(acceptOrderDto.OrderID);
                if (orderDetails == null || !orderDetails.Any())
                {
                    return BadRequest(new { Message = "No order details found.", ErrorMessage = "Cannot process order without items." });
                }

                foreach (var item in orderDetails)
                {
                    var product = await productServiceClient.GetProductByIdAsync(item.ProductID);

                    if (product == null)
                    {
                        return BadRequest(new { Message = "Product not found.", ErrorMessage = $"ProductID {item.ProductID} does not exist." });
                    }

                    if (product.Quantity == null || product.Quantity < item.Quantity)
                    {
                        return BadRequest(new { Message = "Insufficient stock.", ErrorMessage = $"Product {product.ProductID} has insufficient quantity." });
                    }

                    int updatedQuantity = (product.Quantity ?? 0) - item.Quantity;
                    bool productUpdated = await productServiceClient.UpdateProductQuantityAsync(item.ProductID, updatedQuantity);

                    if (!productUpdated)
                    {
                        return StatusCode(500, new { Message = "Failed to update product quantity.", ErrorMessage = $"Could not update ProductID {product.ProductID}." });
                    }
                }

                var updatedOrder = await orderRepository.UpdateOrderAsync(existingOrder);
                if (updatedOrder == null)
                {
                    return StatusCode(500, new { Message = "Failed to update order.", ErrorMessage = "Internal server error." });
                }

                return Ok(new { Message = "Order accepted successfully.", OrderID = updatedOrder.OrderID });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while accepting the order.", ErrorMessage = ex.Message });
            }
        }

        //GetOrders by Manufacturer id 
        [HttpGet("GetOrdersByManufacturerId/{manufacturerId}")]
        public async Task<IActionResult> GetOrdersByManufacturerId(Guid manufacturerId)
        {
            try
            {
                // Get orders by ManufacturerID
                var orders = await orderRepository.GetOrderByManufacturerIdAsync(manufacturerId);
                if (orders == null || !orders.Any())
                {
                    return Ok(new
                    {
                        Message = "Failed",
                        ErrorMessage = "No orders found for the provided Manufacturer ID."
                    });
                }

                // Get related order details for each order
                var orderDetails = await orderDetailsRepository.FindByCondition(od => orders.Select(o => o.OrderID).Contains(od.OrderID)).ToListAsync();
                // Map entities to DTOs
                var ordersDto = _mapper.Map<List<CreateOrderDTO>>(orders);
                var orderDetailsDto = _mapper.Map<List<CreateOrderDetailsDTO>>(orderDetails);
                return Ok(new
                {

                    Message = "Orders fetched successfully.",
                    ErrorMessage = string.Empty,
                    Data = ordersDto.Select(order => new
                    {

                        retailerID = order.RetailerID,
                        manufacturerID = order.ManufacturerID,
                        paymentMode = order.PaymentMode,
                        paymentCurrency = order.PaymentCurrency,
                        shippingCost = order.ShippingCost,
                        shippingCurrency = order.ShippingCurrency,
                        shippingAddress = order.ShippingAddress,
                        createdBy = order.CreatedBy,
                        orderDetails = orderDetailsDto.Select(detail => new
                        {
                            productID = detail.ProductID,
                            quantity = detail.Quantity,
                            productPrice = detail.ProductPrice
                        }).ToList()
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "An error occurred while retrieving the orders for - ",
                    ManufacturerId = manufacturerId,
                    ErrorMessage = ex.Message
                });
            }
        }


        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetOrderById(Guid id)
        {            
            try
            {
                // Get orders by ManufacturerID
                var order = await orderRepository.GetOrderByOrderIdAsync(id);
                if (order == null || !order.Any())
                {
                    return Ok(new
                    {
                        Message = "Failed",
                        ErrorMessage = "No order found for the provided order ID."
                    });
                }

                // Get related order details for each order
                var orderDetails = await orderDetailsRepository.FindByCondition(od => order.Select(o => o.OrderID).Contains(od.OrderID)).ToListAsync();
                
                // Map entities to DTOs
                var orderDto = _mapper.Map<List<GetOrderDTO>>(order);
                var orderDetailsDto = _mapper.Map<List<GetOrderDetailsDTO>>(orderDetails);
                return Ok(new
                {

                    Message = "Order fetched successfully.",
                    ErrorMessage = string.Empty,
                    Data = orderDto.Select(order => new
                    {
                        orderID =  order.OrderID,
                        retailerID = order.RetailerID,
                        manufacturerID = order.ManufacturerID,
                        deliveryPersonnelId = order.DeliveryPersonnelID,
                        orderStatus = order.OrderStatus,
                        totalPrice = order.TotalPrice,
                        paymentMode = order.PaymentMode,
                        paymentCurrency = order.PaymentCurrency,
                        shippingCost = order.ShippingCost,
                        shippingCurrency = order.ShippingCurrency,
                        shippingAddress = order.ShippingAddress,
                        createdOn = order.CreatedOn,
                        createdBy = order.CreatedBy,
                        updatedOn = order.UpdatedOn,
                        updatedBy = order.UpdatedBy,
                        orderDetails = orderDetailsDto.Select(detail => new
                        {
                            orderDetailid = detail.OrderDetailID,
                            productID = detail.ProductID,
                            quantity = detail.Quantity,
                            productPrice = detail.ProductPrice
                        }).ToList()
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "An error occurred while retrieving the orders for - ",
                    ManufacturerId = id,
                    ErrorMessage = ex.Message
                });
            }
        }

        //[HttpPut]
        //[Route("{id}")]
        //public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductDTO updateProductRequestDto)
        //{
        //    try
        //    {
        //        // Check if the product exists
        //        var existingProduct = await orderRepository.GetProductByIdAsync(id);
        //        if (existingProduct == null)
        //        {
        //            return NotFound(new
        //            {
        //                Message = "Product not found.",
        //                ProductCode = string.Empty,
        //                ErrorMessage = "Invalid product ID."
        //            });
        //        }

        //        // Use AutoMapper to update the existing product with values from the DTO.
        //        _mapper.Map(updateProductRequestDto, existingProduct);

        //        // Optionally update properties that aren’t handled by AutoMapper.
        //        existingProduct.UpdatedOn = DateTime.UtcNow;

        //        // Update the product in the repository
        //        var updatedProduct = await orderRepository.UpdateProductAsync(id, existingProduct);

        //        if (updatedProduct == null)
        //        {
        //            return StatusCode(500, new
        //            {
        //                Message = "Failed to update product.",
        //                ProductCode = string.Empty,
        //                ErrorMessage = "Internal server error."
        //            });
        //        }

        //        return Ok(new
        //        {
        //            Message = "Product updated successfully.",
        //            ProductCode = updatedProduct.ProductCode,
        //            ErrorMessage = string.Empty
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new
        //        {
        //            Message = "An error occurred while updating the product.",
        //            ProductCode = string.Empty,
        //            ErrorMessage = ex.Message
        //        });
        //    }
        //}


        //[HttpDelete]
        //[Route("{id}")]
        //public async Task<IActionResult> DeleteProduct(Guid id)
        //{
        //    try
        //    {
        //        // Check if the product exists
        //        var productById = await orderRepository.GetProductByIdAsync(id);
        //        if (productById == null)
        //        {
        //            return NotFound(new
        //            {
        //                Message = "Product not found.",
        //                ProductCode = string.Empty,
        //                ErrorMessage = "Invalid product ID."
        //            });
        //        }

        //        productById.IsActive = false;

        //        await orderRepository.UpdateProductAsync(id, productById);
        //        return Ok(new
        //        {
        //            Message = "Product deleted successfully.",
        //            ProductCode = productById.ProductCode,
        //            ErrorMessage = string.Empty
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new
        //        {
        //            Message = "An error occurred while deleting the product.",
        //            ProductCode = string.Empty,
        //            ErrorMessage = ex.Message
        //        });
        //    }
        //}


    }
}

